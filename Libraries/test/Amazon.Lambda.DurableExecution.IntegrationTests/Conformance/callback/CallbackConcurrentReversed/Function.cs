// 4-19: Concurrent callbacks reversed (create A, create B, await B, await A)
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace CallbackConcurrentReversed;

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
        => DurableFunction.WrapAsync<string[], string>(Workflow, input, context);

    private async Task<string> Workflow(string[] input, IDurableContext context)
    {
        var callbackA = await context.CreateCallbackAsync<string>(name: input[0]);
        var callbackB = await context.CreateCallbackAsync<string>(name: input[1]);

        var resultB = await callbackB.GetResultAsync();
        var resultA = await callbackA.GetResultAsync();

        return $"{resultA}:{resultB}";
    }
}
