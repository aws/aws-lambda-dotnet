# Durable Execution Analyzers (DE001–DE004)

`Amazon.Lambda.DurableExecution` ships a set of Roslyn analyzers that run in the IDE and during
`dotnet build` to catch the most common durable-execution authoring mistakes before they become
confusing runtime failures. The analyzers are bundled inside the `Amazon.Lambda.DurableExecution`
NuGet package (`analyzers/dotnet/cs`), so they activate automatically for any project that references
the package — no extra reference is required. They only run on projects that reference the durable SDK
and only inside durable *workflow code* (a method, local function, or lambda that takes an
`IDurableContext` parameter), so unrelated code in the same project is unaffected.

These analyzers are the .NET counterpart of the JavaScript SDK's
[`@aws/durable-execution-sdk-js-eslint-plugin`](https://github.com/aws/aws-durable-execution-sdk-js/tree/main/packages/aws-durable-execution-sdk-js-eslint-plugin),
re-implemented with Roslyn's semantic model so durable operations are matched by symbol
(`Amazon.Lambda.DurableExecution.IDurableContext`) rather than by method name.

## Why determinism matters

A durable workflow has no persisted program counter. On every invocation — including every replay
after a wait, retry, or failure — your handler runs again from the top. Each durable call
(`StepAsync`, `WaitAsync`, …) looks up its checkpoint and either replays the cached result or runs
fresh. For the cached results to line up with the code, the workflow must execute the **same
operations, in the same order, every time**. Code *outside* a step must therefore be deterministic;
code *inside* a step may be non-deterministic because the step's result is checkpointed once.

## Rules

| ID | Severity | Rule |
|-------|----------|------|
| DE001 | Warning | Non-deterministic call outside a step |
| DE002 | Warning | Nested durable operation inside a step body |
| DE003 | Warning | Mutable variable captured and modified inside a durable operation |
| DE004 | Info | `Task.WhenAll`/`Task.WhenAny` over durable tasks |

### DE001 — Non-deterministic call outside a step

Flags `DateTime.Now`/`UtcNow`/`Today`, `DateTimeOffset.Now`/`UtcNow`, `Guid.NewGuid()`,
`new Random()` / `Random.Shared`, `Stopwatch.GetTimestamp()`/`StartNew()`/`Elapsed*`,
`Environment.TickCount`/`TickCount64`, `RandomNumberGenerator`/`RNGCryptoServiceProvider`, and
`Path.GetTempFileName()`/`GetRandomFileName()` when used in workflow code outside a step. The value
would differ between the original execution and replays, corrupting checkpoint-derived state. A
seeded `new Random(42)` is deterministic and is **not** flagged.

```csharp
// ❌ Flagged — different value on replay
var now = DateTime.UtcNow;
await context.StepAsync((s, ct) => DoWork(now));

// ✅ Captured inside a step
var now = await context.StepAsync((s, ct) => Task.FromResult(DateTime.UtcNow));
await context.StepAsync((s, ct) => DoWork(now));
```

A code fix is offered that wraps the expression in `context.StepAsync(...)`. It is a single-occurrence
quick fix only (no Fix All), because inserting a step shifts the position-derived operation IDs of
subsequent durable calls and would break replay for already-running executions.

### DE002 — Nested durable operation inside a step body

Flags any durable operation (`StepAsync`, `WaitAsync`, `ParallelAsync`, `MapAsync`, `InvokeAsync`,
`RunInChildContextAsync`, `CreateCallbackAsync`, `WaitForCallbackAsync`) invoked inside a *step-wrapped*
delegate — a `StepAsync` body, a `WaitForCallbackAsync` submitter, or a `WaitForConditionAsync` check —
by capturing the outer `IDurableContext`. Step bodies are leaf operations; group durable operations
with `RunInChildContextAsync` instead.

```csharp
// ❌ Flagged — durable op nested in a step body
await context.StepAsync(async (s, ct) =>
{
    await context.WaitAsync(TimeSpan.FromSeconds(1)); // uses the captured outer context
});

// ✅ Group with a child context
await context.RunInChildContextAsync(async (child, ct) =>
{
    await child.StepAsync((s, c) => DoWork());
    await child.WaitAsync(TimeSpan.FromSeconds(1));
});
```

### DE003 — Mutable variable captured and modified inside a durable operation

Flags assignment, compound assignment, or increment/decrement of a variable captured from an outer
scope inside a durable-operation delegate (`StepAsync`, `RunInChildContextAsync`,
`WaitForConditionAsync`, `WaitForCallbackAsync`, and `ParallelAsync`/`MapAsync` branches). On replay the
body is skipped and the cached result returned, so the write never happens. Reading a captured variable
is safe and is not flagged.

```csharp
// ❌ Flagged — write is lost on replay
int total = 0;
await context.StepAsync((s, ct) => { total += 1; return Task.CompletedTask; });

// ✅ Return the value and assign it
total = await context.StepAsync((s, ct) => Task.FromResult(total + 1));
```

### DE004 — `Task.WhenAll`/`Task.WhenAny` over durable tasks

Advisory (Info). `Task.WhenAll`/`Task.WhenAny` work correctly with durable tasks (operation IDs are
allocated deterministically), but they bypass completion policies, concurrency limits, branch naming,
and structured `IBatchResult` output. Prefer `ParallelAsync`/`MapAsync`.

```csharp
// ℹ️ Suggested — works, but prefer ParallelAsync
await Task.WhenAll(context.StepAsync(a), context.StepAsync(b));

// ✅ Preferred
await context.ParallelAsync(new Func<IDurableContext, CancellationToken, Task<int>>[]
{
    (c, ct) => c.StepAsync((s, t) => A()),
    (c, ct) => c.StepAsync((s, t) => B()),
});
```

A code fix converts the simplest safe shape (an inline list of same-typed durable calls on one context
whose aggregate result is discarded) into `ParallelAsync`.

## Configuring severity

Each rule's severity can be overridden per project via `.editorconfig`:

```ini
# Treat the non-determinism rule as an error, silence the WhenAll suggestion.
dotnet_diagnostic.DE001.severity = error
dotnet_diagnostic.DE004.severity = none
```

Note: if your project sets `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`, the Warning-level
rules (DE001–DE003) will fail the build. Lower their severity in `.editorconfig` if you prefer them as
warnings during the preview.
