// 9-4: Map with an empty items list completes immediately with an empty results list
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace MapEmpty;

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
        => DurableFunction.WrapAsync<List<int>?, List<int>>(Workflow, input, context);

    private async Task<List<int>> Workflow(List<int>? input, IDurableContext context)
    {
        var items = input ?? new List<int>();

        var result = await context.MapAsync<int, int>(
            items,
            async (ctx, item, index, all, ct) => item,
            name: "empty");

        return result.GetResults().ToList();
    }
}
