// 8-21: Nested parallel (outer parallel contains inner parallel)
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace ParallelNested;

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
        => DurableFunction.WrapAsync<object?, List<List<string>>>(Workflow, input, context);

    private async Task<List<List<string>>> Workflow(object? input, IDurableContext context)
    {
        var outerBranches = new List<Func<IDurableContext, CancellationToken, Task<List<string>>>>
        {
            async (ctx, ct) =>
            {
                var innerBranches = new List<Func<IDurableContext, CancellationToken, Task<string>>>
                {
                    async (innerCtx, innerCt) => await innerCtx.StepAsync(async (_, _ct) => "i1"),
                    async (innerCtx, innerCt) => await innerCtx.StepAsync(async (_, _ct) => "i2")
                };

                var innerResult = await ctx.ParallelAsync(
                    innerBranches,
                    name: "inner",
                    config: new ParallelConfig { MaxConcurrency = 1 });

                return innerResult.GetResults().ToList();
            }
        };

        var outerResult = await context.ParallelAsync(
            outerBranches,
            name: "outer",
            config: new ParallelConfig { MaxConcurrency = 1 });

        return outerResult.GetResults().ToList();
    }
}
