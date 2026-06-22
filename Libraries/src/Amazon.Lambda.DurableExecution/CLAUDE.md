# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

`Amazon.Lambda.DurableExecution` is the .NET SDK (preview, 0.x) for resilient, long-running AWS Lambda
workflows that checkpoint progress after each step and resume after failures or waits. A workflow can run
for up to ~1 year (the WAIT cap is 31,622,400 seconds) and is only billed for active compute. The SDK is
client-side glue: the *durable execution service* (part of Lambda) owns the checkpoint store, fires timers,
and re-invokes the function; this library re-derives in-memory workflow position from the checkpoint history
the service sends on each invocation. See sibling SDKs (Python/JS/Java) listed in `README.md` for the shared
model — this SDK deliberately mirrors their semantics.

## Build & test

Targets `net8.0;net10.0` (`DefaultPackageTargets` in `buildtools/common.props`). `TreatWarningsAsErrors` is on
everywhere, and the main library is `IsTrimmable` with the trim analyzer enabled — keep new code AOT/trim-clean.

```bash
# Build the library (run from this directory)
dotnet build

# Unit tests (fast, no AWS). Project: Libraries/test/Amazon.Lambda.DurableExecution.Tests
dotnet test ../../test/Amazon.Lambda.DurableExecution.Tests/Amazon.Lambda.DurableExecution.Tests.csproj

# A single test
dotnet test ../../test/Amazon.Lambda.DurableExecution.Tests/Amazon.Lambda.DurableExecution.Tests.csproj \
  --filter "FullyQualifiedName~StepOperationTests"

# Coverage report (requires reportgenerator tool)
../../test/Amazon.Lambda.DurableExecution.Tests/coverage.sh
```

Unit tests reach `internal` types via `InternalsVisibleTo` (declared in the `.csproj`). They use
`Amazon.Lambda.TestUtilities` (`TestLambdaContext`) and the real `SourceGeneratorLambdaJsonSerializer` —
set `TestLambdaContext.Serializer` so `LambdaSerializerHelper.GetRequired` finds one.

### Integration tests (expensive, real AWS)

`Libraries/test/Amazon.Lambda.DurableExecution.IntegrationTests` deploys real Lambdas. Each test builds a
`TestFunctions/<X>/` project with **`dotnet publish` (framework-dependent, linux-x64)**, zips the publish
output, and deploys it as a **zip package on the managed `dotnet10` runtime** (executable model,
`Handler=bootstrap`) — no Docker or ECR. `DurableFunctionDeployment` creates an IAM role + Lambda (with
`DurableConfig` and JSON `LoggingConfig`), invokes it, and tears everything down on dispose.
Requires only the .NET SDK + AWS creds (us-east-1); no Docker. Slow, but no container build. Every behavior
in `docs/` should have a paired integration test under that project. Prefix AWS commands with
`unset AWS_PROFILE` to use `[default]` creds.

**Run integration tests against `net10.0`.** The project multi-targets `net8.0;net10.0`; `dotnet test`
without a framework spins up one testhost per TFW and runs them concurrently, which races two processes on
the same `TestFunctions/<X>/` build dir. Pin the framework:

```bash
dotnet test ../../test/Amazon.Lambda.DurableExecution.IntegrationTests/Amazon.Lambda.DurableExecution.IntegrationTests.csproj \
  -f net10.0 --filter "FullyQualifiedName~MultipleStepsTest"
```

## Architecture: the replay model

This is the part you must understand before changing anything. Read these together:
`DurableFunction.cs`, `DurableExecutionHandler.cs`, `DurableContext.cs`, `Internal/DurableOperation.cs`,
`Internal/ExecutionState.cs`, `Internal/OperationIdGenerator.cs`, `Internal/TerminationManager.cs`.

**Entry point.** The user's Lambda handler delegates to `DurableFunction.WrapAsync<TInput,TOutput>`, which:
hydrates `ExecutionState` from `invocationInput.InitialExecutionState` (paging the service via `NextMarker`),
extracts the user payload from the `EXECUTION`-type op, builds a `CheckpointBatcher` + `DurableContext`, runs
the workflow through `DurableExecutionHandler.RunAsync`, drains checkpoints, and maps the result to a
`DurableExecutionInvocationOutput` with status **Succeeded / Failed / Pending**.

**Each operation runs the same workflow code every invocation.** There is no persisted program counter.
On re-invocation the user function executes from the top again; each durable call (`StepAsync`, `WaitAsync`,
etc.) looks up its own checkpoint and either replays the cached result or runs fresh. This is why workflow
code **must be deterministic** — same operations, same order, same names across deployments.

**Deterministic operation IDs** (`OperationIdGenerator`). Each durable call gets an ID = SHA-256 of
`"<parentPrefix>-<counter>"`, where the counter is per-context and pre-incremented. The same workflow position
yields the same opaque ID across replays, so a checkpoint correlates to a call by *position*, not by name —
renaming a step does **not** break replay (the human name rides separately on `OperationUpdate.Name`).
Reordering or adding/removing calls *does* break it. `ValidateReplayConsistency` enforces this and throws
`NonDeterministicExecutionException` on type/name drift.

**Suspension is implemented by never completing a Task** (`TerminationManager` + `DurableExecutionHandler`).
When an op must suspend (wait timer, scheduled retry, pending callback/invoke) it calls
`Termination.SuspendAndAwait<T>()`, which trips a one-shot signal and returns a Task that *never resolves*.
`RunAsync` runs the user code via `Task.Run` and races it against `TerminationTask` with `Task.WhenAny`:
- user task wins → **Succeeded** (or **Failed** if it threw)
- termination wins → **Pending**; the abandoned user task is GC'd, checkpoints flush, the service fires the
  timer and re-invokes. On replay the suspended op sees its now-terminal checkpoint and returns normally.

**Operation classes** (`Internal/*Operation.cs`) all extend `DurableOperation<TResult>`. The base's
`ExecuteAsync` does: `ValidateReplayConsistency` → `TrackReplay` → look up checkpoint → dispatch to
`StartAsync` (no prior checkpoint) or `ReplayAsync` (checkpoint exists). `StepOperation` is the canonical
example — read its class doc comment for the full status decision table (Succeeded→cached, Failed→rethrow,
Pending→re-suspend if retry timer hasn't fired, Started→crash-recovery under `AtMostOncePerRetry`,
Ready→run next attempt). `DurableContext` is a thin dispatcher: it allocates the op ID, pulls the serializer
off `ILambdaContext.Serializer`, constructs the right `*Operation`, and calls `ExecuteAsync`.

**Checkpointing** (`CheckpointBatcher`). Outbound `OperationUpdate`s (START/SUCCEED/FAIL/RETRY) are enqueued
to a background channel worker that batches and flushes them via `LambdaDurableServiceClient` (which wraps
the `AWSSDK.Lambda` `Checkpoint`/`GetExecutionState` calls). `EnqueueAsync` awaits its batch's flush
(sync semantics); fire-and-forget callers (e.g. the START checkpoint under the default
`AtLeastOncePerRetry`) don't await but must observe the Task's exception. Flush errors become a terminal
error rethrown by the next `EnqueueAsync`/`DrainAsync`. `DurableFunction.IsTerminalCheckpointError`
classifies SDK errors on the final drain: 4xx (except 429 and stale-token) → **Failed** envelope; 429/5xx/
network → let it escape so Lambda retries the whole invocation.

**Replay-mode tracking** (`ExecutionState`). `IsReplaying` starts true iff any completed non-`EXECUTION` op
exists; `TrackReplay` decrements as each is visited and flips to false once the workflow catches up to the
frontier. `ReplayAwareLogger` uses this to suppress log lines emitted during replay so a 30-step workflow
re-invoked 30 times logs each line once — **always use `ctx.Logger`**, never `Console.WriteLine`.
`ExecutionState` is lock-guarded because the batcher worker thread and concurrent parallel/map branches all
touch it.

### Operations surface (`IDurableContext`)

`StepAsync` (checkpointed code + retries), `WaitAsync` (1s–~1yr timer), `RunInChildContextAsync` (isolated
sub-workflow checkpointed as one `CONTEXT` op), `CreateCallbackAsync` / `WaitForCallbackAsync` (external
events; `WaitForCallback` is *composed* from child-context + callback + submitter step — see
`DurableContext.RunWaitForCallback`), `InvokeAsync` (durable-to-durable chained invoke, qualified ARN
required), and `ParallelAsync` / `MapAsync` (concurrent branches → `IBatchResult<T>`).

**Nesting (`NestingType`)** matters for parallel/map. `Nested` (default) gives each branch a full `CONTEXT`
checkpoint. `Flat` runs branches in *virtual* contexts that emit no `CONTEXT` op — inner ops re-parent to the
parallel/map op via `OperationIdGenerator.CreateVirtualChild(operationId, reportedParentId)`, trading trace
granularity for fewer checkpoints. The `idPrefix` vs `reportedParentId` split is the subtle part: inner IDs
always derive from the branch's own op ID (so siblings never collide), but are *reported* under the nearest
non-virtual ancestor (so they reference a parent that actually exists in the checkpoint store).

### Wire format (`Operation.cs`)

`Operation` and its `*Details` types mirror the service envelope JSON exactly (`[JsonPropertyName]`).
String constants live in `OperationTypes` (STEP/WAIT/CALLBACK/CHAINED_INVOKE/CONTEXT/EXECUTION),
`OperationStatuses` (STARTED/SUCCEEDED/FAILED/PENDING/READY/CANCELLED/STOPPED/TIMED_OUT), and
`OperationSubTypes` (PascalCase finer classifier). Plural type names (`OperationTypes`, not `OperationType`)
intentionally avoid collision with `AWSSDK.Lambda` model enums.

## Conventions

- **Programming model:** both the *executable* model (`Main` builds a `LambdaBootstrap` with a handler
  wrapper and an `ILambdaSerializer`) and the *class-library* model on the managed `dotnet10` runtime (a
  plain `Handler` method, serializer via `[assembly: LambdaSerializer(...)]`, deployed with an
  `Assembly::Type::Method` handler string) are supported and have integration coverage (`ClassLibraryTest`
  + `TestFunctions/ClassLibraryFunction`). The serializer is read off `ILambdaContext.Serializer`
  (a preview API; the project-wide `AWSLAMBDA001` suppression in the `.csproj` is intentional for that
  reason). All step/result/payload (de)serialization flows through that one registered serializer, so AOT
  and reflection callers share a single code path — there is no per-call `JsonSerializerContext` argument.
- **Errors:** durable exceptions carry `ErrorType`/`ErrorData`/`OriginalStackTrace` so a failure can be
  reconstructed on replay when the live exception object is gone. `StepException`, `ChildContextException`,
  `CallbackFailedException`/`CallbackTimeoutException`/`CallbackSubmitterException`, `ParallelException`,
  `MapException`, and `NonDeterministicExecutionException` all derive from `DurableExecutionException`.
  When adding error-mapping logic, handle *both* the fresh path (`InnerException` is the live exception) and
  the replay path (`InnerException` is null, `ErrorType` carries the type string) — see
  `DurableContext.MapWaitForCallbackException` for the pattern.
- **Public config types** (`StepConfig`, `WaitForCallbackConfig`, `ParallelConfig`, `MapConfig`,
  `CompletionConfig`, etc.) are nullable optional args; resolve to an effective config inside the dispatcher.
- Inclusive language is enforced repo-wide (see the user's global rules): no master/slave, whitelist/blacklist.
