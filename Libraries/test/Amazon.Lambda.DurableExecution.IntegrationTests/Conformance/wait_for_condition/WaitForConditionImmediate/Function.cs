// 6-2: Wait-for-condition immediate stop (condition already met on first check)
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace WaitForConditionImmediate;

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
                return state;
            },
            new WaitForConditionConfig<int>
            {
                InitialState = input,
                WaitStrategy = WaitStrategy.FromDelegate<int>((state, attempt) =>
                    state >= 5 ? WaitDecision.Stop() : WaitDecision.ContinueAfter(TimeSpan.FromSeconds(1)))
            });

        return result;
    }
}
