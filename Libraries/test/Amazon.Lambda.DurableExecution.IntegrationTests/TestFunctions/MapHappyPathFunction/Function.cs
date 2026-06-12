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
        var orders = new[] { "order-1", "order-2", "order-3" };

        // Each item is processed inside a step so the per-item child context
        // owns a leaf operation. ItemNamer gives each item a readable branch
        // name in the service-side history.
        var batch = await context.MapAsync(
            orders,
            async (ctx, orderId, index, all, _) =>
                await ctx.StepAsync(
                    async (_, _) => { await Task.CompletedTask; return $"{orderId}-{input.OrderId}"; },
                    name: "process"),
            name: "process_all",
            config: new MapConfig { ItemNamer = (item, index) => $"item-{item}" });

        var joined = string.Join(",", batch.GetResults());
        return new TestResult { Status = "completed", Data = joined };
    }
}

public class TestEvent { public string? OrderId { get; set; } }
public class TestResult { public string? Status { get; set; } public string? Data { get; set; } }
