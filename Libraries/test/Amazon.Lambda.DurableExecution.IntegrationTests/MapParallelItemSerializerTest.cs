// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Linq;
using Amazon.Lambda.Model;
using Xunit;
using Xunit.Abstractions;

namespace Amazon.Lambda.DurableExecution.IntegrationTests;

/// <summary>
/// Cloud integration test for the Map/Parallel per-item serializer (ItemSerializer).
/// Deploys <c>MapParallelItemSerializerFunction</c> and asserts from event history that the
/// map items (m-0, m-1) and parallel branch (p-0) — each configured with a camelCase
/// <c>ItemSerializer</c> — produce camelCase per-item result payloads, while the control
/// step (global serializer) stays PascalCase. Proves ItemSerializer routes end-to-end.
/// </summary>
public class MapParallelItemSerializerTest
{
    private readonly ITestOutputHelper _output;
    public MapParallelItemSerializerTest(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task ItemSerializer_AppliesToMapItemsAndParallelBranches()
    {
        await using var deployment = await DurableFunctionDeployment.CreateAsync(
            DurableFunctionDeployment.FindTestFunctionDir("MapParallelItemSerializerFunction"),
            "mpser", _output);

        var (_, executionName) = await deployment.InvokeAsync("{}");

        var arn = await deployment.FindDurableExecutionArnByNameAsync(executionName, TimeSpan.FromSeconds(60));
        Assert.NotNull(arn);

        var status = await deployment.PollForCompletionAsync(arn!, TimeSpan.FromSeconds(90));
        Assert.Equal("SUCCEEDED", status, ignoreCase: true);

        var itemNames = new[] { "m-0", "m-1", "p-0" };
        var history = await deployment.WaitForHistoryAsync(
            arn!,
            h => (h.Events?.Any(e => e.EventType == EventType.StepSucceeded && e.Name == "control_step") ?? false)
              && itemNames.All(n => h.Events?.Any(e => e.EventType == EventType.ContextSucceeded && e.Name == n) ?? false),
            TimeSpan.FromSeconds(90));
        var events = history.Events ?? new List<Event>();

        // Control step used the global serializer → PascalCase.
        var control = events.First(e => e.EventType == EventType.StepSucceeded && e.Name == "control_step");
        Assert.Contains("\"Message\"", control.StepSucceededDetails.Result.Payload);

        // Each map item + parallel branch used its camelCase ItemSerializer for the result.
        foreach (var name in itemNames)
        {
            var ev = events.First(e => e.EventType == EventType.ContextSucceeded && e.Name == name);
            var payload = ev.ContextSucceededDetails.Result?.Payload ?? string.Empty;
            _output.WriteLine($"{name} payload: {payload}");
            Assert.Contains("\"message\"", payload);
            Assert.DoesNotContain("\"Message\"", payload);
        }
    }
}
