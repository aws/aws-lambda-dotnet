// 9-9: Map tolerated-failure-count exceeded (stops early)
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace MapToleratedExceeded;

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
        var result = await context.MapAsync<string, string>(
            new List<string> { "f0", "f1", "never" },
            async (ctx, item, index, all, ct) =>
            {
                if (item != "never") throw new Exception("item failed");
                return item;
            },
            name: "tolerated-exceeded",
            config: new MapConfig<string>
            {
                MaxConcurrency = 1,
                CompletionConfig = new CompletionConfig { ToleratedFailureCount = 1 }
            });

        // Items 0 and 1 fail (failure count 2 exceeds the tolerance of 1), so
        // item 2 is never started.
        return new
        {
            completionReason = ToWireReason(result.CompletionReason),
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
