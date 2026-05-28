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
    /// the workflow suspends inside <c>GetResultAsync</c>; a paired
    /// <c>ApproverFunction</c> Lambda (Event-invoked from the workflow) acts
    /// as the external system and delivers a result via
    /// <c>SendDurableExecutionCallbackSuccess</c>; the workflow resumes and
    /// returns the delivered payload.
    /// </summary>
    /// <remarks>
    /// The callback delivery has to come from a separate Lambda — not from the
    /// test process — because the test's synchronous <c>InvokeAsync</c> blocks
    /// until the durable execution reaches a terminal state. If the test tried
    /// to deliver the callback itself, it would deadlock against its own
    /// blocked Invoke.
    /// </remarks>
    [Fact]
    public async Task CreateCallback_DeliversResultViaSendSuccess()
    {
        await using var deployment = await DurableFunctionDeployment.CreateAsync(
            DurableFunctionDeployment.FindTestFunctionDir("CreateCallbackHappyPathFunction"),
            "cb-happy", _output,
            externalFunctionDir: DurableFunctionDeployment.FindTestFunctionDir("ApproverFunction"));

        var (invokeResponse, executionName) = await deployment.InvokeAsync("""{"orderId":"integ-test"}""");
        var responsePayload = Encoding.UTF8.GetString(invokeResponse.Payload.ToArray());
        _output.WriteLine($"Initial response: {responsePayload}");

        var arn = await deployment.FindDurableExecutionArnByNameAsync(executionName, TimeSpan.FromSeconds(60));
        Assert.NotNull(arn);

        var status = await deployment.PollForCompletionAsync(arn!, TimeSpan.FromSeconds(120));
        Assert.Equal("SUCCEEDED", status, ignoreCase: true);

        // The execution result mirrors the payload the approver sent — proves
        // GetResultAsync deserialized the wire-level callback Result and the
        // workflow returned it.
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
}
