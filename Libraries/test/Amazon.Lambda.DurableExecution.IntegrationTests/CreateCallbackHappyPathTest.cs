using System.IO;
using System.Linq;
using System.Text;
using Amazon.Lambda.Model;
using Xunit;
using Xunit.Abstractions;

namespace Amazon.Lambda.DurableExecution.IntegrationTests;

public class CreateCallbackHappyPathTest
{
    private readonly ITestOutputHelper _output;
    public CreateCallbackHappyPathTest(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// End-to-end happy path for <c>CreateCallbackAsync</c>:
    /// the workflow suspends inside <c>GetResultAsync</c>; the test acts as the
    /// external system and delivers a result via <c>SendDurableExecutionCallbackSuccess</c>;
    /// the workflow resumes and returns the delivered payload.
    /// </summary>
    [Fact]
    public async Task CreateCallback_DeliversResultViaSendSuccess()
    {
        await using var deployment = await DurableFunctionDeployment.CreateAsync(
            DurableFunctionDeployment.FindTestFunctionDir("CreateCallbackHappyPathFunction"),
            "cb-happy", _output);

        var (invokeResponse, executionName) = await deployment.InvokeAsync("""{"orderId": "approve-123"}""");
        var responsePayload = Encoding.UTF8.GetString(invokeResponse.Payload.ToArray());
        _output.WriteLine($"Initial response: {responsePayload}");

        // The workflow suspends after CreateCallback's START checkpoint; locate the
        // execution by name and pull the service-allocated CallbackId from history.
        var arn = await deployment.FindDurableExecutionArnByNameAsync(executionName, TimeSpan.FromSeconds(60));
        Assert.NotNull(arn);

        var callbackId = await WaitForCallbackIdAsync(deployment, arn!, TimeSpan.FromSeconds(60));
        Assert.False(string.IsNullOrEmpty(callbackId), "CallbackStarted event never appeared with a CallbackId");
        _output.WriteLine($"Service-allocated CallbackId: {callbackId}");

        // Act as the external system: deliver a result. The service will re-invoke the
        // Lambda with CALLBACK SUCCEEDED, GetResultAsync deserializes it, and the
        // workflow returns.
        var resultJson = """{"Status":"approved","ApprovedBy":"integ-test"}""";
        await deployment.LambdaClient.SendDurableExecutionCallbackSuccessAsync(
            new SendDurableExecutionCallbackSuccessRequest
            {
                CallbackId = callbackId!,
                Result = new MemoryStream(Encoding.UTF8.GetBytes(resultJson))
            });

        var status = await deployment.PollForCompletionAsync(arn!, TimeSpan.FromSeconds(120));
        Assert.Equal("SUCCEEDED", status, ignoreCase: true);

        // The execution result mirrors the payload we sent — proves GetResultAsync
        // deserialized the wire-level callback Result and the workflow returned it.
        var execution = await deployment.GetExecutionAsync(arn!);
        Assert.NotNull(execution.Result);
        Assert.Contains("approved", execution.Result);
        Assert.Contains("integ-test", execution.Result);

        // History shows the canonical callback lifecycle: Started then Succeeded.
        var history = await deployment.WaitForHistoryAsync(
            arn!,
            h => (h.Events?.Any(e => e.EventType == EventType.CallbackStarted) ?? false)
              && (h.Events?.Any(e => e.EventType == EventType.CallbackSucceeded) ?? false),
            TimeSpan.FromSeconds(60));
        var events = history.Events ?? new List<Event>();

        Assert.Single(events.Where(e => e.EventType == EventType.CallbackStarted));
        Assert.Single(events.Where(e => e.EventType == EventType.CallbackSucceeded));

        var succeeded = events.First(e => e.CallbackSucceededDetails != null);
        Assert.Equal("approve", succeeded.Name);
    }

    /// <summary>
    /// Polls execution history until a <c>CallbackStarted</c> event surfaces a
    /// <c>CallbackId</c>. The history endpoint is eventually consistent and the
    /// callback ID isn't allocated until the service processes the START checkpoint.
    /// </summary>
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
