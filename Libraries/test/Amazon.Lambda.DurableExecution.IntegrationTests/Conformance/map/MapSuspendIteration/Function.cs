// 9-15: Map suspends inside an iteration; replay skips the completed iteration
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace MapSuspendIteration;

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
            new List<string> { "r0", "r1" },
            async (ctx, item, index, all, ct) =>
            {
                // Iteration 1 issues a durable wait before its step, suspending
                // the whole execution mid-map. On replay iteration 0 is skipped.
                if (index == 1)
                {
                    await ctx.WaitAsync(TimeSpan.FromSeconds(1));
                }
                return await ctx.StepAsync(async (_, _ct) => item);
            },
            name: "suspend",
            config: new MapConfig<string> { MaxConcurrency = 1 });

        return result.GetResults().ToList();
    }
}
