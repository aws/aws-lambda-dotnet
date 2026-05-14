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
        // Throw inside a child context to validate the CONTEXT FAIL path: the
        // service must record a ContextFailed event with the error details and
        // mark the workflow FAILED.
        await context.RunInChildContextAsync<string>(
            async (childCtx) =>
            {
                await childCtx.StepAsync(
                    async (_) => { await Task.CompletedTask; return $"prepared-{input.OrderId}"; },
                    name: "prepare");

                throw new InvalidOperationException("intentional child context failure for integration test");
            },
            name: "phase",
            config: new ChildContextConfig { SubType = "OrderProcessing" });

        return new TestResult { Status = "should_not_reach" };
    }
}

public class TestEvent { public string? OrderId { get; set; } }
public class TestResult { public string? Status { get; set; } public string? Data { get; set; } }
