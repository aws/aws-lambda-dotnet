// 9-10: Map tolerated-failure-percentage exceeded (stops early)
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace MapToleratedPct;

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
        // .NET's ToleratedFailurePercentage uses a 0.0-1.0 scale (the JS/Python
        // SDKs use 25), so 25% is expressed as 0.25.
        var result = await context.MapAsync<string, string>(
            new List<string> { "f0", "f1", "never", "never" },
            async (ctx, item, index, all, ct) =>
            {
                if (item != "never") throw new Exception("item failed");
                return item;
            },
            name: "tolerated-pct",
            config: new MapConfig<string>
            {
                MaxConcurrency = 1,
                CompletionConfig = new CompletionConfig { ToleratedFailurePercentage = 0.25 }
            });

        // Items 0 and 1 fail (2/4 = 50% exceeds 25%), so items 2 and 3 are never started.
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
