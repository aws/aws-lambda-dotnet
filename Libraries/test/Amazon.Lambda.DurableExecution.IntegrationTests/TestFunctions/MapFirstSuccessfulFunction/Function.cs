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
        // Four items, each waits a different (durable) duration. The shortest
        // wait should win and short-circuit the map via FirstSuccessful. Wait
        // durations are at least 1s (service timer granularity). The item value
        // IS the wait-seconds; the result is the item's index.
        var waitSeconds = new[] { 8, 1, 5, 6 };

        var batch = await context.MapAsync(
            waitSeconds,
            async (ctx, seconds, index, all, _) =>
            {
                await ctx.WaitAsync(TimeSpan.FromSeconds(seconds), name: $"wait_{index}");
                return index;
            },
            name: "race",
            config: new MapConfig { CompletionConfig = CompletionConfig.FirstSuccessful() });

        var winner = batch.Succeeded.FirstOrDefault();
        return new TestResult
        {
            Status = "completed",
            WinnerIndex = winner?.Index ?? -1,
            WinnerName = winner?.Name,
            CompletionReason = batch.CompletionReason.ToString(),
            SuccessCount = batch.SuccessCount,
            StartedCount = batch.StartedCount
        };
    }
}

public class TestEvent { public string? OrderId { get; set; } }
public class TestResult
{
    public string? Status { get; set; }
    public int WinnerIndex { get; set; }
    public string? WinnerName { get; set; }
    public string? CompletionReason { get; set; }
    public int SuccessCount { get; set; }
    public int StartedCount { get; set; }
}
