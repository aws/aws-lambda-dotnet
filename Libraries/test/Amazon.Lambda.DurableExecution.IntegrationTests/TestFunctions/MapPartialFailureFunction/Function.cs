using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace DurableExecutionTestFunction;

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
        => DurableFunction.WrapAsync<TestEvent, TestResult>(Workflow, input, context);

    private async Task<TestResult> Workflow(TestEvent input, IDurableContext context)
    {
        // Three items, the middle one throws. Map's DEFAULT CompletionConfig is
        // AllCompleted() (permissive) — unlike Parallel's AllSuccessful() — so NO
        // config is supplied here and the map must still drive every item to a
        // terminal state without throwing. This is the key Map-vs-Parallel
        // behavioral difference, validated end-to-end.
        var items = new[] { "ok1", "boom", "ok2" };

        var batch = await context.MapAsync(
            items,
            async (ctx, item, index, all, _) =>
            {
                await Task.CompletedTask;
                if (item == "boom")
                    throw new InvalidOperationException("intentional partial failure");
                return item;
            },
            name: "partial");

        var errors = batch.GetErrors();
        var errorSummary = string.Join("|", errors.Select(e => $"{e.GetType().Name}:{e.Message}"));

        return new TestResult
        {
            Status = "completed",
            SuccessCount = batch.SuccessCount,
            FailureCount = batch.FailureCount,
            ErrorSummary = errorSummary
        };
    }
}

public class TestEvent { public string? OrderId { get; set; } }
public class TestResult
{
    public string? Status { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public string? ErrorSummary { get; set; }
}
