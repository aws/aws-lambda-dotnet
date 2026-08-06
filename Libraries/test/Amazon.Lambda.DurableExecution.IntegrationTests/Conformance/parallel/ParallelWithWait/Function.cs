// 8-14: Parallel with WaitAsync in a branch
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace ParallelWithWait;

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
            async (ctx, ct) => await ctx.StepAsync(async (_, _ct) => "b0"),
            async (ctx, ct) =>
            {
                await ctx.WaitAsync(TimeSpan.FromSeconds(2));
                return "b1";
            }
        };

        var result = await context.ParallelAsync(
            branches,
            name: "wait-branch",
            config: new ParallelConfig { MaxConcurrency = 1 });

        return result.GetResults().ToList();
    }
}
