// 6-11: Wait-for-condition custom serdes (state survives serialization)
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace WaitForConditionCustomSerdes;

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
        => DurableFunction.WrapAsync<string, string>(Workflow, input, context);

    private async Task<string> Workflow(string input, IDurableContext context)
    {
        var result = await context.WaitForConditionAsync(
            async (state, checkCtx, ct) =>
            {
                await Task.CompletedTask;
                return state + "x";
            },
            new WaitForConditionConfig<string>
            {
                InitialState = "",
                WaitStrategy = WaitStrategy.FromDelegate<string>((state, attempt) =>
                    state.Length >= 2 ? WaitDecision.Stop() : WaitDecision.ContinueAfter(TimeSpan.FromSeconds(1)))
            });

        return result;
    }
}
