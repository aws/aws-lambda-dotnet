// 9-16: Map with a large aggregate result (exceeds the checkpoint size threshold)
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace MapLargeResult;

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
        => DurableFunction.WrapAsync<object?, object>(Workflow, input, context);

    private async Task<object> Workflow(object? input, IDurableContext context)
    {
        // Each iteration returns ~70KB; 4 items -> ~280KB aggregate, exceeding
        // the 256KB checkpoint threshold.
        var big = new string('x', 70000);

        var result = await context.MapAsync<int, string>(
            new List<int> { 0, 1, 2, 3 },
            async (ctx, item, index, all, ct) => big,
            name: "large",
            config: new MapConfig<int> { MaxConcurrency = 1 });

        return new
        {
            successCount = result.SuccessCount,
            totalCount = result.TotalCount
        };
    }
}
