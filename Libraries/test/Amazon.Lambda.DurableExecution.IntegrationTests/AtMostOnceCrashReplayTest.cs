// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Linq;
using System.Text;
using Amazon.Lambda.Model;
using Xunit;
using Xunit.Abstractions;

namespace Amazon.Lambda.DurableExecution.IntegrationTests;

public class AtMostOnceCrashReplayTest
{
    private readonly ITestOutputHelper _output;
    public AtMostOnceCrashReplayTest(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// Validates the AtMostOncePerRetry crash-recovery wire path: the Lambda
    /// process is killed mid-step on attempt 1 (after START flush, before
    /// SUCCEED). On re-invocation the SDK sees a STARTED checkpoint with no
    /// terminal record and routes through the retry strategy rather than
    /// re-executing the step. Attempt 2 succeeds.
    ///
    /// This is the only path that exercises the StepInterruptedException
    /// synthesis — the unit-test analogue
    /// (StepAsync_AtMostOnce_StartedReplay_TriggersRetryHandler) fakes the
    /// STARTED state in-memory and never proves the service actually delivers
    /// it on a real crash.
    /// </summary>
    [Fact]
    public async Task AtMostOnce_StepCrashesMidExecution_RecoversViaRetry()
    {
        await using var deployment = await DurableFunctionDeployment.CreateAsync(
            DurableFunctionDeployment.FindTestFunctionDir("AtMostOnceCrashFunction"),
            "amocrash", _output);

        var (invokeResponse, executionName) = await deployment.InvokeAsync("""{"orderId": "x"}""");
        var responsePayload = Encoding.UTF8.GetString(invokeResponse.Payload.ToArray());
        _output.WriteLine($"Response: {responsePayload}");

        var arn = await deployment.FindDurableExecutionArnByNameAsync(executionName, TimeSpan.FromSeconds(60));
        Assert.NotNull(arn);

        // 2s retry delay + initial-attempt cold-start + recovery invoke. Generous headroom.
        var status = await deployment.PollForCompletionAsync(arn!, TimeSpan.FromSeconds(120));
        Assert.Equal("SUCCEEDED", status, ignoreCase: true);

        var history = await deployment.WaitForHistoryAsync(
            arn!,
            h => (h.Events?.Any(e => e.StepSucceededDetails != null && e.Name == "crash_then_recover") ?? false),
            TimeSpan.FromSeconds(60));
        var events = history.Events ?? new List<Event>();

        // Attempt 1 was crashed (no SUCCEED), attempt 2 recovered.
        // We expect exactly one StepSucceeded carrying "recovered on attempt 2".
        var succeeded = events.SingleOrDefault(e => e.StepSucceededDetails != null && e.Name == "crash_then_recover");
        Assert.NotNull(succeeded);
        Assert.Equal("\"recovered on attempt 2\"", succeeded!.StepSucceededDetails.Result?.Payload);

        // Two StepStarted events: one per invocation.
        Assert.True(
            events.Count(e => e.EventType == EventType.StepStarted) >= 2,
            "Expected at least 2 StepStarted events (attempt 1 crashed, attempt 2 recovered).");

        // The crash-recovery branch records the synthesized StepInterruptedException
        // as a StepFailed event for attempt 1, with a message identifying the lost
        // attempt rather than a user exception type.
        var failures = events
            .Where(e => e.StepFailedDetails != null && e.Name == "crash_then_recover")
            .Select(e => e.StepFailedDetails.Error?.Payload?.ErrorMessage ?? string.Empty)
            .ToList();
        Assert.NotEmpty(failures);
        Assert.Contains(failures, m => m.Contains("Step result lost", StringComparison.OrdinalIgnoreCase)
                                    || m.Contains("interrupted", StringComparison.OrdinalIgnoreCase)
                                    || m.Contains("previous attempt", StringComparison.OrdinalIgnoreCase));

        // The execution actually crossed at least one invocation boundary
        // (otherwise replay wasn't exercised at all).
        var invocations = events.Where(e => e.InvocationCompletedDetails != null).ToList();
        Assert.True(
            invocations.Count >= 2,
            $"Expected at least 2 InvocationCompleted events (proves crash + replay), got {invocations.Count}");
    }
}
