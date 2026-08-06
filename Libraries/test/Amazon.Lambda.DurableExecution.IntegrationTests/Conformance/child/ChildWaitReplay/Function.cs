// 3-13: Child context with wait inside - verify replay
// Child context containing only a wait, followed by a step outside the child
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace ChildWaitReplay;

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
            await childContext.WaitAsync(TimeSpan.FromSeconds(2));
            return input;
        }, name: "wait-replay", config: new ChildContextConfig { SubType = "RunInChildContext" });

        var result = await context.StepAsync(
            async (_, _ct) =>
            {
                await Task.CompletedTask;
                return childResult;
            });

        return result;
    }
}
