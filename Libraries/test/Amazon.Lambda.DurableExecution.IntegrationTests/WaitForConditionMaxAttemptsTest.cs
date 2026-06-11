using System.Linq;
using System.Text;
using System.Text.Json;
using Amazon.Lambda.Model;
using Xunit;
using Xunit.Abstractions;

namespace Amazon.Lambda.DurableExecution.IntegrationTests;

public class WaitForConditionMaxAttemptsTest
{
    private readonly ITestOutputHelper _output;
    public WaitForConditionMaxAttemptsTest(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// Validates that when the strategy's max-attempts limit is reached
    /// without isDone being satisfied, the operation throws
    /// <see cref="WaitForConditionException"/> with the correct
    /// <c>AttemptsExhausted</c> count, and the FAILED checkpoint records the
    /// exception type. The workflow catches the exception and returns the
    /// count, so we expect the workflow itself to SUCCEED.
    /// </summary>
    [Fact]
    public async Task WaitForCondition_MaxAttemptsExhausted_ThrowsWithCount()
    {
        await using var deployment = await DurableFunctionDeployment.CreateAsync(
            DurableFunctionDeployment.FindTestFunctionDir("WaitForConditionMaxAttemptsFunction"),
            "wfcmax", _output);

        var (invokeResponse, executionName) = await deployment.InvokeAsync("""{"orderId": "wfc-max"}""");
        var responsePayload = Encoding.UTF8.GetString(invokeResponse.Payload.ToArray());
        _output.WriteLine($"Response: {responsePayload}");

        var arn = await deployment.FindDurableExecutionArnByNameAsync(executionName, TimeSpan.FromSeconds(60));
        Assert.NotNull(arn);

        // 3 attempts at ~1s delay between them = ~2s of timer + execution
        // overhead. Allow generous headroom.
        var status = await deployment.PollForCompletionAsync(arn!, TimeSpan.FromSeconds(120));
        Assert.Equal("SUCCEEDED", status, ignoreCase: true);

        // The workflow caught the WaitForConditionException and returned a
        // result containing AttemptsExhausted. Verify the final payload from
        // the workflow itself (parsed from the GetExecution response).
        var execution = await deployment.GetExecutionAsync(arn!);
        var resultPayload = execution.Result;
        Assert.False(string.IsNullOrEmpty(resultPayload),
            "workflow result payload should be present");

        using var doc = JsonDocument.Parse(resultPayload!);
        Assert.Equal("exhausted", doc.RootElement.GetProperty("Status").GetString());
        // The exact attempts count is 3 — strategy maxAttempts.
        Assert.Equal(3, doc.RootElement.GetProperty("AttemptsExhausted").GetInt32());

        // Verify the operation itself was checkpointed as FAILED with the
        // WaitForConditionException type, even though the workflow recovers.
        var history = await deployment.WaitForHistoryAsync(
            arn!,
            h => (h.Events?.Any(e => e.StepFailedDetails != null && e.Name == "exhausting_poll") ?? false),
            TimeSpan.FromSeconds(60));
        var events = history.Events ?? new List<Event>();

        var stepFailed = events.FirstOrDefault(e => e.StepFailedDetails != null && e.Name == "exhausting_poll");
        Assert.NotNull(stepFailed);
        Assert.Contains("WaitForConditionException",
            stepFailed!.StepFailedDetails.Error?.Payload?.ErrorType ?? string.Empty);
    }
}
