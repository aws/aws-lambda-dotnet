using System.Linq;
using System.Text;
using Amazon.Lambda.Model;
using Xunit;
using Xunit.Abstractions;

namespace Amazon.Lambda.DurableExecution.IntegrationTests;

public class CallbackFailedTest
{
    private readonly ITestOutputHelper _output;
    public CallbackFailedTest(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// End-to-end failure path for <c>CreateCallbackAsync</c>:
    /// the test acts as the external system and reports a failure via
    /// <c>SendDurableExecutionCallbackFailure</c>. The SDK should raise
    /// <see cref="CallbackFailedException"/> from <c>GetResultAsync</c>, and the
    /// workflow surfaces FAILED with that exception type recorded.
    /// </summary>
    [Fact]
    public async Task CallbackFailed_SurfacesAsCallbackFailedException()
    {
        await using var deployment = await DurableFunctionDeployment.CreateAsync(
            DurableFunctionDeployment.FindTestFunctionDir("CallbackFailedFunction"),
            "cb-failed", _output);

        var (invokeResponse, executionName) = await deployment.InvokeAsync("""{"orderId": "x"}""");
        var responsePayload = Encoding.UTF8.GetString(invokeResponse.Payload.ToArray());
        _output.WriteLine($"Initial response: {responsePayload}");

        var arn = await deployment.FindDurableExecutionArnByNameAsync(executionName, TimeSpan.FromSeconds(60));
        Assert.NotNull(arn);

        var callbackId = await WaitForCallbackIdAsync(deployment, arn!, TimeSpan.FromSeconds(60));
        Assert.False(string.IsNullOrEmpty(callbackId));
        _output.WriteLine($"Service-allocated CallbackId: {callbackId}");

        // Act as the external system: report a failure.
        await deployment.LambdaClient.SendDurableExecutionCallbackFailureAsync(
            new SendDurableExecutionCallbackFailureRequest
            {
                CallbackId = callbackId!,
                Error = new Amazon.Lambda.Model.ErrorObject
                {
                    ErrorType = "ApprovalRejected",
                    ErrorMessage = "external system rejected the request",
                }
            });

        var status = await deployment.PollForCompletionAsync(arn!, TimeSpan.FromSeconds(120));
        Assert.Equal("FAILED", status, ignoreCase: true);

        // The workflow's surfaced exception is CallbackFailedException — the SDK
        // wraps the external error message into the exception's Message. Verify
        // the recorded error type is the SDK's CallbackFailedException and that
        // the original failure message survives.
        var execution = await deployment.GetExecutionAsync(arn!);
        Assert.NotNull(execution.Error);
        Assert.Equal(typeof(CallbackFailedException).FullName, execution.Error.ErrorType);
        Assert.Contains("rejected", execution.Error.ErrorMessage);

        // History records both Started and Failed for the same callback.
        var history = await deployment.WaitForHistoryAsync(
            arn!,
            h => (h.Events?.Any(e => e.EventType == EventType.CallbackStarted) ?? false)
              && (h.Events?.Any(e => e.EventType == EventType.CallbackFailed) ?? false),
            TimeSpan.FromSeconds(60));
        var events = history.Events ?? new List<Event>();
        Assert.Single(events.Where(e => e.EventType == EventType.CallbackStarted));
        Assert.Single(events.Where(e => e.EventType == EventType.CallbackFailed));
    }

    private static async Task<string?> WaitForCallbackIdAsync(
        DurableFunctionDeployment deployment, string arn, TimeSpan timeout)
    {
        var history = await deployment.WaitForHistoryAsync(
            arn,
            h => h.Events?.Any(e =>
                e.CallbackStartedDetails != null
                && !string.IsNullOrEmpty(e.CallbackStartedDetails.CallbackId)) ?? false,
            timeout);
        return history.Events?
            .Where(e => e.CallbackStartedDetails != null
                     && !string.IsNullOrEmpty(e.CallbackStartedDetails.CallbackId))
            .Select(e => e.CallbackStartedDetails.CallbackId)
            .FirstOrDefault();
    }
}
