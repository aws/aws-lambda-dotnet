// 9-6: Map throw-if-error propagates an item failure to the execution
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace MapThrowIfError;

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
        => DurableFunction.WrapAsync<object?, List<string>>(Workflow, input, context);

    private async Task<List<string>> Workflow(object? input, IDurableContext context)
    {
        var result = await context.MapAsync<string, string>(
            new List<string> { "fail", "never" },
            async (ctx, item, index, all, ct) =>
            {
                if (item == "fail") throw new Exception("item failed");
                return item;
            },
            name: "throwing",
            config: new MapConfig<string>
            {
                MaxConcurrency = 1,
                CompletionConfig = new CompletionConfig { ToleratedFailureCount = 0 }
            });

        // Rethrows the first item failure; uncaught, the execution fails.
        result.ThrowIfError();
        return result.GetResults().ToList();
    }
}
