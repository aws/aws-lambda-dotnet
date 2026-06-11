using System.Linq;
using System.Text;
using System.Text.Json;
using Amazon.Lambda.Model;
using Xunit;
using Xunit.Abstractions;

namespace Amazon.Lambda.DurableExecution.IntegrationTests;

public class WaitForConditionHappyPathTest
{
    private readonly ITestOutputHelper _output;
    public WaitForConditionHappyPathTest(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// End-to-end happy-path polling. The check function increments a counter
    /// every iteration; the strategy's isDone predicate fires once the counter
    /// hits 3. Validates that the service honors the RETRY-with-delay pattern,
    /// re-invokes the Lambda for each poll iteration, and that state survives
    /// across re-invocations via the RETRY payload — none of which the unit
    /// tests can prove (they fake state transitions in-memory).
    /// </summary>
    [Fact]
    public async Task WaitForCondition_PollsUntilConditionMet()
    {
        await using var deployment = await DurableFunctionDeployment.CreateAsync(
            DurableFunctionDeployment.FindTestFunctionDir("WaitForConditionHappyPathFunction"),
            "wfchappy", _output);

        var (invokeResponse, executionName) = await deployment.InvokeAsync("""{"orderId": "wfc-happy"}""");
        var responsePayload = Encoding.UTF8.GetString(invokeResponse.Payload.ToArray());
        _output.WriteLine($"Response: {responsePayload}");

        var arn = await deployment.FindDurableExecutionArnByNameAsync(executionName, TimeSpan.FromSeconds(60));
        Assert.NotNull(arn);

        // Total expected wall time: 3 attempts with ~2s delay between them =
        // ~4s of timer + execution overhead. Allow generous headroom.
        var status = await deployment.PollForCompletionAsync(arn!, TimeSpan.FromSeconds(120));
        Assert.Equal("SUCCEEDED", status, ignoreCase: true);

        var history = await deployment.WaitForHistoryAsync(
            arn!,
            h => (h.Events?.Any(e => e.StepSucceededDetails != null) ?? false),
            TimeSpan.FromSeconds(60));
        var events = history.Events ?? new List<Event>();

        // Exactly one START emitted on the first iteration (subsequent
        // iterations resume from a RETRY checkpoint and skip START).
        Assert.Equal(1, events.Count(e => e.EventType == EventType.StepStarted && e.Name == "happy_poll"));

        // Each polling iteration surfaces as a StepSucceeded event (one per
        // RETRY plus one for the terminal SUCCEED). The last one carries the
        // terminal state.
        var succeededEvents = events.Where(e => e.StepSucceededDetails != null && e.Name == "happy_poll").ToList();
        Assert.NotEmpty(succeededEvents);
        var succeeded = succeededEvents.Last();

        var finalPayload = succeeded.StepSucceededDetails.Result?.Payload;
        Assert.False(string.IsNullOrEmpty(finalPayload),
            "final SUCCEED payload should carry the terminal state");

        using var doc = JsonDocument.Parse(finalPayload!);
        Assert.Equal(3, doc.RootElement.GetProperty("Counter").GetInt32());
        Assert.Equal(3, doc.RootElement.GetProperty("AttemptNumber").GetInt32());

        // The polling actually caused suspend/resume cycles — at least one
        // invocation per iteration (3 polls = 3+ invocations).
        var invocations = events.Where(e => e.InvocationCompletedDetails != null).ToList();
        Assert.True(
            invocations.Count >= 3,
            $"Expected at least 3 InvocationCompleted events (one per poll iteration), got {invocations.Count}");
    }
}
