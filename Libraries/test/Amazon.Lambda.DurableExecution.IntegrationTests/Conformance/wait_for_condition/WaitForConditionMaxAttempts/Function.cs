// 6-6: Wait-for-condition max attempts exceeded (failure)
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace WaitForConditionMaxAttempts;

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
        => DurableFunction.WrapAsync<int, int>(Workflow, input, context);

    private async Task<int> Workflow(int input, IDurableContext context)
    {
        var result = await context.WaitForConditionAsync(
            async (state, checkCtx, ct) =>
            {
                await Task.CompletedTask;
                return state + 1;
            },
            new WaitForConditionConfig<int>
            {
                InitialState = 0,
                WaitStrategy = WaitStrategy.Fixed<int>(
                    delay: TimeSpan.FromSeconds(1),
                    maxAttempts: 3,
                    isDone: _ => false)
            });

        return result;
    }
}
