using System.Linq;
using System.Text;
using Amazon.Lambda.Model;
using Xunit;
using Xunit.Abstractions;

namespace Amazon.Lambda.DurableExecution.IntegrationTests;

public class WaitForConditionReplayDeterminismTest
{
    private readonly ITestOutputHelper _output;
    public WaitForConditionReplayDeterminismTest(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// End-to-end replay-determinism check for a step + wait-for-condition +
    /// step workflow. The wait-for-condition triggers multiple suspend/resume
    /// cycles (one per polling iteration), so the surrounding steps are
    /// replayed multiple times. Verifies that:
    ///   1. The leading step is re-replayed (not re-executed) across all
    ///      iterations — its checkpointed GUID flows through to the trailing
    ///      step regardless of how many polling iterations happen.
    ///   2. The wait-for-condition operation is checkpointed exactly once
    ///      (one StepStarted), with one terminal SUCCEED carrying the final
    ///      counter state.
    ///   3. Multiple invocations were recorded (proves real replay happened).
    /// </summary>
    [Fact]
    public async Task WaitForCondition_ReplayPreservesIdentityAndState()
    {
        await using var deployment = await DurableFunctionDeployment.CreateAsync(
            DurableFunctionDeployment.FindTestFunctionDir("WaitForConditionReplayDeterminismFunction"),
            "wfcrep", _output);

        var (invokeResponse, executionName) = await deployment.InvokeAsync("""{"orderId": "wfc-replay"}""");
        var responsePayload = Encoding.UTF8.GetString(invokeResponse.Payload.ToArray());
        _output.WriteLine($"Response: {responsePayload}");

        var arn = await deployment.FindDurableExecutionArnByNameAsync(executionName, TimeSpan.FromSeconds(60));
        Assert.NotNull(arn);

        // 3 polls with ~2s delay = ~4s of timer + 2 step invocations.
        var status = await deployment.PollForCompletionAsync(arn!, TimeSpan.FromSeconds(120));
        Assert.Equal("SUCCEEDED", status, ignoreCase: true);

        // History is eventually consistent — wait until both step-succeeded
        // AND the polling op-succeeded events are visible.
        var history = await deployment.WaitForHistoryAsync(
            arn!,
            h => (h.Events?.Count(e => e.StepSucceededDetails != null) ?? 0) >= 3,
            TimeSpan.FromSeconds(60));
        var events = history.Events ?? new List<Event>();

        // Each named step / polling op started exactly once. The leading and
        // trailing steps each have one StepStarted; the polling op also has
        // one (sub-iterations replay from RETRY/READY/PENDING and skip START).
        Assert.Single(events.Where(e => e.EventType == EventType.StepStarted && e.Name == "before_poll"));
        Assert.Single(events.Where(e => e.EventType == EventType.StepStarted && e.Name == "after_poll"));
        Assert.Single(events.Where(e => e.EventType == EventType.StepStarted && e.Name == "determinism_poll"));

        // Plain steps SUCCEED exactly once (replay returns cached values).
        // The polling op surfaces one StepSucceeded per iteration (RETRYs +
        // terminal SUCCEED), so we just require >= 1 there.
        var stepSucceededEvents = events.Where(e => e.StepSucceededDetails != null).ToList();
        Assert.Single(stepSucceededEvents.Where(e => e.Name == "before_poll"));
        Assert.Single(stepSucceededEvents.Where(e => e.Name == "after_poll"));
        Assert.NotEmpty(stepSucceededEvents.Where(e => e.Name == "determinism_poll"));

        // Verify the trailing step received the GUID from the leading step
        // verbatim, AND the final counter — proves the cached step value and
        // the WaitForCondition's terminal payload both round-tripped through
        // replay.
        var beforeEvent = stepSucceededEvents.First(e => e.Name == "before_poll");
        var afterEvent = stepSucceededEvents.First(e => e.Name == "after_poll");
        var generatedGuid = beforeEvent.StepSucceededDetails.Result?.Payload?.Trim('"');
        var echoedResult = afterEvent.StepSucceededDetails.Result?.Payload?.Trim('"');
        Assert.NotNull(generatedGuid);
        Assert.NotNull(echoedResult);
        Assert.True(Guid.TryParse(generatedGuid, out _),
            $"before_poll should produce a valid GUID, got: {generatedGuid}");
        Assert.Equal($"echo:{generatedGuid}:3", echoedResult);

        // The wait-for-condition truly drove suspend/resume — one invocation
        // per poll iteration plus one for the final continuation. With 3
        // polls we expect at least 3 InvocationCompleted events.
        var invocations = events.Where(e => e.InvocationCompletedDetails != null).ToList();
        Assert.True(
            invocations.Count >= 3,
            $"Expected at least 3 InvocationCompleted events (one per poll iteration), got {invocations.Count}");
    }
}
