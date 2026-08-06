// 9-13: Map with a custom item namer
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace MapItemNamer;

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
        => DurableFunction.WrapAsync<List<int>?, List<int>>(Workflow, input, context);

    private async Task<List<int>> Workflow(List<int>? input, IDurableContext context)
    {
        var items = input is { Count: > 0 } ? input : new List<int> { 1, 2 };

        // The item namer names each iteration from its item; it affects
        // observability (the iteration operation name) but not results.
        var result = await context.MapAsync<int, int>(
            items,
            async (ctx, item, index, all, ct) => item * 10,
            name: "named-items",
            config: new MapConfig<int>
            {
                MaxConcurrency = 1,
                ItemNamer = (item, index) => $"item-{item}"
            });

        return result.GetResults().ToList();
    }
}
