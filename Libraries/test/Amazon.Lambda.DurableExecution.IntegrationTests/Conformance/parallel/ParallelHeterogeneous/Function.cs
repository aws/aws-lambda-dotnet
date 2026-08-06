// 8-4: Parallel with heterogeneous return types
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace ParallelHeterogeneous;

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
        => DurableFunction.WrapAsync<object?, List<object>>(Workflow, input, context);

    private async Task<List<object>> Workflow(object? input, IDurableContext context)
    {
        var branches = new List<Func<IDurableContext, CancellationToken, Task<object>>>
        {
            async (ctx, ct) => "hello",
            async (ctx, ct) => 42,
            async (ctx, ct) => new { k = "v" }
        };

        var result = await context.ParallelAsync(
            branches,
            config: new ParallelConfig { MaxConcurrency = 1 });

        return result.GetResults().ToList();
    }
}
