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
        // 6 items, MaxConcurrency = 2. Each item does a 2-second durable wait
        // then captures the post-wait wall-clock as a unix-ms timestamp. The
        // expected outcome is 3 waves of 2 items; total elapsed ~6s. Use
        // IDurableContext.WaitAsync (not Task.Delay) — Task.Delay is NOT durable
        // and would skew this measurement under replay.
        var items = new[] { 0, 1, 2, 3, 4, 5 };

        var batch = await context.MapAsync(
            items,
            async (ctx, item, index, all, _) =>
            {
                await ctx.WaitAsync(TimeSpan.FromSeconds(2), name: $"wait_{index}");
                return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            },
            name: "throttled",
            config: new MapConfig
            {
                MaxConcurrency = 2,
                CompletionConfig = CompletionConfig.AllCompleted()
            });

        return new TestResult
        {
            Status = "completed",
            SuccessCount = batch.SuccessCount,
            Timestamps = batch.GetResults().ToArray()
        };
    }
}

public class TestEvent { public string? OrderId { get; set; } }
public class TestResult
{
    public string? Status { get; set; }
    public int SuccessCount { get; set; }
    public long[]? Timestamps { get; set; }
}
