using System.Linq;
using System.Text;
using Amazon.Lambda.Model;
using Xunit;
using Xunit.Abstractions;

namespace Amazon.Lambda.DurableExecution.IntegrationTests;

public class RetryTest
{
    private readonly ITestOutputHelper _output;
    public RetryTest(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// End-to-end retry: step throws on attempts 1 and 2, succeeds on attempt 3.
    /// Validates that the service honors the RETRY checkpoint, schedules the
    /// requested delay, and re-invokes the Lambda — none of which the unit
    /// tests can prove (they fake state transitions in-memory).
    /// </summary>
    [Fact]
    public async Task FlakyStep_RetriesAndSucceedsOnThirdAttempt()
    {
        await using var deployment = await DurableFunctionDeployment.CreateAsync(
            DurableFunctionDeployment.FindTestFunctionDir("RetryFunction"),
            "retry", _output);

        var (invokeResponse, executionName) = await deployment.InvokeAsync("""{"orderId": "x"}""");
        var responsePayload = Encoding.UTF8.GetString(invokeResponse.Payload.ToArray());
        _output.WriteLine($"Response: {responsePayload}");

        // Initial invoke returns when the SDK suspends after the first failure.
        // The execution continues asynchronously via service-driven re-invokes.
        var arn = await deployment.FindDurableExecutionArnByNameAsync(executionName, TimeSpan.FromSeconds(60));
        Assert.NotNull(arn);

        // Total expected wall time: 2s + 4s of retry delay + execution overhead.
        // Allow generous headroom for service scheduling latency.
        var status = await deployment.PollForCompletionAsync(arn!, TimeSpan.FromSeconds(120));
        Assert.Equal("SUCCEEDED", status, ignoreCase: true);

        var history = await deployment.WaitForHistoryAsync(
            arn!,
            h => (h.Events?.Count(e => e.EventType == EventType.StepStarted) ?? 0) >= 3
              && (h.Events?.Any(e => e.StepSucceededDetails != null) ?? false),
            TimeSpan.FromSeconds(60));
        var events = history.Events ?? new List<Event>();

        // Three attempts ran (attempts 1, 2, 3).
        Assert.Equal(3, events.Count(e => e.EventType == EventType.StepStarted));

        // Two failed attempts recorded retry metadata; the final attempt succeeded.
        Assert.Equal(2, events.Count(e => e.StepFailedDetails != null && e.Name == "flaky_step"));
        var succeeded = events.SingleOrDefault(e => e.StepSucceededDetails != null && e.Name == "flaky_step");
        Assert.NotNull(succeeded);
        Assert.Equal("\"ok on attempt 3\"", succeeded!.StepSucceededDetails.Result?.Payload);

        // The two recorded failure messages reflect the per-attempt exception.
        var failures = events
            .Where(e => e.StepFailedDetails != null && e.Name == "flaky_step")
            .Select(e => e.StepFailedDetails.Error?.Payload?.ErrorMessage ?? string.Empty)
            .ToList();
        Assert.Contains(failures, m => m.Contains("attempt 1"));
        Assert.Contains(failures, m => m.Contains("attempt 2"));

        // Timing check: the service must have actually waited between attempts.
        // With initialDelay=2s, backoffRate=2.0, no jitter: delays are 2s and 4s.
        // The gap between the first and last StepStarted should be >= 6s.
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
