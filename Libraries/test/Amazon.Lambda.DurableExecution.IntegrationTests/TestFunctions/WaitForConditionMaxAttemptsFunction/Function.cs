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
        // Condition is never satisfied (isDone is always false), so the
        // strategy will eventually exhaust maxAttempts and the operation will
        // throw WaitForConditionException. The workflow catches it and
        // surfaces AttemptsExhausted in the result so the test can assert on
        // it without inspecting the FAILED status.
        try
        {
            await context.WaitForConditionAsync<int>(
                check: async (state, _, _) =>
                {
                    await Task.CompletedTask;
                    return state + 1;
                },
                config: new WaitForConditionConfig<int>
                {
                    InitialState = 0,
                    WaitStrategy = WaitStrategy.Fixed<int>(
                        delay: TimeSpan.FromSeconds(1),
                        maxAttempts: 3,
                        isDone: _ => false)
                },
                name: "exhausting_poll");

            return new TestResult { Status = "should_not_reach", AttemptsExhausted = -1 };
        }
        catch (WaitForConditionException ex)
        {
            return new TestResult { Status = "exhausted", AttemptsExhausted = ex.AttemptsExhausted };
        }
    }
}

public class TestEvent { public string? OrderId { get; set; } }
public class TestResult
{
    public string? Status { get; set; }
    public int AttemptsExhausted { get; set; }
}
