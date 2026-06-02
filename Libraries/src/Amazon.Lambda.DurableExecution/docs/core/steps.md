# Steps

`StepAsync` runs a unit of work whose result is checkpointed. On replay, completed steps return their cached result without re-executing.

## Signatures

```csharp
Task<T> StepAsync<T>(
    Func<IStepContext, Task<T>> func,
    string? name = null,
    StepConfig? config = null,
    CancellationToken cancellationToken = default);

Task StepAsync(
    Func<IStepContext, Task> func,
    string? name = null,
    StepConfig? config = null,
    CancellationToken cancellationToken = default);
```

The `IStepContext` parameter exposes the current `AttemptNumber`, the deterministic `OperationId`, and a scoped `Logger`. Returned values are serialized via the `ILambdaSerializer` registered on `ILambdaContext.Serializer`.

## Basic step

```csharp
var user = await ctx.StepAsync(
    async _ => await userService.GetUserAsync(userId),
    name: "fetch-user");
```

## Multiple steps

```csharp
var a = await ctx.StepAsync(async _ => $"a-{input.OrderId}", name: "step_1");
var b = await ctx.StepAsync(async _ => $"{a}-b", name: "step_2");
var c = await ctx.StepAsync(async _ => $"{b}-c", name: "step_3");
```

## Step configuration

Configure step behavior with `StepConfig`:

```csharp
public sealed class StepConfig
{
    public IRetryStrategy? RetryStrategy { get; set; }              // null = no retry
    public StepSemantics Semantics { get; set; } = StepSemantics.AtLeastOncePerRetry;
}
```

### Retry strategies

When a step throws, the configured `IRetryStrategy` decides whether to retry and after what delay.

```csharp
public interface IRetryStrategy
{
    RetryDecision ShouldRetry(Exception exception, int attemptNumber);
}

public readonly struct RetryDecision
{
    public bool ShouldRetry { get; }
    public TimeSpan Delay { get; }

    public static RetryDecision DoNotRetry();
    public static RetryDecision RetryAfter(TimeSpan delay);
}
```

Built-in strategies on the `RetryStrategy` static class:

| Member | Behavior |
| --- | --- |
| `RetryStrategy.Default` | 6 attempts, 2× backoff, 5s initial, 60s max, Full jitter. |
| `RetryStrategy.Transient` | 3 attempts, 2× backoff, 1s initial, 5s max, Half jitter. |
| `RetryStrategy.None` | 1 attempt only — no retry. |
| `RetryStrategy.Exponential(...)` | Builder for custom exponential strategies. |
| `RetryStrategy.FromDelegate(Func<Exception, int, RetryDecision>)` | Wrap a custom decision function. |

`Exponential` parameters:

```csharp
public static IRetryStrategy Exponential(
    int maxAttempts = 3,
    TimeSpan? initialDelay = null,         // default 5s
    TimeSpan? maxDelay = null,             // default 300s
    double backoffRate = 2.0,
    JitterStrategy jitter = JitterStrategy.Full,
    Type[]? retryableExceptions = null,
    string[]? retryableMessagePatterns = null);

public enum JitterStrategy { None, Full, Half }
```

When `retryableExceptions` and `retryableMessagePatterns` are both null (default), every exception is retried up to `maxAttempts`. If either is set, only matching exceptions are retried.

#### Step with retries

```csharp
var result = await ctx.StepAsync<string>(
    async stepCtx =>
    {
        if (stepCtx.AttemptNumber < 3)
            throw new InvalidOperationException($"flake on attempt {stepCtx.AttemptNumber}");
        return $"ok on attempt {stepCtx.AttemptNumber}";
    },
    name: "flaky_step",
    config: new StepConfig
    {
        RetryStrategy = RetryStrategy.Exponential(
            maxAttempts: 3,
            initialDelay: TimeSpan.FromSeconds(2),
            maxDelay: TimeSpan.FromSeconds(10),
            backoffRate: 2.0,
            jitter: JitterStrategy.None)
    });
```

### Step semantics

Control how a step behaves when interrupted mid-execution:

```csharp
public enum StepSemantics
{
    AtLeastOncePerRetry, // default — body may re-execute if Lambda is re-invoked mid-attempt
    AtMostOncePerRetry   // body executes at most once per retry attempt
}
```

| Semantic | Behavior | Use case |
| --- | --- | --- |
| `AtLeastOncePerRetry` (default) | Re-executes the step if interrupted before completion. | Idempotent operations (database upserts, API calls with idempotency keys). |
| `AtMostOncePerRetry` | Never re-executes; throws if interrupted. | Non-idempotent operations (sending email, charging payments). |

These semantics apply *per retry attempt*, not per overall execution. To achieve true at-most-once across the whole workflow, combine with `RetryStrategy.None`:

```csharp
var result = await ctx.StepAsync(
    async _ => await paymentService.ChargeAsync(amount),
    name: "charge-payment",
    config: new StepConfig
    {
        Semantics = StepSemantics.AtMostOncePerRetry,
        RetryStrategy = RetryStrategy.None
    });
```
