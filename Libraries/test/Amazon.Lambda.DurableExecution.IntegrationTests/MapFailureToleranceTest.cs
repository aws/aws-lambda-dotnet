using System.Linq;
using System.Text;
using Amazon.Lambda.Model;
using Xunit;
using Xunit.Abstractions;

namespace Amazon.Lambda.DurableExecution.IntegrationTests;

public class MapFailureToleranceTest
{
    private readonly ITestOutputHelper _output;
    public MapFailureToleranceTest(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// Five items, two fail, ToleratedFailureCount=1. The map must surface a
    /// <see cref="MapException"/> with reason
    /// <see cref="CompletionReason.FailureToleranceExceeded"/>; the workflow must
    /// terminate FAILED. Validates the failure-tolerance short-circuit and that
    /// <c>MapException</c> (not <c>ParallelException</c>) propagates as the
    /// workflow's terminal error.
    /// </summary>
    [Fact]
    public async Task Map_FailureToleranceExceeded_FailsWorkflow()
    {
        await using var deployment = await DurableFunctionDeployment.CreateAsync(
            DurableFunctionDeployment.FindTestFunctionDir("MapFailureToleranceFunction"),
            "mtol", _output);

        var (invokeResponse, executionName) = await deployment.InvokeAsync("""{"orderId": "m3"}""");
        var responsePayload = Encoding.UTF8.GetString(invokeResponse.Payload.ToArray());
        _output.WriteLine($"Response: {responsePayload}");

        // Failed workflows return null payload to the Invoke caller — locate the
        // execution by name to inspect its terminal status.
        var arn = await deployment.FindDurableExecutionArnByNameAsync(executionName, TimeSpan.FromSeconds(60));
        Assert.NotNull(arn);

        var status = await deployment.PollForCompletionAsync(arn!, TimeSpan.FromSeconds(60));
        Assert.Equal("FAILED", status, ignoreCase: true);

        var execution = await deployment.GetExecutionAsync(arn!);
        Assert.NotNull(execution.Error);
        // MapException is the terminal error type the SDK throws when the
        // failure-tolerance short-circuit fires.
        var errorType = execution.Error.ErrorType ?? string.Empty;
        var errorMessage = execution.Error.ErrorMessage ?? string.Empty;
        Assert.True(
            errorType.Contains("MapException", StringComparison.Ordinal)
                || errorMessage.Contains("Map", StringComparison.OrdinalIgnoreCase),
            $"Expected error to indicate MapException; got type='{errorType}' message='{errorMessage}'");

        // History: parent CONTEXT and at least 2 failed item contexts visible.
        var history = await deployment.WaitForHistoryAsync(
            arn!,
            h => (h.Events?.Count(e => e.EventType == EventType.ContextStarted) ?? 0) >= 3
              && (h.Events?.Count(e => e.EventType == EventType.ContextFailed) ?? 0) >= 2,
            TimeSpan.FromSeconds(60));
        var events = history.Events ?? new List<Event>();

        Assert.True(
            events.Count(e => e.EventType == EventType.ContextFailed) >= 2,
            $"Expected >= 2 ContextFailed events; got {events.Count(e => e.EventType == EventType.ContextFailed)}");

        // The parent context (named "tolerance") is checkpointed ContextSucceeded
        // even when the failure tolerance is exceeded: ConcurrentOperation always
        // writes the parent batch summary with action SUCCEED (the completion
        // reason lives inside the payload), then the SDK throws MapException
        // AFTER the checkpoint. This matches the Python/JS/Java wire format. The
        // workflow-level failure is asserted above via PollForCompletionAsync ==
        // FAILED and the MapException error type — the parent CONTEXT itself is
        // NOT recorded as ContextFailed.
        var parentSucceeded = events.FirstOrDefault(e =>
            e.EventType == EventType.ContextSucceeded && e.Name == "tolerance");
        Assert.NotNull(parentSucceeded);
        Assert.DoesNotContain(events, e =>
            e.EventType == EventType.ContextFailed && e.Name == "tolerance");
    }
}
