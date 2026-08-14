// 3-18: Child context with step and wait inside, step and wait after
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace ChildStepWaitAfter;

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
        var childResult = await context.RunInChildContextAsync(async (childContext, _ct) =>
        {
            var stepResult = await childContext.StepAsync(
                async (_, _ct) =>
                {
                    await Task.CompletedTask;
                    return input;
                });

            await childContext.WaitAsync(TimeSpan.FromSeconds(2));

            return stepResult;
        }, name: "step-wait-after", config: new ChildContextConfig { SubType = "RunInChildContext" });

        var afterResult = await context.StepAsync(
            async (_, _ct) =>
            {
                await Task.CompletedTask;
                return childResult;
            });

        await context.WaitAsync(TimeSpan.FromSeconds(2));

        return afterResult;
    }
}
