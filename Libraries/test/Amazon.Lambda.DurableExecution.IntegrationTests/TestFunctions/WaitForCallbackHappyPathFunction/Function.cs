using System.Text;
using Amazon.Lambda;
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.Model;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace DurableExecutionTestFunction;

public class Function
{
    // Reuse a single Lambda client across submitter invocations.
    private static readonly IAmazonLambda LambdaClient = new AmazonLambdaClient();

    public static async Task Main(string[] args)
    {
        var handler = new Function();
        var serializer = new DefaultLambdaJsonSerializer();
        using var handlerWrapper = HandlerWrapper.GetHandlerWrapper<DurableExecutionInvocationInput, DurableExecutionInvocationOutput>(handler.Handler, serializer);
        using var bootstrap = new LambdaBootstrap(handlerWrapper);
        await bootstrap.RunAsync();
    }

    public Task<DurableExecutionInvocationOutput> Handler(
        DurableExecutionInvocationInput input, ILambdaContext context)
        => DurableFunction.WrapAsync<TestEvent, MyResult>(Workflow, input, context);

    private async Task<MyResult> Workflow(TestEvent input, IDurableContext context)
    {
        // The submitter is called once with a freshly-allocated callback ID.
        // It hands that ID off to the paired ApproverFunction (Event invocation —
        // fire-and-forget, modelling a real external system). The submitter
        // returns immediately, the SDK suspends, and the approver eventually
        // calls SendDurableExecutionCallbackSuccess to resolve the workflow
        // out-of-band.
        var externalFunctionName = System.Environment.GetEnvironmentVariable("EXTERNAL_FUNCTION_NAME")
            ?? throw new InvalidOperationException("EXTERNAL_FUNCTION_NAME env var not set");

        var result = await context.WaitForCallbackAsync<MyResult>(
            submitter: async (callbackId, cbCtx) =>
            {
                var payload = $$"""{"callbackId":"{{callbackId}}","orderId":"{{input.OrderId}}"}""";
                await LambdaClient.InvokeAsync(new InvokeRequest
                {
                    FunctionName = externalFunctionName,
                    InvocationType = InvocationType.Event,
                    Payload = payload
                });
            },
            name: "approve");

        return result;
    }
}

public class TestEvent { public string? OrderId { get; set; } }
public class MyResult { public string? Status { get; set; } public string? ApprovedBy { get; set; } }
