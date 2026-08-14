// 9-12: Map with FLAT nesting (virtual iteration contexts)
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace MapFlat;

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
        // With FLAT nesting each item's step is checkpointed directly under the
        // parent Map context; no per-iteration MapIteration context events.
        var result = await context.MapAsync<string, string>(
            new List<string> { "fa", "fb" },
            async (ctx, item, index, all, ct) =>
                await ctx.StepAsync(async (_, _ct) => item),
            name: "flat",
            config: new MapConfig<string>
            {
                MaxConcurrency = 1,
                NestingType = NestingType.Flat
            });

        return result.GetResults().ToList();
    }
}
