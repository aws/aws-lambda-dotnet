# Cancellation

Every user `Func` accepted by `IDurableContext` (`StepAsync`, `RunInChildContextAsync`, `WaitForCallbackAsync`, `WaitForConditionAsync`) receives a `CancellationToken` parameter. Pass it to cancellation-aware APIs inside the body so the workflow can tear down cleanly.

## What the token observes

The token is a linked source combining:

1. The `CancellationToken` you passed to the `IDurableContext` method (so the caller's cancel intent reaches the body).
2. An SDK-owned workflow-shutdown signal that fires when the workflow is being torn down (a sibling operation suspended, a checkpoint failed, or a future parallel branch aborted).

```csharp
var user = await ctx.StepAsync(
    async (_, ct) => await httpClient.GetAsync(url, ct),
    name: "fetch");
```

When either trigger fires, the token transitions to `IsCancellationRequested = true` and `await`s on cancellation-aware APIs unwind via `OperationCanceledException`.

## Semantics

- **`OperationCanceledException` thrown out of a step body via the token** (i.e. `linked.IsCancellationRequested` is true) is treated as cancellation: no FAIL checkpoint is written, no retry is consulted. The exception propagates up.
- **`OperationCanceledException` thrown by user code for unrelated reasons** (token never fired) is treated as a normal step failure: FAIL checkpoint, retry per the configured `RetryStrategy`.
- **The SDK's own writes (checkpoints, batcher flush, the runtime API response)** never observe the workflow-shutdown signal. Successful work is never lost to teardown.
- **When the invocation ends**, the token is cancelled before `DurableFunction.WrapAsync` returns. Work the workflow started but did not await (for example a step still running when a sibling suspended) observes the cancellation. Starting a new durable operation after this point throws `OperationCanceledException`.

## Lambda Managed Instances

On Lambda Managed Instances several invocations run in the same process, and Lambda does not stop an invocation that times out. The SDK therefore applies the invocation deadline (`ILambdaContext.RemainingTime`) itself:

- At the deadline the token is cancelled, new durable operations are rejected, and state loading is cancelled.
- An exception your code throws after the deadline (for example, after catching the cancellation) is not recorded as a step failure, and does not consume a retry.
- An invocation that starts with its deadline already passed times out immediately. When testing in this mode, set `TestLambdaContext.RemainingTime`.
- Checkpoint cleanup gets a short budget past the deadline, then is abandoned.
- `WrapAsync` throws `TimeoutException`. Lambda treats this as an invocation error, the same outcome as a timeout in on-demand Lambda: the execution is not marked failed, and work that was not checkpointed runs again on replay.

Only cooperative code can be stopped. Code that ignores the token, or blocks synchronously, keeps running until it returns, and keeps a thread busy that other invocations could use.

On-demand Lambda is unchanged: the SDK applies no deadline, and the environment's timeout ends the invocation.

## Guidance

- **Do** pass `ct` into every cancellation-aware call inside the step body (`HttpClient.SendAsync(ct)`, `Task.Delay(ct)`, AWS SDK calls). This is what makes caller-cancel and shutdown-cancel actually unwind.
- **Don't** branch workflow logic on `IsCancellationRequested`. It is a runtime concern, not a workflow concern; branching on it makes the workflow non-deterministic across replays.
- **Don't** `catch (OperationCanceledException)` and continue. Either don't catch, or catch and rethrow.

## Replay

Cached operations short-circuit before the user `Func` is invoked. A `SUCCESS` checkpoint replays its serialized result; the token is never built or observed. Replay determinism is structural — cancellation cannot affect it.
