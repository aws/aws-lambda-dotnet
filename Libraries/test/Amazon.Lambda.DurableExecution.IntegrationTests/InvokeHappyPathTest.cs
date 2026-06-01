using System.Linq;
using System.Text;
using Amazon.Lambda.Model;
using Xunit;
using Xunit.Abstractions;

namespace Amazon.Lambda.DurableExecution.IntegrationTests;

public class InvokeHappyPathTest
{
    private readonly ITestOutputHelper _output;
    public InvokeHappyPathTest(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task InvokeAsync_HappyPath_ChildResultPropagatesToParent()
    {
        var (parent, downstream) = await DurableFunctionDeployment.CreateWithDownstreamAsync(
            parentTestFunctionDir: DurableFunctionDeployment.FindTestFunctionDir("InvokeHappyPathParentFunction"),
            downstreamTestFunctionDir: DurableFunctionDeployment.FindTestFunctionDir("InvokeHappyPathChildFunction"),
            scenarioSuffix: "invokehappy",
            output: _output);

        await using (downstream)
        await using (parent)
        {
            var (invokeResponse, executionName) = await parent.InvokeAsync("""{"orderId": "invoke-happy"}""");
            Assert.Equal(200, invokeResponse.StatusCode);

            var responsePayload = Encoding.UTF8.GetString(invokeResponse.Payload.ToArray());
            _output.WriteLine($"Parent response: {responsePayload}");

            // Locate the parent execution and wait for terminal status. Chained
            // invoke suspends the parent — the synchronous Invoke response
            // carries no data — so we drive completion via the listing API.
            var arn = await parent.FindDurableExecutionArnByNameAsync(executionName, TimeSpan.FromSeconds(60));
            Assert.NotNull(arn);

            var status = await parent.PollForCompletionAsync(arn!, TimeSpan.FromSeconds(120));
            Assert.Equal("SUCCEEDED", status, ignoreCase: true);

            // The chained invoke's result surfaces in the parent's history as a
            // ChainedInvokeSucceeded event. The parent then returns that result
            // verbatim from its workflow.
            var history = await parent.WaitForHistoryAsync(
                arn!,
                h => (h.Events?.Any(e => e.EventType == EventType.ChainedInvokeStarted) ?? false)
                  && (h.Events?.Any(e => e.ChainedInvokeSucceededDetails != null) ?? false),
                TimeSpan.FromSeconds(60));
            var events = history.Events ?? new List<Event>();

            var started = events.FirstOrDefault(e => e.EventType == EventType.ChainedInvokeStarted);
            Assert.NotNull(started);
            Assert.Equal(downstream.FunctionArn + ":$LATEST", started!.ChainedInvokeStartedDetails.FunctionName);

            var succeeded = events.FirstOrDefault(e => e.ChainedInvokeSucceededDetails != null);
            Assert.NotNull(succeeded);
            // The child returned the JSON-encoded string "got-42".
            var childPayload = succeeded!.ChainedInvokeSucceededDetails.Result?.Payload?.Trim('"');
            Assert.Equal("got-42", childPayload);

            // The chained invoke event names what was invoked; cross-check against
            // the deployed downstream's name so we know the parent really called
            // the function we wired in.
            Assert.Equal("call_child", succeeded.Name);
        }
    }
}
