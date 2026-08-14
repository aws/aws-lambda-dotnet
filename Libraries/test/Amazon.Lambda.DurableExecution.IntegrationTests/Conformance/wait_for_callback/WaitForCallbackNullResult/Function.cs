// 7-15: WaitForCallback success with null payload
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace WaitForCallbackNullResult;

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
        => DurableFunction.WrapAsync<string, object?>(Workflow, input, context);

    private async Task<object?> Workflow(string input, IDurableContext context)
    {
        // External completes with no payload/null.
        var result = await context.WaitForCallbackAsync<object?>(
            async (callbackId, callbackContext, ct) =>
            {
                await Task.CompletedTask;
            },
            name: input);

        return result;
    }
}
