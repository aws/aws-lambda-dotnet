using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace DurableExecutionTestFunction;

public class Function
{
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
        // The test deliberately never delivers the callback. The service should
        // fire the timeout, mark the callback TIMED_OUT, and the SDK should
        // surface CallbackTimeoutException to the workflow.
        var cb = await context.CreateCallbackAsync<MyResult>(
            name: "approve",
            config: new CallbackConfig { Timeout = TimeSpan.FromSeconds(10) });
        var result = await cb.GetResultAsync();
        return result;
    }
}

public class TestEvent { public string? OrderId { get; set; } }
public class MyResult { public string? Status { get; set; } public string? ApprovedBy { get; set; } }
