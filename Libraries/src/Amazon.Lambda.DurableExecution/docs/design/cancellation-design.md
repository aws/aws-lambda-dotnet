# Cancellation in Amazon.Lambda.DurableExecution — Design

> Status: design (2026-06-10). Targets the preview window before GA so the breaking delegate-shape change lands once.

Thread a `CancellationToken` into every user `Func` accepted by `IDurableContext`. Internally the SDK owns a workflow-scoped `CancellationTokenSource` linked with the caller's token, so user code observes cancel for both upstream caller intent and SDK-driven workflow teardown.

## Table of contents

1. [Motivation](#1-motivation)
2. [Goals and non-goals](#2-goals-and-non-goals)
3. [Public API changes](#3-public-api-changes)
4. [Internal scaffold](#4-internal-scaffold)
5. [Cancellation semantics](#5-cancellation-semantics)
6. [Replay and determinism](#6-replay-and-determinism)
7. [What is NOT cancellable](#7-what-is-not-cancellable)
8. [User-facing guidance](#8-user-facing-guidance)
9. [Phased plan](#9-phased-plan)
10. [Open questions](#10-open-questions)
11. [Out of scope](#11-out-of-scope)

---

## 1. Motivation

`IDurableContext` methods that take a user `Func` (`StepAsync`, `RunInChildContextAsync`, `WaitForCallbackAsync`, `WaitForConditionAsync`) accept a `CancellationToken` parameter that the SDK observes only in its *own* machinery — waiting on the result `Task`, retry-backoff `Task.Delay`s, checkpoint writes. The token never reaches the user-supplied `Func` body. Two consequences:

1. **Caller intent is silently dropped.** A user lambda invoked by an ASP.NET request handler or a host shutdown sequence has no way to forward its caller's `CancellationToken` into the step body. The token is accepted on the public API, then ignored.
2. **No clean teardown of inflight user code.** When the SDK decides to suspend (wait, callback pending, retry scheduled), the `Task.WhenAny` race in `DurableExecutionHandler.RunAsync` returns Pending and abandons the user `Task`. The abandoned `Task` keeps running on the threadpool until either it finishes naturally or Lambda freezes the process. During the window between `WhenAny` resolving and the freeze (checkpoint flush, response serialization, runtime API write), abandoned `HttpClient` calls and other side effects can still complete, and on the next invocation those orphaned operations may resume during a warm thaw.

In the single-threaded model shipping today, the second point is small — `TerminationManager.Terminate()` fires *after* the relevant operation's user code has already resolved. The first point is a real bug on the public surface. We are landing the change now so the breaking delegate signature ships in preview, before GA, and so the same hook is in place when parallel/child-context cancellation needs it.

## 2. Goals and non-goals

### Goals

- Every user `Func` accepted by `IDurableContext` receives a `CancellationToken` parameter.
- The token observes both the caller's `CancellationToken` (passed on each method) and an SDK-owned workflow `CancellationTokenSource`.
- The SDK's workflow CTS fires when `TerminationManager.Terminate()` resolves, so abandoned step bodies unwind via `OperationCanceledException` rather than running to completion in the background.
- Replay determinism is preserved: cached operations short-circuit before the user `Func` is invoked, so a cancelled or already-cancelled token cannot cause divergent re-execution.
- SDK-internal work (checkpoint serialization, runtime API writes, batcher flush) does **not** observe the workflow token — successful work is never lost to teardown.

### Non-goals

- No deadline timer. Lambda's own timeout is the deadline backstop; an SDK background timer that pre-emptively cancels user code adds magic-margin tuning we are not buying.
- No cancellation of `WaitAsync`, `CreateCallbackAsync`, or `InvokeAsync` user-side bodies — those operations do not run user `Func`s.
- No source-generator changes in this design. The `Amazon.Lambda.Annotations` source generator that emits the durable function entry point is updated separately.
- No support for resuming a cancelled workflow. Cancellation is workflow-fatal at the top level; the workflow either suspends per the standard termination flow or fails.
- No changes to the wire format, checkpoint shape, or `ExecutionState`.

## 3. Public API changes

Six `IDurableContext` methods change shape — each gains a `CancellationToken` parameter on its user-supplied `Func`. The trailing `CancellationToken cancellationToken = default` parameter on the method itself is unchanged.

```csharp
// Before
Task<T> StepAsync<T>(
    Func<IStepContext, Task<T>> func,
    string? name = null,
    StepConfig? config = null,
    CancellationToken cancellationToken = default);

// After
Task<T> StepAsync<T>(
    Func<IStepContext, CancellationToken, Task<T>> func,
    string? name = null,
    StepConfig? config = null,
    CancellationToken cancellationToken = default);
```

The same shape change applies to:

- `StepAsync` (void overload) — `Func<IStepContext, CancellationToken, Task>`
- `RunInChildContextAsync<T>` — `Func<IDurableContext, CancellationToken, Task<T>>`
- `RunInChildContextAsync` (void overload) — `Func<IDurableContext, CancellationToken, Task>`
- `WaitForCallbackAsync<T>` — `Func<string, IWaitForCallbackContext, CancellationToken, Task>`
- `WaitForConditionAsync<TState>` — `Func<TState, IConditionCheckContext, CancellationToken, Task<TState>>`

`WaitAsync`, `CreateCallbackAsync`, `InvokeAsync` and the `ConfigureLogger` / property surface are unchanged.

This is a **breaking** change to public delegate signatures. Every existing user lambda must add the parameter (or `_`). It is a major version bump per the change-file rules.

### Why a parameter, not a context property

A property on `IStepContext` (`step.CancellationToken`) is non-breaking and was considered. The Func-parameter shape was chosen because:

1. It is far more discoverable. The signature itself tells the user the token exists; a property requires reading docs.
2. It is consistent with .NET conventions for `Func` overloads that accept cancellation (e.g. `Channel.Reader.ReadAllAsync`, `Parallel.ForEachAsync`).
3. We are still in preview. The cost of changing it later, post-GA, is far higher than the cost of changing it now.

The trade-off: every existing test, doc example, and customer preview lambda needs the parameter added. That is paid once.

## 4. Internal scaffold

### New type — `WorkflowCancellation`

```csharp
// Libraries/src/Amazon.Lambda.DurableExecution/Internal/WorkflowCancellation.cs
internal sealed class WorkflowCancellation : IDisposable
{
    private readonly CancellationTokenSource _cts = new();

    public CancellationToken Token => _cts.Token;

    public WorkflowCancellation(TerminationManager terminationManager)
    {
        // When the SDK decides to suspend or abort the workflow, cancel.
        // Abandoned user Tasks (the WhenAny loser in DurableExecutionHandler)
        // unwind via OperationCanceledException instead of running to
        // completion on the threadpool while Lambda is mid-response.
        terminationManager.TerminationTask.ContinueWith(
            _ => { try { _cts.Cancel(); } catch (ObjectDisposedException) { } },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    public void Dispose() => _cts.Dispose();
}
```

One instance per durable function invocation. Lives alongside `TerminationManager`; constructed in the same place that constructs the `TerminationManager` (the entry point that calls `DurableExecutionHandler.RunAsync`).

### `DurableExecutionHandler.RunAsync` — owns the lifecycle

```csharp
internal static async Task<HandlerResult<TResult>> RunAsync<TResult>(
    ExecutionState executionState,
    TerminationManager terminationManager,
    WorkflowCancellation workflowCancellation,
    Func<Task<TResult>> userHandler) { /* race unchanged */ }
```

The caller constructs `WorkflowCancellation(terminationManager)` and disposes it after `RunAsync` returns. The `Task.Run(userHandler)` race is unchanged.

### `DurableContext` — accepts the workflow CTS, exposes a linker

```csharp
internal sealed class DurableContext : IDurableContext
{
    private readonly WorkflowCancellation _workflowCancellation;
    // ... existing fields ...

    public DurableContext(
        ExecutionState state,
        TerminationManager terminationManager,
        WorkflowCancellation workflowCancellation,   // new
        OperationIdGenerator idGenerator,
        string durableExecutionArn,
        ILambdaContext lambdaContext,
        CheckpointBatcher? batcher = null) { ... }
}
```

Each operation construction passes `_workflowCancellation` down to the operation class; the operation class is responsible for building the linked CTS at the point of user-`Func` invocation (see below). `DurableContext` itself does not build linked CTSes — it only forwards the `WorkflowCancellation`.

The child-context factory passes the **same** `WorkflowCancellation` to the child `DurableContext`. A child does not get an independent cancellation scope; cancelling the workflow cancels the child too.

### Operation classes — link at the user-`Func` boundary

`StepOperation<T>` is the canonical pattern. The same shape applies to `ChildContextOperation<T>`, `WaitForConditionOperation<T>`, and the inline submitter `Step` invocation inside `WaitForCallbackAsync`. `CallbackOperation` does not invoke a user `Func` and is unchanged.

```csharp
// inside StepOperation<T>.ExecuteAsync(callerToken)
using var linked = CancellationTokenSource.CreateLinkedTokenSource(
    callerToken,
    _workflowCancellation.Token);

// ... replay-cache short-circuit (returns cached SUCCESS without invoking _func) ...
// ... retry-loop unchanged ...

var stepCtx = new StepContext(operationId, attempt, scopedLogger);
try
{
    var result = await _func(stepCtx, linked.Token).ConfigureAwait(false);
    // checkpoint SUCCESS (uses CancellationToken.None — see §7)
    return result;
}
catch (OperationCanceledException oce) when (linked.IsCancellationRequested)
{
    // Cancellation: do NOT checkpoint FAIL, do NOT retry. Re-throw so the
    // termination signal owns the suspend/abort decision.
    throw;
}
catch (Exception ex)
{
    // Non-cancellation failure — existing path: checkpoint FAIL, apply
    // retry strategy, etc. Unchanged.
}
```

Two semantic points encoded above:

1. **`when (linked.IsCancellationRequested)` distinguishes our cancellation from a stray `OperationCanceledException` the user threw for unrelated reasons.** A user OCE thrown without our token cancelling falls through to the generic `catch` and is treated as a normal step failure (FAIL checkpoint + retry).
2. **A cancelled step is not checkpointed.** The next invocation will replay the operation from scratch (no SUCCESS, no FAIL) and either re-execute or, if the workflow is itself terminating, never reach this point.

### Void overload wrappers

`StepAsync(Func<IStepContext, CancellationToken, Task>)` and the void `RunInChildContextAsync` already wrap the user `Func` to return a synthetic `null`. The wrapper threads the token through:

```csharp
public async Task StepAsync(
    Func<IStepContext, CancellationToken, Task> func,
    string? name = null,
    StepConfig? config = null,
    CancellationToken cancellationToken = default)
{
    await RunStep<object?>(
        async (ctx, ct) => { await func(ctx, ct); return null; },
        name, config, cancellationToken);
}
```

### `WaitForCallbackAsync` — composed submitter

`WaitForCallbackAsync` composes a child context that runs `CreateCallbackAsync` + `StepAsync(submitter)` + `callback.GetResultAsync`. The submitter call propagates the token:

```csharp
await childCtx.StepAsync(
    async (stepCtx, ct) =>
    {
        var submitterCtx = new WaitForCallbackContext(stepCtx.Logger);
        await submitter(callback.CallbackId, submitterCtx, ct);
    },
    name: submitterName,
    config: stepConfig,
    cancellationToken: cancellationToken);
```

## 5. Cancellation semantics

The decision tree for an `OperationCanceledException` thrown out of a user `Func`:

| Workflow CTS fired? | Caller token fired? | Step body threw OCE? | Result |
|---|---|---|---|
| no | no | yes (user-thrown OCE, unrelated) | Treated as a normal step failure: FAIL checkpoint, retry per `RetryStrategy`. |
| no | yes | yes | Step is abandoned: no checkpoint written, OCE propagates up, the workflow's user-handler `Task` faults. The `WhenAny` race in `RunAsync` returns FAILED with the OCE as the cause. |
| yes | either | yes | Step is abandoned: no checkpoint written, OCE propagates up. The termination signal that cancelled the workflow CTS has already resolved `TerminationTask`, so `WhenAny` returns Pending (or Failed if termination carried an exception). The user OCE is observed by `userTask` but never reaches the handler result — the termination outcome wins. |

Implementation: `catch (OperationCanceledException) when (linked.IsCancellationRequested)` separates "our cancellation" from "user-thrown OCE." The latter falls through to the generic `catch (Exception)` path.

### Behavior of the workflow CTS over a workflow's lifetime

- Constructed at workflow-entry time, before `Task.Run(userHandler)`.
- Cancels exactly once, when `TerminationManager.TerminationTask` resolves (any reason). Termination's reason set today: `WaitScheduled`, `RetryScheduled`, `CallbackPending`, `InvokePending`, `CheckpointFailed`. The CTS does not distinguish reasons; user code observing cancel only knows "the workflow is being torn down."
- Disposed after `RunAsync` returns, in the same scope as the `TerminationManager`.

### Why "always cancel on termination" rather than "only on hard-abort reasons"

`TerminationManager.Terminate()` fires for both resumable suspensions (wait, callback pending, retry scheduled) and hard aborts (checkpoint failed). In every case the user `Task` is being abandoned — the operation that caused termination has already resolved its own result, and any other in-flight user code in the same `Task.Run` lineage is now dead weight. Cancelling them all gives:

- Cleaner threadpool: abandoned `HttpClient` calls release connections promptly.
- Less risk of orphaned side effects landing during the freeze window.
- Simpler model: one signal, one meaning.

The cost is small in the single-threaded model: today, `Terminate()` fires only after the user `Func` for the relevant operation has already returned, so there is rarely user code mid-await to cancel. The mechanism becomes load-bearing once parallel branches exist.

## 6. Replay and determinism

The cancellation token does not interact with replay state, by design. Specifics:

1. **Cached operations short-circuit before the user `Func` is invoked.** Each `*Operation.ExecuteAsync` checks `ExecutionState` for a SUCCESS checkpoint matching the deterministic operation ID and returns the cached result without ever building the linked CTS or calling `_func`. A cancelled token cannot cause divergent re-execution because the user code never runs.
2. **The workflow CTS is per-invocation and fresh on replay.** Invocation N's CTS state is not reconstructed on N+1. User code that branches on `IsCancellationRequested` could in principle observe different values across replays of the same logical step. This is a misuse — see §8 — and is documented, not engineered around.
3. **Termination fires after, not during, user-`Func` execution in single-threaded mode.** Today, the termination signal that cancels the workflow CTS is raised by an operation that has already resolved its own result. In single-threaded code, the `Task.Run` user task is not concurrently awaiting anything else when termination fires. So in single-threaded land, the workflow CTS rarely interrupts an in-progress user `Func` body — its observable effect is propagating the **caller's** `CancellationToken` into user code. The mechanism becomes load-bearing for parallel.
4. **A cancelled step does not produce a checkpoint.** No SUCCESS, no FAIL. The next invocation replays the operation from scratch — either re-executes the body, or never reaches the operation because the workflow itself is terminating.

## 7. What is NOT cancellable

The workflow CTS is for **user-side I/O only**. The following code paths must complete even when the workflow is being torn down, and therefore must **not** observe the workflow token:

- Checkpoint serialization and the runtime API write (the SDK's call to record SUCCESS/FAIL after a user step body resolves).
- `CheckpointBatcher` flush.
- Construction of the response payload returned to RuntimeSupport.
- Any `LambdaSerializerHelper` invocation that serializes a step result before checkpointing.

Implementation rule: code on these paths uses `CancellationToken.None` for any cancellation parameter, never the workflow token. A test verifies that a step that succeeds and is then cancelled (workflow CTS fires after `_func` returns successfully) still has its SUCCESS checkpoint persisted.

## 8. User-facing guidance

The following are documented as misuses in `docs/core/steps.md`, `child-contexts.md`, `callbacks.md`, and `wait.md`:

- **Do not branch workflow logic on `IsCancellationRequested`.** It is a runtime concern, not a workflow concern. Branching on it makes the workflow non-deterministic across replays.
- **Do not catch `OperationCanceledException` thrown on the workflow token and continue.** If the workflow is being torn down, continued work is wasted. If the caller cancelled, the user expects unwind. Either swallow-and-rethrow, or do not catch.
- **Do pass `step.CancellationToken` into every cancellation-aware API call inside the step body** (`HttpClient.SendAsync(ct)`, `Task.Delay(ct)`, AWS SDK calls). This is what makes deadline propagation and caller-token propagation actually work.

## 9. Phased plan

### Phase 1 — internal plumbing (no public API changes)

1. Add `WorkflowCancellation` (Internal/).
2. Construct `WorkflowCancellation(terminationManager)` in the entry point that today constructs `TerminationManager`. Add as a new parameter to `DurableExecutionHandler.RunAsync`.
3. Add `WorkflowCancellation` to the `DurableContext` constructor. Forward to operation classes (no behavior change yet — operations ignore it).
4. Unit test: `WorkflowCancellation.Token.IsCancellationRequested` becomes `true` after `terminationManager.Terminate(...)` resolves; remains `false` until then.

### Phase 2 — operation classes link and pass through

5. Each operation class that invokes a user `Func` (`StepOperation`, `ChildContextOperation`, `WaitForConditionOperation`, the inline submitter step in `WaitForCallbackAsync`) accepts `WorkflowCancellation` via its constructor.
6. Inside `ExecuteAsync`, build `using var linked = CancellationTokenSource.CreateLinkedTokenSource(callerToken, _workflowCancellation.Token);` and pass `linked.Token` into the user `Func`.
7. Add the cancellation-aware exception path: `catch (OperationCanceledException) when (linked.IsCancellationRequested) { throw; }` — no FAIL checkpoint, no retry.
8. Verify SDK-internal paths (checkpoint write, batcher flush, response build) continue to use `CancellationToken.None` and never the linked or workflow token.

### Phase 3 — public Func signatures

9. Update `IDurableContext` (six methods) to accept the new Func shape.
10. Update `DurableContext` to match. The internal `RunStep`, `RunChildContext`, `RunWaitForCallback` glue threads the new parameter into the operation classes.
11. Update the void-step and void-child-context wrappers to forward the token.
12. Update `WaitForCallbackAsync`'s composed submitter call to pass the token.

### Phase 4 — tests

13. Update every existing test that passes a `Func` body — add `_` or `ct`.
14. New tests:
    - Caller's token fires → user `Func` observes cancel via `linked.Token`.
    - `terminationManager.Terminate(WaitScheduled)` while user `Func` is mid-await → user `Func` observes cancel.
    - User-thrown `OperationCanceledException` (without our token cancelling) is treated as a normal step failure and retried per the `RetryStrategy`.
    - Cancelled step writes no checkpoint (neither SUCCESS nor FAIL).
    - Successful step that races with workflow cancel still writes its SUCCESS checkpoint (the §7 invariant).
    - Replay path: cached step result returns without invoking the user `Func` even when the workflow token is already cancelled.
    - Child context propagates the workflow CTS to its inner `IDurableContext`; cancelling the workflow cancels in-flight child operations.
    - `WaitForConditionAsync` check function receives the linked token.
    - `WaitForCallbackAsync` submitter receives the linked token.

### Phase 5 — docs and change file

15. Update XML doc on every changed `IDurableContext` Func parameter to describe the linked-token contract (caller token + SDK shutdown signal).
16. Update `docs/core/steps.md`, `child-contexts.md`, `callbacks.md`, `wait.md` examples to take and forward the token.
17. Add a new short doc `docs/core/cancellation.md` covering the §8 guidance.
18. `autover change` — major increment for `Amazon.Lambda.DurableExecution`. Changelog message names the breaking delegate-signature change explicitly so preview users see it.

## 10. Open questions

1. **Should termination always cancel, or only for hard-abort reasons?** Current decision: always (see §5). Worth flagging to reviewers in case the parallel design wants to distinguish "we're suspending, sibling branches should stop" from "we're aborting, sibling branches must stop."
2. **Should `DurableContext` expose `WorkflowCancellation.Token` as a property on `IDurableContext`** (e.g. `IDurableContext.CancellationToken`) for advanced users who want to observe workflow-wide cancel without being inside an operation? Defer until a concrete use case appears; adding it later is non-breaking.
3. **`InvokeAsync` and the workflow CTS.** `InvokeAsync` does not accept a user `Func`, but it does fire an outbound durable-service call. §7 says runtime API writes do not observe the workflow token — the same rule should apply here so an in-flight invoke is not torn down mid-call once we have already committed an INVOKE START checkpoint. The caller's `cancellationToken` parameter is honored as today (synchronous `ThrowIfCancellationRequested` before the call); the workflow CTS is not linked. Confirm at implementation time.

## 11. Out of scope

- **The `Amazon.Lambda.Annotations` source generator.** Once Phases 1–3 land, the generator's emitted entry-point wrapper passes a workflow `CancellationToken` into the user's top-level handler. That is a separate change and design.
- **Parallel branches and map operations.** Their cancellation rides on the same `WorkflowCancellation`, but the semantic decisions (one branch failure cancels siblings? failure modes carried in error-aggregate?) are owned by the parallel design.
- **Lambda deadline timer.** Considered and rejected (see §2 non-goals). If we later decide deadline-aware cancel is worth it, it will be added as an explicit `Terminate` reason raised by code that owns the deadline policy, not as a generic background timer in `WorkflowCancellation`.
- **A way to resume a cancelled workflow.** Cancellation is workflow-fatal at the top level.
- **Wire format, checkpoint shape, or `ExecutionState` changes.** None.
