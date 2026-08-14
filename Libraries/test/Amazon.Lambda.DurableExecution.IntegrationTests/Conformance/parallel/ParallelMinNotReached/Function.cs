// 8-17: Parallel where MinSuccessful is not reached
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace ParallelMinNotReached;

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
            async (ctx, ct) => "ok"
        };

        var result = await context.ParallelAsync(
            branches,
            name: "min-not-reached",
            config: new ParallelConfig
            {
                MaxConcurrency = 1,
                CompletionConfig = new CompletionConfig { MinSuccessful = 3 }
            });

        return new
        {
            completionReason = ToWireReason(result.CompletionReason),
            status = result.HasFailure ? "FAILED" : "SUCCEEDED",
            successCount = result.SuccessCount,
            failureCount = result.FailureCount,
            totalCount = result.TotalCount
        };
    }

    private static string ToWireReason(CompletionReason reason) => reason switch
    {
        CompletionReason.AllCompleted => "ALL_COMPLETED",
        CompletionReason.MinSuccessfulReached => "MIN_SUCCESSFUL_REACHED",
        CompletionReason.FailureToleranceExceeded => "FAILURE_TOLERANCE_EXCEEDED",
        _ => reason.ToString()
    };
}
