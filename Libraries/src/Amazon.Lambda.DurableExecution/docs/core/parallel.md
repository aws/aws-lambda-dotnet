# Parallel

`ParallelAsync` runs N branches concurrently, each in its own child context, and returns an `IBatchResult<T>` aggregating the per-branch outcomes. Each branch is checkpointed independently, so the fan-out survives Lambda re-invocations: branches that already completed are restored from their checkpoints on replay rather than re-run.

Use it to fan out independent work — calling several services at once, processing a set of items, racing redundant providers — when the branches don't depend on one another. For a sequential series of checkpointed operations, use [`StepAsync`](steps.md) instead; for an isolated single child context, use [`RunInChildContextAsync`](child-contexts.md).

## Signature

```csharp
// Unnamed branches — IBatchItem.Name is null; index is used for identity.
Task<IBatchResult<T>> ParallelAsync<T>(
    IReadOnlyList<Func<IDurableContext, CancellationToken, Task<T>>> branches,
    string? name = null,
    ParallelConfig? config = null,
    CancellationToken cancellationToken = default);

// Named branches — the name surfaces on IBatchItem.Name and in execution traces.
Task<IBatchResult<T>> ParallelAsync<T>(
    IReadOnlyList<DurableBranch<T>> branches,
    string? name = null,
    ParallelConfig? config = null,
    CancellationToken cancellationToken = default);
```

Each branch receives its own `IDurableContext` and a `CancellationToken` (linking the caller-supplied token with the SDK's workflow-shutdown signal — see [Cancellation](cancellation.md)), so a branch can itself use steps, waits, and nested durable operations. Branch results are serialized to per-branch checkpoints via the `ILambdaSerializer` registered on `ILambdaContext.Serializer`. The operation `name` is used for observability and to derive the deterministic operation ID, so keep it stable across deployments.

## Example

Fan out three independent lookups and collect the results:

```csharp
var batch = await ctx.ParallelAsync(
    new[]
    {
        new DurableBranch<PricingQuote>("primary",  async (branchCtx, ct) =>
            await branchCtx.StepAsync((_, t) => primaryProvider.QuoteAsync(order, t), name: "quote")),
        new DurableBranch<PricingQuote>("secondary", async (branchCtx, ct) =>
            await branchCtx.StepAsync((_, t) => secondaryProvider.QuoteAsync(order, t), name: "quote")),
        new DurableBranch<PricingQuote>("tertiary",  async (branchCtx, ct) =>
            await branchCtx.StepAsync((_, t) => tertiaryProvider.QuoteAsync(order, t), name: "quote")),
    },
    name: "fan-out-quotes");

var quotes = batch.GetResults();   // all three, in original branch order
```

With the default completion policy (`AllSuccessful`, fail-fast), any single branch failure resolves the batch with `CompletionReason.FailureToleranceExceeded` and `HasFailure == true`. The operation never throws on failure — inspect the result or call `ThrowIfError()`.

## Configuration

```csharp
public sealed class ParallelConfig
{
    public int? MaxConcurrency { get; set; }                                   // null = unlimited; must be >= 1 when set
    public CompletionConfig CompletionConfig { get; set; } = CompletionConfig.AllSuccessful();
    public NestingType NestingType { get; set; } = NestingType.Nested;
}
```

`MaxConcurrency` bounds how many branches run at once via a semaphore — useful to avoid overwhelming a downstream service. `NestingType.Nested` (default) gives each branch a full child context visible in traces; `NestingType.Flat` runs branches in virtual contexts that emit no per-branch `CONTEXT` checkpoint, recording per-branch results inline on the parallel operation's payload instead — fewer checkpoints, at the cost of trace granularity.

## Completion policies

`CompletionConfig` decides when the batch resolves and whether it resolves as success or failure. Construct it via the static factories or set the threshold properties directly; multiple criteria combine, and the batch resolves as soon as any one is met or violated.

| Factory | Behavior |
| --- | --- |
| `CompletionConfig.AllSuccessful()` | Every branch must succeed (equivalent to `ToleratedFailureCount = 0`, and to a default/empty `CompletionConfig`). Any failure resolves the batch as `FailureToleranceExceeded`. **Default.** |
| `CompletionConfig.AllCompleted()` | Run every branch to a terminal state regardless of failures (`ToleratedFailureCount = int.MaxValue`). Inspect `Succeeded` / `Failed` (or call `ThrowIfError`) afterward. |
| `CompletionConfig.FirstSuccessful()` | Resolve as soon as one branch succeeds (`MinSuccessful = 1`). Branches not yet dispatched are reported as `Started`. |

For finer control, set the properties yourself:

```csharp
public sealed class CompletionConfig
{
    public int? MinSuccessful { get; set; }                  // resolve once this many branches succeed; null = no minimum
    public int? ToleratedFailureCount { get; set; }          // fail when failures strictly exceed this count
    public double? ToleratedFailurePercentage { get; set; }  // fail when failure ratio strictly exceeds this [0.0–1.0]
}
```

The chosen policy is recorded on the result as a `CompletionReason`: `AllCompleted`, `MinSuccessfulReached`, or `FailureToleranceExceeded`.

> **Dispatched branches always run to completion.** A short-circuit (e.g. `FirstSuccessful` reaching its `MinSuccessful`, or a failure threshold being exceeded) stops *new* branches from being dispatched — those surface as `Started` — but branches already in flight are never cancelled. This guarantees replay determinism: every dispatched branch ends in a terminal state, so the original run and any replay agree. The consequence is that with `MaxConcurrency = null` (unlimited) every branch is dispatched up front, so `FirstSuccessful` still runs all of them to completion even though only the first success is needed. Set `MaxConcurrency` to bound how many branches run at once and limit this wasted compute.

## Inspecting results

`IBatchResult<T>` exposes both aggregate counts and per-branch items:

```csharp
batch.All        // IReadOnlyList<IBatchItem<T>>, original index order
batch.Succeeded  // items with Status == Succeeded
batch.Failed     // items with Status == Failed
batch.Started    // items not dispatched before a short-circuit resolved the batch

batch.GetResults();   // IReadOnlyList<T> of successful results — never throws
batch.GetErrors();    // IReadOnlyList<DurableExecutionException> of failures
batch.ThrowIfError(); // throw the first failure, if any

batch.SuccessCount;   // also FailureCount, StartedCount, TotalCount, HasFailure
batch.CompletionReason;
```

Each `IBatchItem<T>` carries `Index`, `Name`, `Status` (`Succeeded` / `Failed` / `Started`), `Result` (populated only when succeeded), and `Error` (populated only when failed).

## Failure handling

```csharp
// Drive every branch to completion, then inspect partial results.
var batch = await ctx.ParallelAsync(
    branches,
    name: "process-items",
    config: new ParallelConfig { CompletionConfig = CompletionConfig.AllCompleted() });

foreach (var item in batch.Failed)
{
    ctx.Logger.LogWarning("Branch {Name} failed: {Error}", item.Name, item.Error?.Message);
}

var succeeded = batch.GetResults();
```

The operation never throws on failure — even under the default `AllSuccessful` (fail-fast) policy, a batch with a failed branch resolves with `CompletionReason.FailureToleranceExceeded` and `HasFailure == true` (matching the JS/Python/Java SDKs). Inspect the result, or call `ThrowIfError()` to opt into surfacing the first branch failure as an exception:

```csharp
var batch = await ctx.ParallelAsync(branches, name: "fan-out");

if (batch.HasFailure)
{
    ctx.Logger.LogWarning(
        "Parallel operation failed ({Reason}); {Failed} of {Total} branches failed.",
        batch.CompletionReason, batch.FailureCount, batch.TotalCount);

    batch.ThrowIfError(); // rethrow the first branch's DurableExecutionException, if desired
}
```
