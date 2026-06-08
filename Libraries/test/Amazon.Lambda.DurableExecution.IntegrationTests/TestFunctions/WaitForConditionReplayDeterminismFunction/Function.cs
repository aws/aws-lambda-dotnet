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
        // Step 1: capture a fresh value. On replay this MUST return the
        // checkpointed value rather than re-executing.
        var generatedId = await context.StepAsync(
            async (_) => { await Task.CompletedTask; return Guid.NewGuid().ToString(); },
            name: "before_poll");

        // Wait-for-condition with 3 polls. Each poll iteration is a separate
        // invocation, and the operation's deterministic ID + RETRY-payload
        // state must round-trip across re-invocations.
        var pollResult = await context.WaitForConditionAsync<Counter>(
            check: async (state, ctx) =>
            {
                await Task.CompletedTask;
                return new Counter(state.Count + 1);
            },
            config: new WaitForConditionConfig<Counter>
            {
                InitialState = new Counter(0),
                WaitStrategy = WaitStrategy.Fixed<Counter>(
                    delay: TimeSpan.FromSeconds(2),
                    maxAttempts: 10,
                    isDone: c => c.Count >= 3)
            },
            name: "determinism_poll");

        // Step 2: echo the generated ID. After replay, this should see the
        // SAME GUID from step 1 — proves replay returned the cached value.
        var echoed = await context.StepAsync(
            async (_) => { await Task.CompletedTask; return $"echo:{generatedId}:{pollResult.Count}"; },
            name: "after_poll");

        return new TestResult { Status = "completed", Data = echoed };
    }
}

public record Counter(int Count);

public class TestEvent { public string? OrderId { get; set; } }
public class TestResult { public string? Status { get; set; } public string? Data { get; set; } }
