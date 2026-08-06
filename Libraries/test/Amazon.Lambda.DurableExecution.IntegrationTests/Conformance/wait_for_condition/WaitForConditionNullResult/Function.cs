// 6-10: Wait-for-condition with null result
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace WaitForConditionNullResult;

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
        => DurableFunction.WrapAsync<object?, object?>(Workflow, input, context);

    private async Task<object?> Workflow(object? input, IDurableContext context)
    {
        var result = await context.WaitForConditionAsync<object?>(
            async (state, checkCtx, ct) =>
            {
                await Task.CompletedTask;
                return null;
            },
            new WaitForConditionConfig<object?>
            {
                InitialState = null,
                WaitStrategy = WaitStrategy.FromDelegate<object?>((state, attempt) =>
                    WaitDecision.Stop())
            });

        return result;
    }
}
