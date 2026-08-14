// 6-7: Wait-for-condition check function throws (uncaught failure)
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace WaitForConditionCheckThrows;

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
        => DurableFunction.WrapAsync<object?, int>(Workflow, input, context);

    private async Task<int> Workflow(object? input, IDurableContext context)
    {
        var result = await context.WaitForConditionAsync<int>(
            async (state, checkCtx, ct) =>
            {
                await Task.CompletedTask;
                throw new InvalidOperationException("check function error");
            },
            new WaitForConditionConfig<int>
            {
                InitialState = 0,
                WaitStrategy = WaitStrategy.FromDelegate<int>((state, attempt) =>
                    WaitDecision.ContinueAfter(TimeSpan.FromSeconds(1)))
            });

        return result;
    }
}
