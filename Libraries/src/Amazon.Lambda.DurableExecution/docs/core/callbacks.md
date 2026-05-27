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

```csharp
var result = await ctx.WaitForCallbackAsync<MyResult>(
    submitter: async (callbackId, cbCtx) =>
    {
        var payload = $$"""{"callbackId":"{{callbackId}}","orderId":"{{input.OrderId}}"}""";
        await LambdaClient.InvokeAsync(new InvokeRequest
        {
            FunctionName = externalFunctionName,
            InvocationType = InvocationType.Event,
            Payload = payload
        });
    },
    name: "approve");
```

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

```csharp
var cb = await ctx.CreateCallbackAsync<MyResult>(name: "approve");

await ctx.StepAsync(async _ =>
{
    var payload = $$"""{"callbackId":"{{cb.CallbackId}}","orderId":"{{input.OrderId}}"}""";
    await LambdaClient.InvokeAsync(new InvokeRequest
    {
        FunctionName = externalFunctionName,
        InvocationType = InvocationType.Event,
        Payload = payload
    });
}, name: "submit");

return await cb.GetResultAsync();
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
