// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Linq;
using System.Text;
using Amazon.Lambda.Model;
using Xunit;
using Xunit.Abstractions;

namespace Amazon.Lambda.DurableExecution.IntegrationTests;

/// <summary>
/// Proves a durable function works on a managed dotnet runtime using the
/// <b>class-library programming model</b> — a plain <c>Handler</c> method with no
/// <c>Main</c>/<c>LambdaBootstrap</c> loop, deployed via an <c>Assembly::Type::Method</c>
/// handler string. Confirms the RuntimeSupport durable-execution changes are live in
/// the managed runtime, so the executable model is no longer required.
/// </summary>
public class ClassLibraryTest
{
    private readonly ITestOutputHelper _output;
    public ClassLibraryTest(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task ClassLibrary_TwoSteps_Checkpointed()
    {
        await using var deployment = await DurableFunctionDeployment.CreateAsync(
            DurableFunctionDeployment.FindTestFunctionDir("ClassLibraryFunction"),
            "classlib", _output,
            handler: "ClassLibraryFunction::ClassLibraryFunction.Function::Handler");

        var (invokeResponse, executionName) = await deployment.InvokeAsync("""{"orderId": "chain"}""");
        Assert.Equal(200, invokeResponse.StatusCode);

        var responsePayload = Encoding.UTF8.GetString(invokeResponse.Payload.ToArray());
        _output.WriteLine($"Response: {responsePayload}");

        var arn = await deployment.FindDurableExecutionArnByNameAsync(executionName, TimeSpan.FromSeconds(60));
        Assert.NotNull(arn);

        var status = await deployment.PollForCompletionAsync(arn!, TimeSpan.FromSeconds(60));
        Assert.Equal("SUCCEEDED", status, ignoreCase: true);

        // History is eventually consistent — wait until both steps are indexed.
        var history = await deployment.WaitForHistoryAsync(
            arn!,
            h => (h.Events?.Count(e => e.EventType == EventType.StepStarted) ?? 0) >= 2
              && (h.Events?.Count(e => e.StepSucceededDetails != null) ?? 0) >= 2,
            TimeSpan.FromSeconds(60));
        var events = history.Events ?? new List<Event>();

        // Both steps ran exactly once, in declaration order, each chaining from the
        // previous one's output — same checkpointing behavior as the executable model.
        Assert.Equal(2, events.Count(e => e.EventType == EventType.StepStarted));

        var stepResults = events
            .Where(e => e.StepSucceededDetails != null)
            .Select(e => $"{e.Name}={e.StepSucceededDetails.Result?.Payload?.Trim('"')}")
            .ToList();
        Assert.Equal(
            new[]
            {
                "step_1=a-chain",
                "step_2=a-chain-b",
            },
            stepResults);
    }
}
