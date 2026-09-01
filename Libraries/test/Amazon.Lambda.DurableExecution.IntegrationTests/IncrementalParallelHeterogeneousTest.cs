// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Linq;
using System.Text;
using Amazon.Lambda.Model;
using Xunit;
using Xunit.Abstractions;

namespace Amazon.Lambda.DurableExecution.IntegrationTests;

public class IncrementalParallelHeterogeneousTest
{
    private readonly ITestOutputHelper _output;
    public IncrementalParallelHeterogeneousTest(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// End-to-end incremental, heterogeneous parallel: three branches registered
    /// one at a time via <c>CreateParallel</c>/<c>BranchAsync</c> return three
    /// unrelated types (string, int, POCO), each retrieved through its own typed
    /// handle. Validates the parent CONTEXT and per-branch CONTEXT checkpoints all
    /// land in the service-side history with the correct names, and that the
    /// heterogeneous per-branch values round-trip into the user-visible result.
    /// </summary>
    [Fact]
    public async Task IncrementalParallel_HeterogeneousBranches_Succeed()
    {
        await using var deployment = await DurableFunctionDeployment.CreateAsync(
            DurableFunctionDeployment.FindTestFunctionDir("IncrementalParallelHeterogeneousFunction"),
            "iphetero", _output);

        var (invokeResponse, executionName) = await deployment.InvokeAsync("""{"orderId": "p1"}""");
        Assert.Equal(200, invokeResponse.StatusCode);

        var responsePayload = Encoding.UTF8.GetString(invokeResponse.Payload.ToArray());
        _output.WriteLine($"Response: {responsePayload}");

        var arn = await deployment.FindDurableExecutionArnByNameAsync(executionName, TimeSpan.FromSeconds(60));
        Assert.NotNull(arn);

        var status = await deployment.PollForCompletionAsync(arn!, TimeSpan.FromSeconds(60));
        Assert.Equal("SUCCEEDED", status, ignoreCase: true);

        // Each heterogeneous branch's typed result surfaces in the user payload.
        Assert.Contains("reserved-p1", responsePayload); // string branch
        Assert.Contains("200", responsePayload);          // int branch
        Assert.Contains("USD:4200", responsePayload);      // POCO branch

        // History is eventually consistent — wait until the parent CONTEXT and all
        // three child CONTEXT checkpoints are visible.
        var history = await deployment.WaitForHistoryAsync(
            arn!,
            h => (h.Events?.Count(e => e.EventType == EventType.ContextStarted) ?? 0) >= 4
              && (h.Events?.Count(e => e.EventType == EventType.ContextSucceeded) ?? 0) >= 4,
            TimeSpan.FromSeconds(60));
        var events = history.Events ?? new List<Event>();

        // Parent + 3 branches = 4 ContextStarted, 4 ContextSucceeded.
        Assert.Equal(4, events.Count(e => e.EventType == EventType.ContextStarted));
        Assert.Equal(4, events.Count(e => e.EventType == EventType.ContextSucceeded));

        var startedNames = events
            .Where(e => e.EventType == EventType.ContextStarted)
            .Select(e => e.Name)
            .ToList();
        Assert.Contains("process-order", startedNames);
        Assert.Contains("inventory", startedNames);
        Assert.Contains("payment", startedNames);
        Assert.Contains("shipping", startedNames);

        // No branch failed.
        Assert.Empty(events.Where(e => e.EventType == EventType.ContextFailed));
    }
}
