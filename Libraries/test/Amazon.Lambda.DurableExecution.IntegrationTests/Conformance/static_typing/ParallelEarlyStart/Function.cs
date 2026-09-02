// 12-2: Parallel branch starts before registration is sealed
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace ParallelEarlyStart;

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
        string firstResult;
        IParallelBranch<string> second;

        await using (var parallel = context.CreateParallel(
            name: "early-start",
            config: new ParallelConfig { MaxConcurrency = 1 }))
        {
            IParallelBranch<string> first = parallel.BranchAsync(
                "first", (_, _) => Task.FromResult("ready"));
            firstResult = await first;

            second = parallel.BranchAsync(
                "second", (_, _) => Task.FromResult(firstResult + "-second"));
            await parallel.CompleteAsync();
        }

        return new List<string> { firstResult, await second };
    }
}
