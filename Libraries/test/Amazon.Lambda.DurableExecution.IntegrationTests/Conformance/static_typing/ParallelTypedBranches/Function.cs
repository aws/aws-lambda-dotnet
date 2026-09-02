// 12-1: Parallel with independently typed heterogeneous branch handles
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace ParallelTypedBranches;

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
        IParallelBranch<string> inventory;
        IParallelBranch<int> payment;
        IParallelBranch<Dictionary<string, string>> quote;

        await using (var parallel = context.CreateParallel(
            name: "typed-branches",
            config: new ParallelConfig { MaxConcurrency = 1 }))
        {
            inventory = parallel.BranchAsync(
                "inventory", (_, _) => Task.FromResult("reserved"));
            payment = parallel.BranchAsync(
                "payment", (_, _) => Task.FromResult(200));
            quote = parallel.BranchAsync(
                "quote", (_, _) => Task.FromResult(new Dictionary<string, string>
                {
                    ["currency"] = "USD"
                }));

            await parallel.CompleteAsync();
        }

        string inventoryResult = await inventory;
        int paymentResult = await payment;
        Dictionary<string, string> quoteResult = await quote;
        return new List<object> { inventoryResult, paymentResult, quoteResult };
    }
}
