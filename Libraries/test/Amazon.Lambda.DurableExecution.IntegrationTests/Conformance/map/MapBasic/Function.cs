// 9-1: Map basic (one step per item, all succeed)
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace MapBasic;

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
        => DurableFunction.WrapAsync<List<string>?, List<string>>(Workflow, input, context);

    private async Task<List<string>> Workflow(List<string>? input, IDurableContext context)
    {
        var items = input is { Count: > 0 } ? input : new List<string> { "World", "Kiro" };

        var result = await context.MapAsync<string, string>(
            items,
            async (ctx, item, index, all, ct) =>
                await ctx.StepAsync(async (_, _ct) => $"Hello, {item}!"),
            name: "map",
            config: new MapConfig<string> { MaxConcurrency = 1 });

        return result.GetResults().ToList();
    }
}
