using System.Linq;
using System.Text;
using Amazon.Lambda.Model;
using Xunit;
using Xunit.Abstractions;

namespace Amazon.Lambda.DurableExecution.IntegrationTests;

public class InvokeWithTenantIdTest
{
    private readonly ITestOutputHelper _output;
    public InvokeWithTenantIdTest(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task InvokeAsync_WithTenantId_PropagatesToChainedInvokeOptions()
    {
        var (parent, downstream) = await DurableFunctionDeployment.CreateWithDownstreamAsync(
            parentTestFunctionDir: DurableFunctionDeployment.FindTestFunctionDir("InvokeWithTenantIdFunction"),
            downstreamTestFunctionDir: DurableFunctionDeployment.FindTestFunctionDir("InvokeChildTenantFunction"),
            scenarioSuffix: "invoketenant",
            output: _output,
            // The downstream must be PER_TENANT for the service to accept a
            // chained invoke carrying a TenantId. The parent stays default.
            enableDownstreamTenancy: true);

        await using (downstream)
        await using (parent)
        {
            var (invokeResponse, executionName) = await parent.InvokeAsync("""{"orderId": "tenant-test"}""");
            Assert.Equal(200, invokeResponse.StatusCode);

            var responsePayload = Encoding.UTF8.GetString(invokeResponse.Payload.ToArray());
            _output.WriteLine($"Parent response: {responsePayload}");

            var arn = await parent.FindDurableExecutionArnByNameAsync(executionName, TimeSpan.FromSeconds(60));
            Assert.NotNull(arn);

            var status = await parent.PollForCompletionAsync(arn!, TimeSpan.FromSeconds(120));
            Assert.Equal("SUCCEEDED", status, ignoreCase: true);

            var history = await parent.WaitForHistoryAsync(
                arn!,
                h => h.Events?.Any(e => e.EventType == EventType.ChainedInvokeStarted) ?? false,
                TimeSpan.FromSeconds(60));
            var events = history.Events ?? new List<Event>();

            var started = events.FirstOrDefault(e => e.EventType == EventType.ChainedInvokeStarted);
            Assert.NotNull(started);

            // The tenant ID flows through ChainedInvokeOptions -> service ->
            // ChainedInvokeStartedDetails. This is the load-bearing assertion:
            // it proves the SDK's InvokeConfig.TenantId reaches the wire.
            Assert.Equal("test-tenant", started!.ChainedInvokeStartedDetails.TenantId);

            // The chained call still produced a result — proves nothing in the
            // tenant-routing path silently dropped the invocation.
            var succeeded = events.FirstOrDefault(e => e.ChainedInvokeSucceededDetails != null);
            Assert.NotNull(succeeded);
            var childPayload = succeeded!.ChainedInvokeSucceededDetails.Result?.Payload?.Trim('"');
            Assert.Equal("tenant-aware-7", childPayload);
        }
    }
}
