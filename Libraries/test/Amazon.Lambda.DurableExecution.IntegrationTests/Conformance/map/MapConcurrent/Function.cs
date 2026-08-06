// 9-11: Map real concurrency (MaxConcurrency=2) preserves index-ordered results
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace MapConcurrent;

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
            new List<string> { "r0", "r1", "r2" },
            async (ctx, item, index, all, ct) => item,
            name: "concurrent",
            config: new MapConfig<string> { MaxConcurrency = 2 });

        // Results are guaranteed index-ordered regardless of completion order.
        return result.GetResults().ToList();
    }
}
