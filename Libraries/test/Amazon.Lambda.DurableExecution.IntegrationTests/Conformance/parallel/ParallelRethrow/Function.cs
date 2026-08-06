// 8-7: Parallel rethrow (ThrowIfError propagates failure)
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace ParallelRethrow;

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
        => DurableFunction.WrapAsync<object?, string>(Workflow, input, context);

    private async Task<string> Workflow(object? input, IDurableContext context)
    {
        var branches = new List<Func<IDurableContext, CancellationToken, Task<string>>>
        {
            async (ctx, ct) => throw new Exception("branch error"),
            async (ctx, ct) => "never"
        };

        var result = await context.ParallelAsync(
            branches,
            name: "rethrow",
            config: new ParallelConfig
            {
                MaxConcurrency = 1,
                CompletionConfig = new CompletionConfig { ToleratedFailureCount = 0 }
            });

        result.ThrowIfError();

        return "unreachable";
    }
}
