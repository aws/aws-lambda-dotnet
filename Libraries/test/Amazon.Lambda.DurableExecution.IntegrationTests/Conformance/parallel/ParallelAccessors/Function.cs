// 8-20: Parallel result accessors (HasFailure, Succeeded, Failed, GetErrors)
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace ParallelAccessors;

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
        => DurableFunction.WrapAsync<object?, object>(Workflow, input, context);

    private async Task<object> Workflow(object? input, IDurableContext context)
    {
        var branches = new List<Func<IDurableContext, CancellationToken, Task<string>>>
        {
            async (ctx, ct) => "ok",
            async (ctx, ct) => throw new Exception("branch failed"),
            async (ctx, ct) => "ok2"
        };

        var result = await context.ParallelAsync(
            branches,
            name: "accessors",
            config: new ParallelConfig
            {
                MaxConcurrency = 1,
                CompletionConfig = new CompletionConfig { ToleratedFailureCount = 1 }
            });

        return new
        {
            hasFailure = result.HasFailure,
            successCount = result.Succeeded.Count,
            failureCount = result.Failed.Count,
            errorCount = result.GetErrors().Count
        };
    }
}
