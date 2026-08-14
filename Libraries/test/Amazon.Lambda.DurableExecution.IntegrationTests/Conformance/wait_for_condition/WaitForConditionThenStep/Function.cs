// 6-12: Wait-for-condition followed by a step (result passed onward)
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace WaitForConditionThenStep;

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
        var threshold = input;
        var pollResult = await context.WaitForConditionAsync(
            async (state, checkCtx, ct) =>
            {
                await Task.CompletedTask;
                return state + 1;
            },
            new WaitForConditionConfig<int>
            {
                InitialState = 0,
                WaitStrategy = WaitStrategy.FromDelegate<int>((state, attempt) =>
                    state >= threshold ? WaitDecision.Stop() : WaitDecision.ContinueAfter(TimeSpan.FromSeconds(1)))
            });

        var stepResult = await context.StepAsync(
            async (_, ct) =>
            {
                await Task.CompletedTask;
                return pollResult * 10;
            });

        return stepResult;
    }
}
