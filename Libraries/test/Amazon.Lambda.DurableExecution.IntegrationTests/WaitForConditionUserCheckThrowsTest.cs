using System.Linq;
using System.Text;
using System.Text.Json;
using Amazon.Lambda.Model;
using Xunit;
using Xunit.Abstractions;

namespace Amazon.Lambda.DurableExecution.IntegrationTests;

public class WaitForConditionUserCheckThrowsTest
{
    private readonly ITestOutputHelper _output;
    public WaitForConditionUserCheckThrowsTest(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// Validates the user-check-throws path: when the check function throws
    /// on a polling iteration, the operation checkpoints FAIL with the
    /// original exception type and the SDK surfaces a <see cref="StepException"/>
    /// carrying that <c>ErrorType</c>. Mirrors the unit test
    /// <c>WaitForConditionOperationTests.CheckThrows_CheckpointsFailAndThrows</c>.
    /// </summary>
    [Fact]
    public async Task WaitForCondition_UserCheckThrows_SurfacesAsStepException()
    {
        await using var deployment = await DurableFunctionDeployment.CreateAsync(
            DurableFunctionDeployment.FindTestFunctionDir("WaitForConditionUserCheckThrowsFunction"),
            "wfcthrow", _output);

        var (invokeResponse, executionName) = await deployment.InvokeAsync("""{"orderId": "wfc-throw"}""");
        var responsePayload = Encoding.UTF8.GetString(invokeResponse.Payload.ToArray());
        _output.WriteLine($"Response: {responsePayload}");

        var arn = await deployment.FindDurableExecutionArnByNameAsync(executionName, TimeSpan.FromSeconds(60));
        Assert.NotNull(arn);

        // Attempt 1 succeeds (returns state+1=1), strategy schedules ~1s
        // delay, then attempt 2 throws. ~2s of timer + execution overhead.
        var status = await deployment.PollForCompletionAsync(arn!, TimeSpan.FromSeconds(120));
        Assert.Equal("SUCCEEDED", status, ignoreCase: true);

        // The workflow caught the StepException. Verify it captured the
        // expected error type via the workflow's returned payload.
        var execution = await deployment.GetExecutionAsync(arn!);
        var resultPayload = execution.Result;
        Assert.False(string.IsNullOrEmpty(resultPayload),
            "workflow result payload should be present");

        using var doc = JsonDocument.Parse(resultPayload!);
        Assert.Equal("caught_step_exception", doc.RootElement.GetProperty("Status").GetString());
        Assert.Equal("System.InvalidOperationException",
            doc.RootElement.GetProperty("ErrorType").GetString());
        Assert.Contains("intentional check failure",
            doc.RootElement.GetProperty("ErrorMessage").GetString() ?? string.Empty);

        // Verify the polling op itself was checkpointed as FAILED with the
        // original exception type (NOT WaitForConditionException — that's
        // reserved for max-attempts exhaustion).
        var history = await deployment.WaitForHistoryAsync(
            arn!,
            h => (h.Events?.Any(e => e.StepFailedDetails != null && e.Name == "throwing_poll") ?? false),
            TimeSpan.FromSeconds(60));
        var events = history.Events ?? new List<Event>();

        var stepFailed = events.FirstOrDefault(e => e.StepFailedDetails != null && e.Name == "throwing_poll");
        Assert.NotNull(stepFailed);
        Assert.Equal("System.InvalidOperationException",
            stepFailed!.StepFailedDetails.Error?.Payload?.ErrorType);
        Assert.Contains("intentional check failure",
            stepFailed.StepFailedDetails.Error?.Payload?.ErrorMessage ?? string.Empty);
    }
}
