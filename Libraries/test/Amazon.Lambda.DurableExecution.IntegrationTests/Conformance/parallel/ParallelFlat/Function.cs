// 8-12: Parallel with flat nesting type
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace ParallelFlat;

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
        var branches = new List<Func<IDurableContext, CancellationToken, Task<string>>>
        {
            async (ctx, ct) => await ctx.StepAsync(async (_, _ct) => "fa"),
            async (ctx, ct) => await ctx.StepAsync(async (_, _ct) => "fb")
        };

        var result = await context.ParallelAsync(
            branches,
            name: "flat",
            config: new ParallelConfig
            {
                MaxConcurrency = 1,
                NestingType = NestingType.Flat
            });

        return result.GetResults().ToList();
    }
}
