using System.Linq;
using System.Text;
using Amazon.Lambda.Model;
using Xunit;
using Xunit.Abstractions;

namespace Amazon.Lambda.DurableExecution.IntegrationTests;

public class LongRetryChainTest
{
    private readonly ITestOutputHelper _output;
    public LongRetryChainTest(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// Long retry chain across many invocations: step fails 5 times before
    /// succeeding on attempt 6. Validates that StepDetails.Attempt increments
    /// monotonically across invocations (no off-by-one, no skipped attempts)
    /// and that IStepContext.AttemptNumber on the user side matches the wire
    /// value on each attempt.
    /// </summary>
    [Fact]
    public async Task FailsFiveTimesThenSucceeds_AttemptCounterIsMonotonic()
    {
        await using var deployment = await DurableFunctionDeployment.CreateAsync(
            DurableFunctionDeployment.FindTestFunctionDir("LongRetryChainFunction"),
            "longretry", _output);

        var (invokeResponse, executionName) = await deployment.InvokeAsync("""{"orderId": "x"}""");
        var responsePayload = Encoding.UTF8.GetString(invokeResponse.Payload.ToArray());
        _output.WriteLine($"Response: {responsePayload}");

        var arn = await deployment.FindDurableExecutionArnByNameAsync(executionName, TimeSpan.FromSeconds(60));
        Assert.NotNull(arn);

        // Total retry delay budget: 1+2+3+4+5 = 15s. Allow generous headroom.
        var status = await deployment.PollForCompletionAsync(arn!, TimeSpan.FromSeconds(180));
        Assert.Equal("SUCCEEDED", status, ignoreCase: true);

        var history = await deployment.WaitForHistoryAsync(
            arn!,
            h => (h.Events?.Count(e => e.EventType == EventType.StepStarted) ?? 0) >= 6
              && (h.Events?.Any(e => e.StepSucceededDetails != null) ?? false),
            TimeSpan.FromSeconds(60));
        var events = history.Events ?? new List<Event>();

        // Six attempts total: five failures + one success.
        Assert.Equal(6, events.Count(e => e.EventType == EventType.StepStarted));
        Assert.Equal(5, events.Count(e => e.StepFailedDetails != null && e.Name == "long_retry_step"));
        var succeeded = events.SingleOrDefault(e => e.StepSucceededDetails != null && e.Name == "long_retry_step");
        Assert.NotNull(succeeded);

        // The user-facing AttemptNumber on the final (winning) attempt was 6 —
        // proves IStepContext.AttemptNumber tracks the wire attempt counter
        // across invocations, not just within a single invocation.
        Assert.Equal("\"ok on attempt 6\"", succeeded!.StepSucceededDetails.Result?.Payload);

        // Each failure carries a unique per-attempt message — confirms the user-side
        // counter incremented exactly once per invocation, no duplicates or skips.
        var failureMessages = events
            .Where(e => e.StepFailedDetails != null && e.Name == "long_retry_step")
            .Select(e => e.StepFailedDetails.Error?.Payload?.ErrorMessage ?? string.Empty)
            .ToList();
        Assert.Equal(5, failureMessages.Count);
        for (int i = 1; i <= 5; i++)
        {
            Assert.Contains(failureMessages, m => m.Contains($"attempt {i}"));
        }

        // The chain was executed across multiple invocations (proves the
        // service actually re-invoked us between retries instead of holding
        // a single Lambda alive through all six attempts).
        var invocations = events.Where(e => e.InvocationCompletedDetails != null).ToList();
        Assert.True(
            invocations.Count >= 5,
            $"Expected at least 5 InvocationCompleted events (one per retry boundary), got {invocations.Count}");
    }
}
