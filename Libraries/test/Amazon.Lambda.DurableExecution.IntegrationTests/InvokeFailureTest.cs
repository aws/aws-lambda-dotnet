using System.Linq;
using System.Text;
using Amazon.Lambda.Model;
using Xunit;
using Xunit.Abstractions;

namespace Amazon.Lambda.DurableExecution.IntegrationTests;

public class InvokeFailureTest
{
    private readonly ITestOutputHelper _output;
    public InvokeFailureTest(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task InvokeAsync_ChildThrows_ParentSurfacesInvokeFailedException()
    {
        var (parent, downstream) = await DurableFunctionDeployment.CreateWithDownstreamAsync(
            parentTestFunctionDir: DurableFunctionDeployment.FindTestFunctionDir("InvokeFailureParentFunction"),
            downstreamTestFunctionDir: DurableFunctionDeployment.FindTestFunctionDir("InvokeFailureChildFunction"),
            scenarioSuffix: "invokefail",
            output: _output);

        await using (downstream)
        await using (parent)
        {
            var (invokeResponse, executionName) = await parent.InvokeAsync("""{"orderId": "invoke-fail"}""");
            var responsePayload = Encoding.UTF8.GetString(invokeResponse.Payload.ToArray());
            _output.WriteLine($"Parent response: {responsePayload}");

            var arn = await parent.FindDurableExecutionArnByNameAsync(executionName, TimeSpan.FromSeconds(60));
            Assert.NotNull(arn);

            // The parent catches InvokeFailedException and returns normally —
            // the parent execution itself SUCCEEDS even though the chained
            // invocation FAILED. This is the value of the SDK's exception
            // surface: failure is observable but not necessarily fatal.
            var status = await parent.PollForCompletionAsync(arn!, TimeSpan.FromSeconds(120));
            Assert.Equal("SUCCEEDED", status, ignoreCase: true);

            var history = await parent.WaitForHistoryAsync(
                arn!,
                h => (h.Events?.Any(e => e.EventType == EventType.ChainedInvokeStarted) ?? false)
                  && (h.Events?.Any(e => e.ChainedInvokeFailedDetails != null) ?? false),
                TimeSpan.FromSeconds(60));
            var events = history.Events ?? new List<Event>();

            // Exactly one chained invoke was issued and it FAILED — the parent
            // did not retry the invoke (no retry semantics for InvokeAsync yet).
            Assert.Equal(1, events.Count(e => e.EventType == EventType.ChainedInvokeStarted));
            var failed = events.FirstOrDefault(e => e.ChainedInvokeFailedDetails != null);
            Assert.NotNull(failed);
            Assert.Equal("call_failing_child", failed!.Name);

            var error = failed.ChainedInvokeFailedDetails.Error?.Payload;
            Assert.NotNull(error);
            // The child's exception type and message propagate through the
            // service into the parent's history. Some service implementations
            // record only the simple type name and others the fully-qualified
            // one — match either by checking for the substring.
            Assert.Contains("InvalidOperationException", error!.ErrorType ?? string.Empty);
            Assert.Contains("intentional child failure", error.ErrorMessage ?? string.Empty);

            // The parent's terminal result encodes "parent-saw-<errorType>" — confirms
            // the parent's catch block ran AND the exception's ErrorType field
            // was populated by the SDK on resume from the FAILED chained invoke.
            // Without the Result assertions, a regression that left ErrorType
            // null would still produce a SUCCEEDED execution (parent-saw-unknown)
            // and silently pass.
            var execution = await parent.GetExecutionAsync(arn!);
            Assert.Null(execution.Error);
            Assert.NotNull(execution.Result);
            Assert.Contains("parent-saw-", execution.Result);
            Assert.DoesNotContain("parent-saw-unknown", execution.Result);
            Assert.Contains("InvalidOperationException", execution.Result);
        }
    }
}
