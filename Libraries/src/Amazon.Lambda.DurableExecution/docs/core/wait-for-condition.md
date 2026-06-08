# Wait For Condition

`WaitForConditionAsync` polls a check function until a wait strategy decides to stop. Between polls the workflow is suspended — the Lambda terminates and is re-invoked when the strategy's chosen delay elapses, so you pay for compute time only while the check actually runs.

Use it when you're waiting on something whose readiness you can only learn by *asking* (an order settling, a file landing in S3, an external job finishing) rather than waiting a fixed duration. For a fixed-duration pause, use [`WaitAsync`](wait.md) instead.

## Signature

```csharp
Task<TState> WaitForConditionAsync<TState>(
    Func<TState, IConditionCheckContext, Task<TState>> check,
    WaitForConditionConfig<TState> config,
    string? name = null,
    CancellationToken cancellationToken = default);
```

On every iteration the `check` function receives the state returned by the previous invocation — seeded by `config.InitialState` on the very first call — and returns the next state. The configured `IWaitStrategy<TState>` then decides whether to keep polling and how long to wait. State is checkpointed each iteration, so the polling loop survives Lambda re-invocations deterministically and you can carry per-poll bookkeeping (a cursor, a counter) inside the state itself.

The `IConditionCheckContext` parameter exposes the current `AttemptNumber` (1-based) and a scoped `Logger`. The returned state is serialized via the `ILambdaSerializer` registered on `ILambdaContext.Serializer`.

When the strategy stops because its `maxAttempts` limit is reached — rather than because the condition was met — the operation throws `WaitForConditionException` carrying `AttemptsExhausted` and the last observed `LastState`.

## Example

Poll an order's status until it reaches a terminal value:

```csharp
var finalStatus = await ctx.WaitForConditionAsync(
    check: async (state, checkCtx) =>
    {
        checkCtx.Logger.LogInformation("Polling order on attempt {Attempt}", checkCtx.AttemptNumber);
        return await orderService.GetStatusAsync(orderId);
    },
    config: new WaitForConditionConfig<OrderStatus>
    {
        InitialState = OrderStatus.Unknown,
        WaitStrategy = WaitStrategy.Exponential<OrderStatus>(
            isDone: s => s == OrderStatus.Completed || s == OrderStatus.Cancelled)
    },
    name: "wait-for-order-settle");
```

## Configuration

```csharp
public sealed class WaitForConditionConfig<TState>
{
    public required TState InitialState { get; set; }              // seeds the first check call
    public required IWaitStrategy<TState> WaitStrategy { get; set; } // decides continue/stop + delay
}
```

## Wait strategies

The check function reports state; the `IWaitStrategy<TState>` decides what to do with it. Each built-in strategy on the `WaitStrategy` static class accepts an optional `isDone` predicate, so the common case — stop when the latest state satisfies a condition — stays declarative without implementing the interface yourself.

| Member | Behavior |
| --- | --- |
| `WaitStrategy.Exponential<TState>(...)` | Delay grows by `backoffRate` each attempt, up to `maxDelay`. Defaults: 60 attempts, 5s initial, 300s max, 1.5× backoff, Full jitter. |
| `WaitStrategy.Linear<TState>(...)` | Delay grows by a fixed `increment` each attempt, optionally capped at `maxDelay`. Defaults: 60 attempts, 5s initial, 5s increment. |
| `WaitStrategy.Fixed<TState>(delay, ...)` | Every poll waits the same `delay`. Default: 60 attempts. |
| `WaitStrategy.FromDelegate<TState>(Func<TState, int, WaitDecision>)` | Wrap a custom decision function. |

These defaults are tuned for *polling*, not retry-on-exception, and match the Python, JS, and Java reference SDKs.

```csharp
public static IWaitStrategy<TState> Exponential<TState>(
    int maxAttempts = 60,
    TimeSpan? initialDelay = null,         // default 5s
    TimeSpan? maxDelay = null,             // default 300s
    double backoffRate = 1.5,
    JitterStrategy jitter = JitterStrategy.Full,
    Func<TState, bool>? isDone = null);
```

### Custom strategies

For richer logic (wall-clock budgets, conditional jitter), use `FromDelegate` or implement `IWaitStrategy<TState>` directly. `Decide` returns a `WaitDecision`:

```csharp
public interface IWaitStrategy<TState>
{
    WaitDecision Decide(TState state, int attemptNumber);
}

public readonly record struct WaitDecision
{
    public bool ShouldContinue { get; }
    public TimeSpan Delay { get; }   // floored at 1s by the service timer

    public static WaitDecision Stop();                      // condition met → return current state
    public static WaitDecision ContinueAfter(TimeSpan delay); // suspend, re-evaluate after delay
}
```

`Stop()` ends the operation successfully and returns the latest state. `ContinueAfter(delay)` suspends the Lambda until the delay elapses. To signal "gave up," a strategy throws `WaitForConditionException` (the built-in strategies do this when `attemptNumber` reaches `maxAttempts`).

## `WaitForCondition` vs. retries

`IWaitStrategy<TState>` is distinct from [`IRetryStrategy`](steps.md#retry-strategies): a retry strategy decides whether to retry *after an exception* (its input is the thrown `Exception`), while a wait strategy decides whether to keep polling *based on observed state* (its input is the latest `TState`). If the check function itself throws, that error surfaces as a `StepException` — the wait strategy is not consulted.

## Failure handling

```csharp
try
{
    var status = await ctx.WaitForConditionAsync(check, config, name: "poll-job");
}
catch (WaitForConditionException ex)
{
    // Strategy exhausted its attempts without the condition being met.
    var attempts = ex.AttemptsExhausted;
    var last = (JobStatus?)ex.LastState;   // boxed — cast to your state type
    ctx.Logger.LogWarning("Gave up after {Attempts} polls; last status was {Status}", attempts, last);
}
```
