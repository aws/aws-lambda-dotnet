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
        // The check function throws on attempt 2. Per the WaitForCondition
        // contract, the check-thrown exception is checkpointed as FAIL and
        // surfaced through the SDK as a StepException carrying the original
        // exception type ("System.InvalidOperationException"). The workflow
        // catches it and reports the captured ErrorType so the test can assert
        // without requiring the workflow to FAIL outright.
        try
        {
            await context.WaitForConditionAsync<int>(
                check: async (state, ctx, _) =>
                {
                    await Task.CompletedTask;
                    if (ctx.AttemptNumber == 2)
                        throw new InvalidOperationException("intentional check failure on attempt 2");
                    return state + 1;
                },
                config: new WaitForConditionConfig<int>
                {
                    InitialState = 0,
                    WaitStrategy = WaitStrategy.Fixed<int>(
                        delay: TimeSpan.FromSeconds(1),
                        maxAttempts: 10,
                        isDone: _ => false)
                },
                name: "throwing_poll");

            return new TestResult { Status = "should_not_reach", ErrorType = null };
        }
        catch (StepException ex)
        {
            return new TestResult { Status = "caught_step_exception", ErrorType = ex.ErrorType, ErrorMessage = ex.Message };
        }
    }
}

public class TestEvent { public string? OrderId { get; set; } }
public class TestResult
{
    public string? Status { get; set; }
    public string? ErrorType { get; set; }
    public string? ErrorMessage { get; set; }
}
