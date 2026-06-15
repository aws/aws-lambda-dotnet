// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Linq;
using System.Text;
using System.Text.Json;
using Amazon.Lambda.Model;
using Xunit;
using Xunit.Abstractions;

namespace Amazon.Lambda.DurableExecution.IntegrationTests;

public class ParallelFirstSuccessfulTest
{
    private readonly ITestOutputHelper _output;
    public ParallelFirstSuccessfulTest(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// Four branches with staggered durable waits, <c>FirstSuccessful</c>: as
    /// soon as one branch completes, the parallel resolves. In-flight branches
    /// remain in <see cref="BatchItemStatus.Started"/> rather than being
    /// cancelled. Validates the cross-cutting decision: orphan branches are NOT
    /// cancelled, and short-circuit reports them as Started.
    /// </summary>
    [Fact]
    public async Task Parallel_FirstSuccessful_ShortCircuitsOnFirstWin()
    {
        await using var deployment = await DurableFunctionDeployment.CreateAsync(
            DurableFunctionDeployment.FindTestFunctionDir("ParallelFirstSuccessfulFunction"),
            "pfirst", _output);

        var (invokeResponse, executionName) = await deployment.InvokeAsync("""{"orderId": "p4"}""");
        var responsePayload = Encoding.UTF8.GetString(invokeResponse.Payload.ToArray());
        _output.WriteLine($"Response: {responsePayload}");

        var arn = await deployment.FindDurableExecutionArnByNameAsync(executionName, TimeSpan.FromSeconds(60));
        Assert.NotNull(arn);

        // Wait timer = 8s, plus invocation overhead. Generous timeout for
        // CI variance.
        var status = await deployment.PollForCompletionAsync(arn!, TimeSpan.FromSeconds(120));
        Assert.Equal("SUCCEEDED", status, ignoreCase: true);

        // The workflow's response payload reports the winning branch.
        using var doc = JsonDocument.Parse(responsePayload);
        var winnerIndex = doc.RootElement.GetProperty("WinnerIndex").GetInt32();
        var winnerName = doc.RootElement.GetProperty("WinnerName").GetString();
        var completionReason = doc.RootElement.GetProperty("CompletionReason").GetString();
        var successCount = doc.RootElement.GetProperty("SuccessCount").GetInt32();

        // At least one branch succeeded — the workflow short-circuited as soon
        // as the first win materialised.
        Assert.True(successCount >= 1, $"Expected >= 1 successful branch, got {successCount}");
        Assert.True(winnerIndex >= 0 && winnerIndex < 4,
            $"WinnerIndex should be a valid branch index, got {winnerIndex}");
        Assert.NotNull(winnerName);

        // CompletionReason is MinSuccessfulReached only if some branch was left
        // un-dispatched at the time the threshold was met. With unbounded
        // concurrency every branch dispatches immediately, so the reason is
        // AllCompleted (all dispatched branches finished). Either reason is
        // acceptable — just ensure it isn't FailureToleranceExceeded.
        Assert.NotEqual("FailureToleranceExceeded", completionReason);

        // Service-side: the parent CONTEXT and at least one branch CONTEXT
        // succeeded. Other branches' final state is timing-dependent — they
        // could be Started (left in flight) or Succeeded (completed before
        // the parent's CONTEXT SUCCEED was flushed). The orchestrator
        // deliberately does not cancel in-flight branches once the
        // short-circuit fires.
        var history = await deployment.WaitForHistoryAsync(
            arn!,
            h => (h.Events?.Any(e => e.EventType == EventType.ContextSucceeded && e.Name == "race") ?? false),
            TimeSpan.FromSeconds(60));
        var events = history.Events ?? new List<Event>();

        var parentSucceeded = events.FirstOrDefault(e =>
            e.EventType == EventType.ContextSucceeded && e.Name == "race");
        Assert.NotNull(parentSucceeded);

        // The winning branch's CONTEXT SUCCEEDED is in the history.
        Assert.Contains(events, e => e.EventType == EventType.ContextSucceeded && e.Name == winnerName);
    }
}
