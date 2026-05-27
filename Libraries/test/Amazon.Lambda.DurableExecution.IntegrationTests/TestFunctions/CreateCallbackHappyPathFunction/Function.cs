using Amazon.Lambda;
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.Model;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace DurableExecutionTestFunction;

public class Function
{
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
        // Hand the service-allocated callback ID to the paired ApproverFunction
        // (Event invocation — fire-and-forget). The approver runs in its own Lambda
        // and resolves the callback out-of-band by calling
        // SendDurableExecutionCallbackSuccess. This mirrors WaitForCallbackHappyPath's
        // topology so the test process never has to play "external system" — the
        // synchronous Invoke from the test would otherwise deadlock against the
        // suspended workflow.
        var externalFunctionName = System.Environment.GetEnvironmentVariable("EXTERNAL_FUNCTION_NAME")
            ?? throw new InvalidOperationException("EXTERNAL_FUNCTION_NAME env var not set");

        var cb = await context.CreateCallbackAsync<MyResult>(name: "approve");

        // Wrap the hand-off in a step so replays don't re-invoke the approver.
        await context.StepAsync(async _ =>
        {
            var payload = $$"""{"callbackId":"{{cb.CallbackId}}","orderId":"integ-test"}""";
            await LambdaClient.InvokeAsync(new InvokeRequest
            {
                FunctionName = externalFunctionName,
                InvocationType = InvocationType.Event,
                Payload = payload
            });
        }, name: "submit");

        return await cb.GetResultAsync();
    }
}

public class TestEvent { public string? OrderId { get; set; } }
public class MyResult { public string? Status { get; set; } public string? ApprovedBy { get; set; } }
