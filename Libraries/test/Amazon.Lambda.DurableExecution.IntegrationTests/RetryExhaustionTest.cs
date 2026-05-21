using System.Linq;
using System.Text;
using Amazon.Lambda.Model;
using Xunit;
using Xunit.Abstractions;

namespace Amazon.Lambda.DurableExecution.IntegrationTests;

public class RetryExhaustionTest
{
    private readonly ITestOutputHelper _output;
    public RetryExhaustionTest(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// End-to-end retry exhaustion: step always throws, maxAttempts=3.
    /// Validates that the SDK records exactly three StepStarted/StepFailed pairs,
    /// the final attempt produces a FAIL checkpoint (not RETRY), and the workflow
    /// terminates FAILED with the original exception surfaced through the
    /// execution-level error.
    /// </summary>
    [Fact]
    public async Task AlwaysFailsStep_ExhaustsRetries_TerminatesFailed()
    {
        await using var deployment = await DurableFunctionDeployment.CreateAsync(
            DurableFunctionDeployment.FindTestFunctionDir("RetryExhaustionFunction"),
            "rexhaust", _output);

        var (invokeResponse, executionName) = await deployment.InvokeAsync("""{"orderId": "x"}""");
        var responsePayload = Encoding.UTF8.GetString(invokeResponse.Payload.ToArray());
        _output.WriteLine($"Response: {responsePayload}");

        // Failed workflows return null payload synchronously; locate the execution by name.
        var arn = await deployment.FindDurableExecutionArnByNameAsync(executionName, TimeSpan.FromSeconds(60));
        Assert.NotNull(arn);

        // 2s + 4s of retry delays + 3x execution overhead. Generous headroom for scheduling.
        var status = await deployment.PollForCompletionAsync(arn!, TimeSpan.FromSeconds(120));
        Assert.Equal("FAILED", status, ignoreCase: true);

        // Execution-level error is the original exception from the final attempt.
        var execution = await deployment.GetExecutionAsync(arn!);
        Assert.NotNull(execution.Error);
        Assert.Contains("attempt 3", execution.Error.ErrorMessage);

        var history = await deployment.WaitForHistoryAsync(
            arn!,
            h => (h.Events?.Count(e => e.EventType == EventType.StepStarted) ?? 0) >= 3
              && (h.Events?.Count(e => e.StepFailedDetails != null) ?? 0) >= 3,
            TimeSpan.FromSeconds(60));
        var events = history.Events ?? new List<Event>();

        // Three attempts ran in total — no extra (off-by-one) and no truncation.
        Assert.Equal(3, events.Count(e => e.EventType == EventType.StepStarted));

        // Three failures recorded; no successes.
        Assert.Equal(3, events.Count(e => e.StepFailedDetails != null && e.Name == "always_fails_step"));
        Assert.Empty(events.Where(e => e.StepSucceededDetails != null));

        // Each recorded failure carries the right per-attempt message.
        var failures = events
            .Where(e => e.StepFailedDetails != null && e.Name == "always_fails_step")
            .Select(e => e.StepFailedDetails.Error?.Payload?.ErrorMessage ?? string.Empty)
            .ToList();
        Assert.Contains(failures, m => m.Contains("attempt 1"));
        Assert.Contains(failures, m => m.Contains("attempt 2"));
        Assert.Contains(failures, m => m.Contains("attempt 3"));

        // Service honored the retry delays. No-jitter exponential backoff at 2s/4s
        // means the gap between the first and last StepStarted is >= 6s.
        var startedTimestamps = events
            .Where(e => e.EventType == EventType.StepStarted && e.EventTimestamp.HasValue)
            .OrderBy(e => e.EventTimestamp!.Value)
            .Select(e => e.EventTimestamp!.Value)
            .ToList();
        var totalGap = startedTimestamps[^1] - startedTimestamps[0];
        _output.WriteLine($"Time between first and last attempt: {totalGap.TotalSeconds:F1}s");
        Assert.True(totalGap >= TimeSpan.FromSeconds(6),
            $"Service did not honor retry delays: {totalGap.TotalSeconds:F1}s gap (expected >= 6s)");
    }
}
