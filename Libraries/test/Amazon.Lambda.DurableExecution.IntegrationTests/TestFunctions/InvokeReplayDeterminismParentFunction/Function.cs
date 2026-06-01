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
        var downstreamArn = System.Environment.GetEnvironmentVariable("DOWNSTREAM_FUNCTION_ARN")
            ?? throw new InvalidOperationException("DOWNSTREAM_FUNCTION_ARN env var is not set.");

        // Step 1 generates a fresh GUID. On replay this MUST return the
        // checkpointed value — proves the SDK's deterministic operation IDs
        // line up with the service's view of the state.
        var generatedId = await context.StepAsync(
            async (_) => { await Task.CompletedTask; return Guid.NewGuid().ToString(); },
            name: "before_invoke");

        // The chained invoke forces a suspend/resume cycle. After the resume,
        // step 1 must replay (returning the cached GUID) and the invoke must
        // not be re-fired (cached result is returned immediately).
        var invokeResult = await context.InvokeAsync<string, string>(
            downstreamArn,
            payload: generatedId,
            name: "echo_invoke");

        var afterInvoke = await context.StepAsync(
            async (_) => { await Task.CompletedTask; return $"final:{invokeResult}"; },
            name: "after_invoke");

        return new TestResult { Status = "completed", Data = afterInvoke };
    }
}

public class TestEvent { public string? OrderId { get; set; } }
public class TestResult { public string? Status { get; set; } public string? Data { get; set; } }
