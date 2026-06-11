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
        // Exponential strategy with no jitter so the timing is predictable.
        // Done flips on attempt 3 (1-based). With initialDelay=1s,
        // backoffRate=1.5, maxDelay=4s, no jitter: delays are 1s, 1.5s
        // (which the SDK ceilings to 2s due to 1s timer granularity).
        var finalState = await context.WaitForConditionAsync<State>(
            check: async (state, ctx, _) =>
            {
                await Task.CompletedTask;
                var done = ctx.AttemptNumber >= 3;
                return new State(done, ctx.AttemptNumber);
            },
            config: new WaitForConditionConfig<State>
            {
                InitialState = new State(false, 0),
                WaitStrategy = WaitStrategy.Exponential<State>(
                    maxAttempts: 5,
                    initialDelay: TimeSpan.FromSeconds(1),
                    maxDelay: TimeSpan.FromSeconds(4),
                    backoffRate: 1.5,
                    jitter: JitterStrategy.None,
                    isDone: s => s.Done)
            },
            name: "exp_poll");

        return new TestResult
        {
            Status = "completed",
            AttemptsTaken = finalState.AttemptNumber,
            Done = finalState.Done
        };
    }
}

public record State(bool Done, int AttemptNumber);

public class TestEvent { public string? OrderId { get; set; } }
public class TestResult
{
    public string? Status { get; set; }
    public int AttemptsTaken { get; set; }
    public bool Done { get; set; }
}
