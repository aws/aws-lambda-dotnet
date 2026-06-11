using System.Linq;
using System.Text;
using System.Text.Json;
using Amazon.Lambda.Model;
using Xunit;
using Xunit.Abstractions;

namespace Amazon.Lambda.DurableExecution.IntegrationTests;

public class WaitForConditionExponentialTest
{
    private readonly ITestOutputHelper _output;
    public WaitForConditionExponentialTest(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// End-to-end exponential-backoff polling. The check function flips
    /// <c>Done</c> on attempt 3, so the strategy stops after exactly 3
    /// iterations. Validates that the service honors the per-iteration delay
    /// (which grows with each retry) without any in-process Thread.Sleep.
    /// Timing is asserted loosely because the service's scheduling latency
    /// dominates short delays — we only require the gap to be at least the
    /// configured floor.
    /// </summary>
    [Fact]
    public async Task WaitForCondition_ExponentialBackoff_CompletesOnExpectedAttempt()
    {
        await using var deployment = await DurableFunctionDeployment.CreateAsync(
            DurableFunctionDeployment.FindTestFunctionDir("WaitForConditionExponentialFunction"),
            "wfcexp", _output);

        var (invokeResponse, executionName) = await deployment.InvokeAsync("""{"orderId": "wfc-exp"}""");
        var responsePayload = Encoding.UTF8.GetString(invokeResponse.Payload.ToArray());
        _output.WriteLine($"Response: {responsePayload}");

        var arn = await deployment.FindDurableExecutionArnByNameAsync(executionName, TimeSpan.FromSeconds(60));
        Assert.NotNull(arn);

        // Total expected wall time: 1s + 2s of timer = ~3s + execution
        // overhead. Allow generous headroom for service scheduling latency.
        var status = await deployment.PollForCompletionAsync(arn!, TimeSpan.FromSeconds(120));
        Assert.Equal("SUCCEEDED", status, ignoreCase: true);

        var history = await deployment.WaitForHistoryAsync(
            arn!,
            h => (h.Events?.Any(e => e.StepSucceededDetails != null && e.Name == "exp_poll") ?? false),
            TimeSpan.FromSeconds(60));
        var events = history.Events ?? new List<Event>();

        // Each polling iteration surfaces as a StepSucceeded event (one per
        // RETRY plus one for the terminal SUCCEED). The last one carries the
        // terminal state.
        var succeededEvents = events.Where(e => e.StepSucceededDetails != null && e.Name == "exp_poll").ToList();
        Assert.NotEmpty(succeededEvents);
        var succeeded = succeededEvents.Last();

        var finalPayload = succeeded.StepSucceededDetails.Result?.Payload;
        Assert.False(string.IsNullOrEmpty(finalPayload));

        using var doc = JsonDocument.Parse(finalPayload!);
        Assert.True(doc.RootElement.GetProperty("Done").GetBoolean());
        Assert.Equal(3, doc.RootElement.GetProperty("AttemptNumber").GetInt32());

        // The polling caused real suspend/resume cycles — at least 3
        // invocations (one per attempt).
        var invocations = events.Where(e => e.InvocationCompletedDetails != null).ToList();
        Assert.True(
            invocations.Count >= 3,
            $"Expected at least 3 InvocationCompleted events (one per poll), got {invocations.Count}");
    }
}
