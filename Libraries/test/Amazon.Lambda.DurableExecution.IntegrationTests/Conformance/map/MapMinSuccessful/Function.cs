// 9-7: Map min-successful early completion
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace MapMinSuccessful;

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
            new List<string> { "s0", "s1", "s2", "s3" },
            async (ctx, item, index, all, ct) => item,
            name: "min-successful",
            config: new MapConfig<string>
            {
                MaxConcurrency = 1,
                CompletionConfig = new CompletionConfig { MinSuccessful = 2 }
            });

        // After 2 successes the threshold is reached; items 2 and 3 are never
        // started, so TotalCount (dispatched items only) is 2.
        return new
        {
            completionReason = ToWireReason(result.CompletionReason),
            successCount = result.SuccessCount,
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
