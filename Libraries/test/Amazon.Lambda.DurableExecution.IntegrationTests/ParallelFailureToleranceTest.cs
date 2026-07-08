// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Linq;
using System.Text;
using Amazon.Lambda.Model;
using Xunit;
using Xunit.Abstractions;

namespace Amazon.Lambda.DurableExecution.IntegrationTests;

public class ParallelFailureToleranceTest
{
    private readonly ITestOutputHelper _output;
    public ParallelFailureToleranceTest(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// Five branches, two fail, ToleratedFailureCount=1. The parallel resolves with
    /// <see cref="CompletionReason.FailureToleranceExceeded"/> but does NOT throw
    /// (JS parity): the workflow completes SUCCEEDED and the returned
    /// <c>BatchResult</c> reports the completion reason. Validates the
    /// failure-tolerance short-circuit and that the parent CONTEXT is checkpointed
    /// ContextSucceeded (the completion reason lives inside the payload).
    /// </summary>
    [Fact]
    public async Task Parallel_FailureToleranceExceeded_CompletesWithReason()
    {
        await using var deployment = await DurableFunctionDeployment.CreateAsync(
            DurableFunctionDeployment.FindTestFunctionDir("ParallelFailureToleranceFunction"),
            "ptol", _output);

        var (invokeResponse, executionName) = await deployment.InvokeAsync("""{"orderId": "p3"}""");
        var responsePayload = Encoding.UTF8.GetString(invokeResponse.Payload.ToArray());
        _output.WriteLine($"Response: {responsePayload}");

        var arn = await deployment.FindDurableExecutionArnByNameAsync(executionName, TimeSpan.FromSeconds(60));
        Assert.NotNull(arn);

        // The operation no longer throws on failure tolerance, so the workflow
        // itself succeeds — the failure surfaces in the result payload, not as a
        // terminal workflow error.
        var status = await deployment.PollForCompletionAsync(arn!, TimeSpan.FromSeconds(60));
        Assert.Equal("SUCCEEDED", status, ignoreCase: true);

        var execution = await deployment.GetExecutionAsync(arn!);
        Assert.Null(execution.Error);
        Assert.Contains("FailureToleranceExceeded", responsePayload, StringComparison.Ordinal);

        // History: parent CONTEXT and at least 2 failed branch contexts visible.
        var history = await deployment.WaitForHistoryAsync(
            arn!,
            h => (h.Events?.Count(e => e.EventType == EventType.ContextStarted) ?? 0) >= 3
              && (h.Events?.Count(e => e.EventType == EventType.ContextFailed) ?? 0) >= 2,
            TimeSpan.FromSeconds(60));
        var events = history.Events ?? new List<Event>();

        // At least 2 branches failed (the third may or may not have been
        // dispatched depending on race).
        Assert.True(
            events.Count(e => e.EventType == EventType.ContextFailed) >= 2,
            $"Expected >= 2 ContextFailed events; got {events.Count(e => e.EventType == EventType.ContextFailed)}");

        // The parent context (named "tolerance") is checkpointed ContextSucceeded
        // even when the failure tolerance is exceeded: ConcurrentOperation always
        // writes the parent batch summary with action SUCCEED (the completion
        // reason lives inside the payload). This matches the Python/JS/Java wire
        // format. The parent CONTEXT itself is NOT recorded as ContextFailed.
        var parentSucceeded = events.FirstOrDefault(e =>
            e.EventType == EventType.ContextSucceeded && e.Name == "tolerance");
        Assert.NotNull(parentSucceeded);
        Assert.DoesNotContain(events, e =>
            e.EventType == EventType.ContextFailed && e.Name == "tolerance");
    }
}
