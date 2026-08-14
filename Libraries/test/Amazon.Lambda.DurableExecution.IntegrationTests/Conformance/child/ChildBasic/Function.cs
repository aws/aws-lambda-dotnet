// 3-1: Child context basic - single step inside child context
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace ChildBasic;

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
        var result = await context.RunInChildContextAsync(async (childContext, _ct) =>
        {
            var stepResult = await childContext.StepAsync(
                async (_, _ct) =>
                {
                    await Task.CompletedTask;
                    return input;
                });

            return stepResult;
        }, config: new ChildContextConfig { SubType = "RunInChildContext" });

        return result;
    }
}
