using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using Microsoft.Extensions.Logging;

namespace DurableExecutionTestFunction;

/// <summary>
/// Workflow used by ReplayAwareLoggerTest. Pairs each replay-aware
/// <c>context.Logger.LogInformation</c> line with a control
/// <c>Console.WriteLine</c> so the test can prove the SDK suppresses replay
/// duplicates: the LogInformation lines should appear exactly once across the
/// two invocations a Wait-driven workflow produces, while the Console.WriteLine
/// control lines should appear once per invocation.
/// </summary>
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
        // Workflow-level: emitted on invocation 1, suppressed on invocation 2 (replay).
        context.Logger.LogInformation("LOG_REPLAY_TEST workflow_start order={OrderId}", input.OrderId);
        Console.WriteLine($"LOG_REPLAY_CONTROL workflow_start order={input.OrderId}");

        var step1 = await context.StepAsync(
            async (_) =>
            {
                // Emitted inside the step's BeginScope, so the line carries
                // both execution-level scope (durableExecutionArn, awsRequestId)
                // AND step-level scope (operationId, operationName, attempt).
                context.Logger.LogInformation("LOG_REPLAY_TEST inside_step1 order={OrderId}", input.OrderId);
                await Task.CompletedTask;
                return $"validated-{input.OrderId}";
            },
            name: "validate");

        // Between-step log: invocation 1 emits, invocation 2 is still in Replay
        // (Wait-on-SUCCEEDED replay does not flip the mode), so it must be suppressed.
        context.Logger.LogInformation("LOG_REPLAY_TEST after_step1 result={Result}", step1);
        Console.WriteLine($"LOG_REPLAY_CONTROL after_step1 result={step1}");

        await context.WaitAsync(TimeSpan.FromSeconds(3), name: "short_wait");

        // Step 2 runs fresh on invocation 2 — its EnterExecutionMode flips the
        // logger from suppress to passthrough. The next LogInformation lands.
        var step2 = await context.StepAsync(
            async (_) =>
            {
                await Task.CompletedTask;
                return $"processed-{step1}";
            },
            name: "process");

        context.Logger.LogInformation("LOG_REPLAY_TEST workflow_end final={Final}", step2);
        Console.WriteLine($"LOG_REPLAY_CONTROL workflow_end final={step2}");

        return new TestResult { Status = "completed", Data = step2 };
    }
}

public class TestEvent { public string? OrderId { get; set; } }
public class TestResult { public string? Status { get; set; } public string? Data { get; set; } }
