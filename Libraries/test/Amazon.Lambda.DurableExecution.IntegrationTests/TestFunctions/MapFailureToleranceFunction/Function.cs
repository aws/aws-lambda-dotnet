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
        // Five items, two throw. ToleratedFailureCount = 1 means a second failure
        // exceeds tolerance and the map surfaces a MapException — terminating the
        // workflow FAILED.
        var items = new[] { "ok1", "bad1", "ok2", "bad2", "ok3" };

        var batch = await context.MapAsync(
            items,
            async (ctx, item, index, all, _) =>
            {
                await Task.CompletedTask;
                if (item.StartsWith("bad"))
                    throw new InvalidOperationException($"{item} boom");
                return item;
            },
            name: "tolerance",
            config: new MapConfig
            {
                CompletionConfig = new CompletionConfig { ToleratedFailureCount = 1 }
            });

        // Should not reach here — the map must throw MapException.
        return new TestResult { Status = "should_not_reach", SuccessCount = batch.SuccessCount };
    }
}

public class TestEvent { public string? OrderId { get; set; } }
public class TestResult
{
    public string? Status { get; set; }
    public int SuccessCount { get; set; }
}
