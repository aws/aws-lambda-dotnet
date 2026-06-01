// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Linq;
using System.Text;
using Amazon.Lambda.Model;
using Xunit;
using Xunit.Abstractions;

namespace Amazon.Lambda.DurableExecution.IntegrationTests;

public class ChildContextRetryFailsTest
{
    private readonly ITestOutputHelper _output;
    public ChildContextRetryFailsTest(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// End-to-end: a step inside a child context retries until exhausted, then
    /// the child closes as ContextFailed. Validates the child is a single
    /// retry/error boundary — every per-attempt StepStarted/StepFailed (and the
    /// terminal ContextFailed's surfaced exception) reflect the same logical
    /// failure under the same parent op id.
    /// </summary>
    [Fact]
    public async Task ChildContext_RetryExhaustionInsideChild_AllAttemptsParentedUnderChild()
    {
        await using var deployment = await DurableFunctionDeployment.CreateAsync(
            DurableFunctionDeployment.FindTestFunctionDir("ChildContextRetryFailsFunction"),
            "childctxretry", _output);

        var (invokeResponse, executionName) = await deployment.InvokeAsync("""{"orderId": "integ-test-retry"}""");
        var responsePayload = Encoding.UTF8.GetString(invokeResponse.Payload.ToArray());
        _output.WriteLine($"Response: {responsePayload}");

        var arn = await deployment.FindDurableExecutionArnByNameAsync(executionName, TimeSpan.FromSeconds(60));
        Assert.NotNull(arn);

        // 3 attempts with 2s + 4s retry delays plus service-driven re-invokes.
        var status = await deployment.PollForCompletionAsync(arn!, TimeSpan.FromSeconds(120));
        Assert.Equal("FAILED", status, ignoreCase: true);

        var execution = await deployment.GetExecutionAsync(arn!);
        Assert.NotNull(execution.Error);
        Assert.Contains("always-fails", execution.Error.ErrorMessage);

        var history = await deployment.WaitForHistoryAsync(
            arn!,
            h => (h.Events?.Any(e => e.EventType == EventType.ContextStarted) ?? false)
              && (h.Events?.Any(e => e.EventType == EventType.ContextFailed) ?? false)
              && (h.Events?.Count(e => e.EventType == EventType.StepStarted) ?? 0) >= 3,
            TimeSpan.FromSeconds(60));
        var events = history.Events ?? new List<Event>();

        var contextStarted = events.SingleOrDefault(e => e.EventType == EventType.ContextStarted && e.Name == "phase");
        Assert.NotNull(contextStarted);
        var contextOpId = contextStarted!.Id;
        Assert.NotNull(contextOpId);

        // All 3 step attempts (with their per-attempt StepFailed records) ran
        // inside the child boundary.
        var stepStarted = events.Where(e => e.EventType == EventType.StepStarted && e.Name == "always_fails").ToList();
        Assert.Equal(3, stepStarted.Count);
        Assert.All(stepStarted, e => Assert.Equal(contextOpId, e.ParentId));

        var stepFailed = events.Where(e => e.StepFailedDetails != null && e.Name == "always_fails").ToList();
        Assert.Equal(3, stepFailed.Count);
        Assert.All(stepFailed, e => Assert.Equal(contextOpId, e.ParentId));

        // The per-attempt failure messages reflect the user's exception.
        var failureMessages = stepFailed
            .Select(e => e.StepFailedDetails.Error?.Payload?.ErrorMessage ?? string.Empty)
            .ToList();
        Assert.Contains(failureMessages, m => m.Contains("attempt 1"));
        Assert.Contains(failureMessages, m => m.Contains("attempt 2"));
        Assert.Contains(failureMessages, m => m.Contains("attempt 3"));

        // Each StepFailed event preserves StackTrace through the wire — proves
        // StepDetails.Error mapping doesn't drop frames.
        Assert.All(stepFailed, e =>
        {
            var stack = e.StepFailedDetails.Error?.Payload?.StackTrace;
            Assert.NotNull(stack);
            Assert.NotEmpty(stack);
        });

        // The child closes the boundary at the parent scope (root) and surfaces
        // the underlying exception type — a single retry/error envelope.
        var contextFailed = events.SingleOrDefault(e => e.EventType == EventType.ContextFailed && e.Name == "phase");
        Assert.NotNull(contextFailed);
        Assert.Null(contextFailed!.ParentId);
        var contextError = contextFailed.ContextFailedDetails.Error?.Payload;
        Assert.NotNull(contextError);
        Assert.Contains("always-fails", contextError!.ErrorMessage ?? string.Empty);
        // StackTrace round-trips end-to-end — the service preserves it from the
        // checkpointed FAIL update and returns it on replay/history.
        Assert.NotNull(contextError.StackTrace);
        Assert.NotEmpty(contextError.StackTrace);

        Assert.DoesNotContain(events, e => e.StepSucceededDetails != null);
        Assert.DoesNotContain(events, e => e.EventType == EventType.ContextSucceeded);

        // Service honored retry delays: with 2s + 4s and no jitter, the gap
        // between first and last StepStarted should be >= 6s.
        var startedTimestamps = stepStarted
            .Where(e => e.EventTimestamp.HasValue)
            .OrderBy(e => e.EventTimestamp!.Value)
            .Select(e => e.EventTimestamp!.Value)
            .ToList();
        var totalGap = startedTimestamps[^1] - startedTimestamps[0];
        _output.WriteLine($"Time between first and last attempt: {totalGap.TotalSeconds:F1}s");
        Assert.True(totalGap >= TimeSpan.FromSeconds(6),
            $"Service did not honor retry delays inside child: {totalGap.TotalSeconds:F1}s gap (expected >= 6s)");
    }
}
