// 4-17: Sequential callbacks (A then B)
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace CallbackSequential;

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
        var resultA = await callbackA.GetResultAsync();

        var callbackB = await context.CreateCallbackAsync<string>(name: input[1]);
        var resultB = await callbackB.GetResultAsync();

        return $"{resultA}:{resultB}";
    }
}
