// 3-16: Child context returning null - child returns null without any durable operation
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace ChildReturnsNull;

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
        var result = await context.RunInChildContextAsync<object?>(async (childContext, _ct) =>
        {
            await Task.CompletedTask;
            return null;
        }, name: "returns-null", config: new ChildContextConfig { SubType = "RunInChildContext" });

        return result;
    }
}
