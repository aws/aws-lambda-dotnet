// 8-3: Parallel with named branches using DurableBranch
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace ParallelNamedBranches;

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
        var branches = new List<DurableBranch<string>>
        {
            new DurableBranch<string>("first", async (ctx, ct) => "one"),
            new DurableBranch<string>("second", async (ctx, ct) => "two")
        };

        var result = await context.ParallelAsync(
            branches,
            name: "named-parallel",
            config: new ParallelConfig { MaxConcurrency = 1 });

        return result.GetResults().ToList();
    }
}
