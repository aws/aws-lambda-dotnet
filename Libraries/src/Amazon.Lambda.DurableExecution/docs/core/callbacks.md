# Callbacks

Callbacks let a workflow suspend until an external system (a human approver, a webhook, another service) delivers a result. The external system completes the callback by calling `SendDurableExecutionCallbackSuccess`, `SendDurableExecutionCallbackFailure`, or `SendDurableExecutionCallbackHeartbeat` with the `callbackId` you handed it.

Two APIs are available:

- `WaitForCallbackAsync<T>` — composite operation; create the callback, hand it to the external system inside a submitter delegate, and suspend until the result arrives.
- `CreateCallbackAsync<T>` — lower-level; allocate the callback yourself, hand the ID out in your own steps, and `await` the result separately.

## `WaitForCallbackAsync<T>`

```csharp
Task<T> WaitForCallbackAsync<T>(
    Func<string, IWaitForCallbackContext, Task> submitter,
    string? name = null,
    WaitForCallbackConfig? config = null,
    CancellationToken cancellationToken = default);
```

The submitter receives the freshly allocated `callbackId` and an `IWaitForCallbackContext` (logger-only). Submitter failures (after retries are exhausted) surface as `CallbackSubmitterException`; callback failures and timeouts surface as `CallbackFailedException` / `CallbackTimeoutException`.

## `CreateCallbackAsync<T>`

```csharp
Task<ICallback<T>> CreateCallbackAsync<T>(
    string? name = null,
    CallbackConfig? config = null,
    CancellationToken cancellationToken = default);
```

The returned `ICallback<T>` exposes:

- `string CallbackId` — give this to the external system.
- `Task<T> GetResultAsync(CancellationToken)` — `await` to suspend until the external system completes the callback.

The result is deserialized using the registered `ILambdaSerializer`. Throws `CallbackFailedException` or `CallbackTimeoutException` on failure.

## End-to-end example

Two Lambdas: a workflow that suspends on a callback, and a separate approver Lambda that resolves it. The workflow hands its `callbackId` to the approver via `Event` invocation (fire-and-forget), then suspends. The approver runs in its own Lambda and signals completion by calling `SendDurableExecutionCallbackSuccessAsync`.

### 1. Workflow Lambda — `WaitForCallbackAsync<T>`

```csharp
using Amazon.Lambda;
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.Model;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace OrderApprovalWorkflow;

public class Function
{
    private static readonly IAmazonLambda LambdaClient = new AmazonLambdaClient();

    public static async Task Main()
    {
        var handler = new Function();
        var serializer = new DefaultLambdaJsonSerializer();
        using var wrapper = HandlerWrapper.GetHandlerWrapper<DurableExecutionInvocationInput, DurableExecutionInvocationOutput>(
            handler.Handler, serializer);
        using var bootstrap = new LambdaBootstrap(wrapper);
        await bootstrap.RunAsync();
    }

    public Task<DurableExecutionInvocationOutput> Handler(
        DurableExecutionInvocationInput input, ILambdaContext context)
        => DurableFunction.WrapAsync<OrderInput, ApprovalResult>(Workflow, input, context);

    private async Task<ApprovalResult> Workflow(OrderInput input, IDurableContext ctx)
    {
        var approverFunctionName = Environment.GetEnvironmentVariable("APPROVER_FUNCTION_NAME")
            ?? throw new InvalidOperationException("APPROVER_FUNCTION_NAME env var not set");

        // Suspend until the approver Lambda calls SendDurableExecutionCallbackSuccessAsync
        // with this callback ID. The submitter is invoked once with a freshly-allocated
        // ID; it hands the ID to the approver and returns immediately.
        var result = await ctx.WaitForCallbackAsync<ApprovalResult>(
            submitter: async (callbackId, cbCtx) =>
            {
                var payload = $$"""{"callbackId":"{{callbackId}}","orderId":"{{input.OrderId}}"}""";
                await LambdaClient.InvokeAsync(new InvokeRequest
                {
                    FunctionName = approverFunctionName,
                    InvocationType = InvocationType.Event,   // fire-and-forget
                    Payload = payload
                });
            },
            name: "approve");

        return result;
    }
}

public record OrderInput(string OrderId);
public record ApprovalResult(string Status, string ApprovedBy);
```

### 2. Approver Lambda — completes the callback

A plain Lambda — no durable execution wrapper. It receives the callback ID, performs whatever logic the external system needs, and calls `SendDurableExecutionCallbackSuccessAsync` to resume the workflow.

```csharp
using System.Text;
using Amazon.Lambda;
using Amazon.Lambda.Core;
using Amazon.Lambda.Model;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace OrderApprovalWorkflow;

public class ApproverFunction
{
    private static readonly IAmazonLambda LambdaClient = new AmazonLambdaClient();

    public static async Task Main()
    {
        var handler = new ApproverFunction();
        var serializer = new DefaultLambdaJsonSerializer();
        using var wrapper = HandlerWrapper.GetHandlerWrapper<ApproverInput, object?>(
            handler.Handler, serializer);
        using var bootstrap = new LambdaBootstrap(wrapper);
        await bootstrap.RunAsync();
    }

    public async Task<object?> Handler(ApproverInput input, ILambdaContext context)
    {
        // The result JSON must match the T in WaitForCallbackAsync<T> — here, ApprovalResult.
        var resultJson = $$"""{"Status":"approved","ApprovedBy":"{{input.OrderId}}"}""";
        await LambdaClient.SendDurableExecutionCallbackSuccessAsync(
            new SendDurableExecutionCallbackSuccessRequest
            {
                CallbackId = input.CallbackId,
                Result = new MemoryStream(Encoding.UTF8.GetBytes(resultJson))
            });
        return null;
    }
}

public record ApproverInput(string CallbackId, string OrderId);
```

To signal failure instead, call `SendDurableExecutionCallbackFailureAsync` — the workflow throws `CallbackFailedException`. To extend the heartbeat deadline (when `HeartbeatTimeout` is configured), call `SendDurableExecutionCallbackHeartbeatAsync`.

### `CreateCallbackAsync<T>` variant

When you need to allocate the ID before deciding how to hand it out — e.g. several steps run between callback creation and submission — use `CreateCallbackAsync` and a separate `StepAsync` for the submission. Wrapping the hand-off in a step prevents replays from re-invoking the approver.

```csharp
private async Task<ApprovalResult> Workflow(OrderInput input, IDurableContext ctx)
{
    var cb = await ctx.CreateCallbackAsync<ApprovalResult>(name: "approve");

    await ctx.StepAsync(async _ =>
    {
        var payload = $$"""{"callbackId":"{{cb.CallbackId}}","orderId":"{{input.OrderId}}"}""";
        await LambdaClient.InvokeAsync(new InvokeRequest
        {
            FunctionName = approverFunctionName,
            InvocationType = InvocationType.Event,
            Payload = payload
        });
    }, name: "submit");

    return await cb.GetResultAsync();
}
```

## Configuration

```csharp
public class CallbackConfig
{
    public TimeSpan Timeout { get; set; }           // overall callback timeout, ≥ 1s or Zero (default = no timeout)
    public TimeSpan HeartbeatTimeout { get; set; }  // heartbeat-gap timeout, ≥ 1s or Zero (default = no timeout)
}

public class WaitForCallbackConfig : CallbackConfig
{
    public IRetryStrategy? RetryStrategy { get; set; } // applied to the submitter step only
}
```
