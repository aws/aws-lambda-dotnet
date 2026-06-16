# Amazon.Lambda.DurableExecution.Testing — Design

> Status: design (2026-06-09). Companion to [`Docs/durable-execution-design.md`](../../../../../Docs/durable-execution-design.md). Implementation plan to follow.

A separate NuGet package (`Amazon.Lambda.DurableExecution.Testing`) that lets developers test their durable functions without deploying to AWS. Ships a programmatic `DurableTestRunner<TIn, TOut>` for in-process unit testing and a `CloudDurableTestRunner<TIn, TOut>` for integration tests against deployed functions, with the same inspection API on both.

## Table of contents

1. [Goals and non-goals](#1-goals-and-non-goals)
2. [Architecture](#2-architecture)
3. [Public API surface](#3-public-api-surface)
4. [Local runner internals](#4-local-runner-internals)
5. [Cloud runner internals](#5-cloud-runner-internals)
6. [Step inspection model](#6-step-inspection-model)
7. [Safety, errors, edge cases](#7-safety-errors-edge-cases)
8. [Testing the testing SDK](#8-testing-the-testing-sdk)
9. [Cross-SDK comparison](#9-cross-sdk-comparison)
10. [Implementation summary](#10-implementation-summary)

---

## 1. Goals and non-goals

### Goals

- Drive a `[DurableExecution]` handler to a terminal state in-process, with no AWS network calls.
- Match the cross-SDK promise: the same assertions a developer writes against the local runner port to the cloud runner with only a runner-type swap.
- Exercise the **real** runtime engine — `DurableFunction.WrapAsync`, `ExecutionState`, `CheckpointBatcher`, `TerminationManager`, the configured `ILambdaSerializer` — so tests catch behavior changes the runtime introduces.
- Cover every operation kind shipped in v1 of `Amazon.Lambda.DurableExecution`: step, wait, parallel, map, child context, callback, chained invoke, wait-for-condition.
- Surface a per-step inspection API rich enough to assert on results, errors, retry attempts, and parallel/map branch outcomes.

### Non-goals

- Not a parallel reimplementation of the orchestration engine. The testing package never reaches into `ExecutionState` directly or replays operations on its own.
- Not a UI. Web emulators (Test Tool v2 / Aspire dashboard) are post-v1 work tracked separately and reuse the engine surfaces this package exercises.
- No history-events polling mode for the cloud runner — the operations API is sufficient.
- No live-during-execution `runner.GetOperation(name)` API — the two-call (`StartAsync` + `WaitForCallbackAsync` + `WaitForResultAsync`) shape covers live-callback inspection without expanding the runner's surface.
- No support for testing `[DurableExecution]` source-generator output that doesn't match `Func<TIn, IDurableContext, Task<TOut>>` — the source generator must emit that shape (it does today).

---

## 2. Architecture

### Package layout

| Package | Targets | Public types added |
|---|---|---|
| `Amazon.Lambda.DurableExecution` (existing) | `$(DefaultPackageTargets)` → `net8.0;net10.0` | none — only an `internal` interface and `internal` `WrapAsync` overload |
| `Amazon.Lambda.DurableExecution.Testing` (new) | `$(DefaultPackageTargets)` → `net8.0;net10.0` | `DurableTestRunner<TIn,TOut>`, `CloudDurableTestRunner<TIn,TOut>`, `IDurableTestRunner<TIn,TOut>`, `TestResult<TOut>`, `TestStep`, `TestRunnerOptions`, `CloudTestRunnerOptions`, `OperationKind`, `OperationStatus`, `TestExecutionFailedException`, `TestExecutionLimitException`, `UnregisteredSiblingFunctionException`, `CloudTestException` |

The new testing package depends on:

- `Amazon.Lambda.DurableExecution` (project reference) — required for `IDurableContext`, `DurableFunction.WrapAsync`, the operation types, and the internal `IDurableServiceClient` seam (visible via `InternalsVisibleTo`).
- `Amazon.Lambda.TestUtilities` (project reference) — `TestLambdaContext`, `TestLambdaLogger` for the runner's `ILambdaContext` substitute.
- `Amazon.Lambda.Serialization.SystemTextJson` (package reference) — `DefaultLambdaJsonSerializer` is the fallback when `TestRunnerOptions.Serializer` is null.

### Interception strategy: `IDurableServiceClient` seam

The runtime SDK already isolates outbound durable RPCs behind a single class — `LambdaDurableServiceClient`, currently `internal sealed`. We promote that class to implement an `internal IDurableServiceClient` interface and inject a fake implementation from the testing package. The orchestration loop in `DurableFunction.WrapAsync` runs unmodified; only the two outbound RPCs (`CheckpointAsync`, `GetExecutionStateAsync`) are swapped. This keeps the testing-package surface tiny (two methods to fake) and exercises the **real** runtime engine — replay logic, checkpoint batching, termination handling, serializer dispatch — on every test.

Three changes to the runtime package — all `internal`, no public-API impact:

```csharp
// New, in Amazon.Lambda.DurableExecution/Services/IDurableServiceClient.cs
internal interface IDurableServiceClient
{
    Task<string?> CheckpointAsync(
        string durableExecutionArn,
        string? checkpointToken,
        IReadOnlyList<OperationUpdate> pendingOperations,
        Action<IReadOnlyList<Operation>>? onNewOperations = null,
        CancellationToken cancellationToken = default);

    Task<(List<Operation> Operations, string? NextMarker)> GetExecutionStateAsync(
        string durableExecutionArn,
        string? checkpointToken,
        string marker,
        CancellationToken cancellationToken = default);
}

// Existing class, now declares the interface
internal sealed class LambdaDurableServiceClient : IDurableServiceClient { /* unchanged body */ }

// New internal overload alongside the existing public ones
internal static Task<DurableExecutionInvocationOutput> WrapAsync<TInput, TOutput>(
    Func<TInput, IDurableContext, Task<TOutput>> workflow,
    DurableExecutionInvocationInput invocationInput,
    ILambdaContext lambdaContext,
    IDurableServiceClient serviceClient);

// AssemblyInfo addition
[InternalsVisibleTo("Amazon.Lambda.DurableExecution.Testing, PublicKey=...")]
```

Public `WrapAsync` overloads (the four that take `IAmazonLambda` or default to one) remain byte-identical. Existing customers see no change.

### Runtime flow under test

```
Test code
   │
   ▼
new DurableTestRunner<OrderEvent, OrderResult>(
    handler: orderFn.Handler,
    options: new TestRunnerOptions { SkipTime = true, MaxInvocations = 100 })
   │
   ▼
DurableTestRunner.RunAsync(input, timeout)
   │
   │ orchestration loop
   ▼
┌────────────────────────────────────────────────────┐
│ while (output.Status == Pending && i < Max) {       │
│   build DurableExecutionInvocationInput from        │
│     in-memory state (operations + checkpoint token) │
│                                                     │
│   output = await DurableFunction.WrapAsync(         │
│     workflow, input, ctx, fakeServiceClient)        │  ← internal overload
│                                                     │
│   advance time / mint callback IDs / route          │
│     CHAINED_INVOKE to registered sibling handlers   │
│                                                     │
│   i++                                               │
│ }                                                   │
│                                                     │
│ return TestResult.From(output, in-memory ops)       │
└────────────────────────────────────────────────────┘
   │
   │ uses
   ▼
InMemoryDurableServiceClient : IDurableServiceClient
   ├── CheckpointAsync(...)        — accumulate updates, mint callback IDs, advance waits
   └── GetExecutionStateAsync(...) — return paginated in-memory operations
```

Because the seam is the service client, the orchestration loop drives the **real** runtime engine — every replay-consistency check, every operation-id allocation, every batch-flush boundary that ships in production code is exercised by every test.

### Why an interface and not a broader fake

`IDurableServiceClient` exposes only the two methods the runtime needs to talk to the durable execution service. A test fake implements those two methods; everything else stays in the production engine. This is the same shape both reference SDKs (Python's `DurableServiceClient`, JavaScript's `CheckpointApiClient`) settled on. The decoupling from AWSSDK request/response shapes pays off when AWSSDK adds a new durable RPC: the interface is a contract we own, and the runtime keeps mapping AWSSDK shapes to our own `Operation` / `OperationUpdate` types in one place (`LambdaDurableServiceClient`), unchanged.

---

## 3. Public API surface

### `DurableTestRunner<TInput, TOutput>` (local)

```csharp
namespace Amazon.Lambda.DurableExecution.Testing;

public sealed class DurableTestRunner<TInput, TOutput> : IDurableTestRunner<TInput, TOutput>, IAsyncDisposable
{
    public DurableTestRunner(
        Func<TInput, IDurableContext, Task<TOutput>> handler,
        TestRunnerOptions? options = null);

    // Single-shot — drives the workflow to completion. Throws if the workflow needs callbacks.
    public Task<TestResult<TOutput>> RunAsync(
        TInput input,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);

    // Two-call shape — for workflows that use WaitForCallbackAsync.
    public Task<string> StartAsync(
        TInput input,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);

    public Task<string> WaitForCallbackAsync(
        string durableExecutionArn,
        string? name = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);

    public Task SendCallbackSuccessAsync<TResult>(
        string callbackId,
        TResult result,
        CancellationToken cancellationToken = default);

    public Task SendCallbackFailureAsync(
        string callbackId,
        ErrorObject? error = null,
        CancellationToken cancellationToken = default);

    public Task SendCallbackHeartbeatAsync(
        string callbackId,
        CancellationToken cancellationToken = default);

    public Task<TestResult<TOutput>> WaitForResultAsync(
        string durableExecutionArn,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);

    // Sibling-function routing for InvokeAsync.
    public DurableTestRunner<TInput, TOutput> RegisterFunction<TPayload, TResult>(
        string functionNameOrArn,
        Func<TPayload, ILambdaContext, Task<TResult>> handler);

    public DurableTestRunner<TInput, TOutput> RegisterDurableFunction<TPayload, TResult>(
        string functionNameOrArn,
        Func<TPayload, IDurableContext, Task<TResult>> handler);

    public ValueTask DisposeAsync();
}
```

### `CloudDurableTestRunner<TInput, TOutput>` (cloud)

Same shape — test code that targets local re-points to cloud just by swapping the runner type.

```csharp
public sealed class CloudDurableTestRunner<TInput, TOutput>
    : IDurableTestRunner<TInput, TOutput>, IAsyncDisposable
{
    public CloudDurableTestRunner(
        string functionArn,                       // qualified: ":alias", ":$LATEST", or ":N"
        IAmazonLambda? lambdaClient = null,
        CloudTestRunnerOptions? options = null);

    public Task<TestResult<TOutput>> RunAsync(TInput input, TimeSpan? timeout = null, CancellationToken ct = default);
    public Task<string> StartAsync(TInput input, TimeSpan? timeout = null, CancellationToken ct = default);
    public Task<string> WaitForCallbackAsync(string durableExecutionArn, string? name = null, TimeSpan? timeout = null, CancellationToken ct = default);
    public Task SendCallbackSuccessAsync<TResult>(string callbackId, TResult result, CancellationToken ct = default);
    public Task SendCallbackFailureAsync(string callbackId, ErrorObject? error = null, CancellationToken ct = default);
    public Task SendCallbackHeartbeatAsync(string callbackId, CancellationToken ct = default);
    public Task<TestResult<TOutput>> WaitForResultAsync(string durableExecutionArn, TimeSpan? timeout = null, CancellationToken ct = default);

    public ValueTask DisposeAsync();
}
```

`RegisterFunction` is omitted because cloud invokes the real deployed sibling.

### `IDurableTestRunner<TInput, TOutput>` (the cross-SDK contract)

```csharp
public interface IDurableTestRunner<TInput, TOutput>
{
    Task<TestResult<TOutput>> RunAsync(TInput input, TimeSpan? timeout = null, CancellationToken ct = default);
    Task<string> StartAsync(TInput input, TimeSpan? timeout = null, CancellationToken ct = default);
    Task<string> WaitForCallbackAsync(string durableExecutionArn, string? name = null, TimeSpan? timeout = null, CancellationToken ct = default);
    Task SendCallbackSuccessAsync<TResult>(string callbackId, TResult result, CancellationToken ct = default);
    Task SendCallbackFailureAsync(string callbackId, ErrorObject? error = null, CancellationToken ct = default);
    Task SendCallbackHeartbeatAsync(string callbackId, CancellationToken ct = default);
    Task<TestResult<TOutput>> WaitForResultAsync(string durableExecutionArn, TimeSpan? timeout = null, CancellationToken ct = default);
}
```

Tests written against the interface run unchanged against either runner.

### Options

```csharp
public sealed record TestRunnerOptions
{
    public bool SkipTime { get; init; } = true;
    public int MaxInvocations { get; init; } = 100;
    public TimeSpan DefaultTimeout { get; init; } = TimeSpan.FromSeconds(30);
    public ILambdaSerializer? Serializer { get; init; }      // null → DefaultLambdaJsonSerializer from
                                                             //         Amazon.Lambda.Serialization.SystemTextJson
                                                             //         (added as a package dependency)
    public ILoggerFactory? LoggerFactory { get; init; }
    public string DurableExecutionArn { get; init; }
        = "arn:aws:lambda:us-east-1:123456789012:execution:test-fn:test-execution";
}

public sealed record CloudTestRunnerOptions
{
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(2);
    public TimeSpan DefaultTimeout { get; init; } = TimeSpan.FromMinutes(5);
    public ILambdaSerializer? Serializer { get; init; }
    public InvocationType InvocationType { get; init; } = InvocationType.RequestResponse;
}
```

### Example — callback-free workflow

```csharp
await using var runner = new DurableTestRunner<OrderEvent, OrderResult>(
    handler: new Function().Handler,
    options: new TestRunnerOptions { SkipTime = true });

runner.RegisterFunction<PaymentReq, PaymentResp>(
    "process-payment", new PaymentFunction().Handler);

var result = await runner.RunAsync(new OrderEvent { OrderId = "order-123" });
result.EnsureSucceeded();
Assert.Equal("approved", result.Result!.Status);

var validate = result.GetStep("validate_order");
Assert.Equal(OperationKind.Step, validate.Kind);
Assert.True(validate.GetResult<ValidationResult>()!.IsValid);
```

### Example — workflow with `WaitForCallbackAsync`

```csharp
await using var runner = new DurableTestRunner<ApprovalRequest, ApprovalResult>(
    handler: new Function().Handler);

var arn = await runner.StartAsync(new ApprovalRequest { OrderId = "order-123" });
var callbackId = await runner.WaitForCallbackAsync(arn, name: "approve");

await runner.SendCallbackSuccessAsync(callbackId, new ApprovalDecision { Approved = true });

var result = await runner.WaitForResultAsync(arn);
result.EnsureSucceeded();
Assert.True(result.Result!.Approved);
```

---

## 4. Local runner internals

### Component map

```
DurableTestRunner<TIn,TOut>
   │
   ├── _store: InMemoryOperationStore                         (the source of truth)
   │     ├── operations: Dictionary<string, Operation>        (keyed by op id, insertion-ordered)
   │     ├── checkpointToken: string                          (incremented per checkpoint)
   │     ├── completedExecutions: Dictionary<arn, output>     (for WaitForResultAsync)
   │     ├── pendingCallbacks: Dictionary<callbackId, opId>   (for SendCallback*)
   │     └── waitingCallbacks: Dictionary<arn, ...>           (per-name + anonymous)
   │
   ├── _serviceClient: InMemoryDurableServiceClient           (implements IDurableServiceClient)
   │     └── delegates to CheckpointProcessor + _store
   │
   ├── _checkpointProcessor: CheckpointProcessor              (applies updates; mints callback IDs;
   │                                                           advances waits if SkipTime)
   │
   ├── _registry: FunctionRegistry                            (sibling handlers for InvokeAsync)
   │
   └── _orchestrator: ExecutionOrchestrator                   (the drive-to-terminal loop)
```

### `InMemoryDurableServiceClient`

```csharp
internal sealed class InMemoryDurableServiceClient : IDurableServiceClient
{
    private readonly InMemoryOperationStore _store;
    private readonly CheckpointProcessor _processor;

    public Task<string?> CheckpointAsync(
        string arn, string? token,
        IReadOnlyList<OperationUpdate> updates,
        Action<IReadOnlyList<Operation>>? onNewOperations,
        CancellationToken ct)
    {
        var (newToken, newOps) = _processor.Process(arn, token, updates);
        if (onNewOperations is not null && newOps.Count > 0) onNewOperations(newOps);
        return Task.FromResult<string?>(newToken);
    }

    public Task<(List<Operation>, string?)> GetExecutionStateAsync(
        string arn, string? token, string marker, CancellationToken ct)
    {
        return Task.FromResult<(List<Operation>, string?)>(
            (_store.GetAllOperations(arn).ToList(), null));   // single page in v1
    }
}
```

### `CheckpointProcessor` — what runs on every checkpoint

Roles, in order:

1. **Validate token.** Reject stale tokens with `InvalidParameterValueException` so the runtime's "transient checkpoint token" carve-out fires correctly.
2. **Apply updates.** For each `OperationUpdate`, write/merge the corresponding `Operation` into the store.
3. **Mint callback IDs.** When an update is `CALLBACK START`, generate a `callbackId` deterministic from the operation id (e.g., `"cb-{operationId}"` or a hash thereof — implementer's choice; the only contract is uniqueness within a single execution and stability across re-invocations within the same test run). Stamp it on `CallbackDetails`, record the mapping in `pendingCallbacks`.
4. **Advance time-bound ops** if `SkipTime`:
   - `WAIT START` → fold to `WAIT SUCCEEDED` immediately, with `ScheduledEndTimestamp = now`.
   - `STEP RETRY` (with `NextAttemptTimestamp`) → fold to `STEP READY` immediately.
5. **Route chained invokes.** When an update is `CHAINED_INVOKE START`:
   - Look up `functionName` in `_registry`.
   - If found: invoke the registered handler in-process (recursing into a nested `DurableTestRunner` if it's durable), serialize the result, write back `CHAINED_INVOKE SUCCEEDED` (or `FAILED`) with `ChainedInvokeDetails.Result/Error`.
   - If not found: leave `CHAINED_INVOKE STARTED` and let the orchestration loop fail with `UnregisteredSiblingFunctionException`.
6. **Wake waiting callback awaiters.** If any updates are `CALLBACK STARTED`, resolve any `TaskCompletionSource` in `waitingCallbacks` keyed by name (or anonymous queue).
7. **Return `(newCheckpointToken, newOperations)`.** `newOperations` is the list of operations that changed (so the runtime can merge them via `onNewOperations`).

### `ExecutionOrchestrator` — the drive-to-terminal loop

```csharp
internal async Task<DurableExecutionInvocationOutput> DriveToTerminalAsync(
    string arn, TInput input, TimeSpan timeout, CancellationToken ct)
{
    using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
    timeoutCts.CancelAfter(timeout);

    DurableExecutionInvocationOutput output = null!;
    var invocationCount = 0;

    while (invocationCount < _options.MaxInvocations)
    {
        timeoutCts.Token.ThrowIfCancellationRequested();

        var invocationInput = new DurableExecutionInvocationInput
        {
            DurableExecutionArn = arn,
            CheckpointToken = _store.CurrentToken(arn),
            InitialExecutionState = invocationCount == 0
                ? null
                : new InitialExecutionState
                {
                    Operations = _store.GetAllOperations(arn).ToList(),
                    NextMarker = null,
                },
        };

        output = await DurableFunction.WrapAsync(    // internal overload with IDurableServiceClient
            _handler, invocationInput, _lambdaContext, _serviceClient);

        invocationCount++;

        if (output.Status != InvocationStatus.Pending) return output;

        // If everything pending requires external input (a callback awaiting
        // SendCallbackSuccessAsync), suspend and let the test code drive.
        if (HasOnlyExternallyDrivenWork(arn)) return output;
    }

    throw new TestExecutionLimitException(/* see safety section */);
}
```

`RunAsync` calls `DriveToTerminalAsync`. `StartAsync` does too, but treats "all work is externally-driven" as a normal exit (returning the arn). `SendCallbackSuccessAsync` mutates state and re-enters `DriveToTerminalAsync`. `WaitForResultAsync` awaits a `TaskCompletionSource` that the orchestrator signals when terminal.

### Time skipping

No `ITimeProvider`, no fake clock. Mechanical:

- `WaitOperation.ReplayAsync` reads `existing.WaitDetails?.ScheduledEndTimestamp` and compares to `DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()`. If `>=`, returns immediately.
- The `CheckpointProcessor` writes `ScheduledEndTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - 1` (already elapsed) when `SkipTime = true`.
- `StepOperation` retry uses the same idiom against `NextAttemptTimestamp`.

Faster than a fake clock, no thread-safety surface, exercises the **real** replay code path.

### Sibling-function routing

```csharp
internal sealed class FunctionRegistry
{
    public void RegisterDurable<TPayload, TResult>(string name, Func<TPayload, IDurableContext, Task<TResult>> h);
    public void RegisterPlain<TPayload, TResult>(string name, Func<TPayload, ILambdaContext, Task<TResult>> h);

    public async Task<(string? Result, ErrorObject? Error)> InvokeAsync(
        string functionNameOrArn, string serializedPayload, ILambdaSerializer serializer)
    {
        var entry = LookupByNameOrArn(functionNameOrArn)
                    ?? throw new UnregisteredSiblingFunctionException(functionNameOrArn);

        try
        {
            if (entry.IsDurable)
            {
                using var childRunner = entry.CreateChildRunner();
                var typed = serializer.Deserialize<TPayload>(serializedPayload);
                var result = await childRunner.RunAsync(typed);
                result.EnsureSucceeded();
                return (serializer.Serialize(result.Result), null);
            }
            else
            {
                var typed = serializer.Deserialize<TPayload>(serializedPayload);
                var result = await entry.Plain(typed, _lambdaContext);
                return (serializer.Serialize(result), null);
            }
        }
        catch (Exception ex)
        {
            return (null, ErrorObject.FromException(ex));
        }
    }
}
```

Name matching is exact-string, with ARN parsing to extract `:function:NAME[:qualifier]` so a customer can register with the short name and the workflow can call with the full ARN (or vice versa). Match priority: exact match → ARN-extracted name match.

### What is *not* reimplemented

`ExecutionState`, `TerminationManager`, `CheckpointBatcher`, `OperationIdGenerator`, the `*Operation` classes, `LambdaSerializerHelper.GetRequired`, every replay-consistency check — all from the runtime package, exercised as-is. That is the value of injecting at the service-client boundary instead of reimplementing the orchestrator.

---

## 5. Cloud runner internals

The cloud runner has no orchestration loop and no `IDurableServiceClient` fake. It invokes a deployed Lambda, polls `GetDurableExecutionState` until terminal, reconstructs the same `TestResult<TOutput>`.

### Component map

```
CloudDurableTestRunner<TIn,TOut>
   │
   ├── _lambdaClient: IAmazonLambda                     (real AWS client)
   ├── _functionArn: string                             (qualified)
   ├── _serializer: ILambdaSerializer
   ├── _options: CloudTestRunnerOptions
   ├── _trackedExecutions: ConcurrentDictionary<arn, TaskCompletionSource<...>>
   └── _knownCallbacks: ConcurrentDictionary<arn, ConcurrentDictionary<name?, Channel<string>>>
```

### `StartAsync`

```csharp
public async Task<string> StartAsync(TInput input, TimeSpan? timeout, CancellationToken ct)
{
    var payload = SerializeForLambda(input);

    var response = await _lambdaClient.InvokeAsync(new InvokeRequest
    {
        FunctionName = _functionArn,
        InvocationType = _options.InvocationType,
        Payload = payload,
    }, ct);

    var arn = ExtractDurableExecutionArn(response);
    if (arn is null)
        throw new CloudTestException(
            "Lambda response did not include a DurableExecutionArn. " +
            "Verify the function is configured with [DurableExecution].");

    _trackedExecutions[arn] = new TaskCompletionSource<TestResult<TOutput>>();
    return arn;
}
```

`ExtractDurableExecutionArn` tries the typed accessor on `InvokeResponse` first, falls back to header parsing if needed. The exact extraction is one focused method we adjust as we learn what AWSSDK exposes.

### `WaitForResultAsync`

```csharp
public async Task<TestResult<TOutput>> WaitForResultAsync(string arn, TimeSpan? timeout, CancellationToken ct)
{
    using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
    timeoutCts.CancelAfter(timeout ?? _options.DefaultTimeout);

    string? checkpointToken = null;
    string? marker = null;
    var operations = new Dictionary<string, Operation>();

    while (true)
    {
        timeoutCts.Token.ThrowIfCancellationRequested();

        // Reuse the runtime's own service-client implementation. It already does
        // the SDK-to-internal-Operation mapping and wraps errors with durable
        // execution context.
        var serviceClient = new LambdaDurableServiceClient(_lambdaClient);
        var (page, nextMarker) = await serviceClient.GetExecutionStateAsync(
            arn, checkpointToken, marker ?? "", timeoutCts.Token);

        foreach (var op in page) operations[op.Id!] = op;
        marker = nextMarker;

        BufferAnyNewCallbacks(arn, operations.Values);

        var execOp = operations.Values.FirstOrDefault(o => o.Type == OperationTypes.Execution);
        if (execOp?.Status is OperationStatuses.Succeeded or OperationStatuses.Failed)
        {
            var result = BuildTestResult(arn, execOp, operations.Values);
            if (_trackedExecutions.TryRemove(arn, out var tcs)) tcs.TrySetResult(result);
            return result;
        }

        if (string.IsNullOrEmpty(marker))
        {
            await Task.Delay(_options.PollInterval, timeoutCts.Token);
            checkpointToken = null;
        }
        // Non-empty marker: more pages — loop without sleeping.
    }
}
```

### `WaitForCallbackAsync`

The polling loop continuously discovers `CALLBACK STARTED` operations and buffers them per-name. `WaitForCallbackAsync` reads from the buffer:

```csharp
public async Task<string> WaitForCallbackAsync(string arn, string? name, TimeSpan? timeout, CancellationToken ct)
{
    using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
    timeoutCts.CancelAfter(timeout ?? _options.DefaultTimeout);

    EnsurePollingStarted(arn, timeoutCts.Token);

    var channel = _knownCallbacks
        .GetOrAdd(arn, _ => new())
        .GetOrAdd(name ?? "<anonymous>", _ => Channel.CreateUnbounded<string>());

    return await channel.Reader.ReadAsync(timeoutCts.Token);
}
```

`EnsurePollingStarted` runs a single background task per arn that drives the buffer and resolves the result `TaskCompletionSource` on terminal state. `WaitForCallbackAsync` and `WaitForResultAsync` share that loop.

### `SendCallbackSuccessAsync` (cloud)

Cloud callbacks complete via the real AWS API:

```csharp
public Task SendCallbackSuccessAsync<TResult>(string callbackId, TResult result, CancellationToken ct)
{
    var serialized = SerializeToString(result);
    return _lambdaClient.SendDurableExecutionCallbackSuccessAsync(
        new SendDurableExecutionCallbackSuccessRequest
        {
            CallbackId = callbackId,
            Result = serialized,
        }, ct);
}
```

`SendCallbackFailureAsync` and `SendCallbackHeartbeatAsync` are equivalent calls to the matching AWSSDK methods. Errors propagate as `AmazonLambdaException`.

### `BuildTestResult` (cloud)

Same shape as the local runner produces:

```csharp
private TestResult<TOutput> BuildTestResult(string arn, Operation execOp, IEnumerable<Operation> allOps)
{
    var status = execOp.Status switch
    {
        OperationStatuses.Succeeded => InvocationStatus.Succeeded,
        OperationStatuses.Failed    => InvocationStatus.Failed,
        _                           => InvocationStatus.Pending,
    };

    TOutput? result = default;
    if (status == InvocationStatus.Succeeded && execOp.ExecutionDetails?.OutputPayload is { } payload)
        result = _serializer.Deserialize<TOutput>(payload);

    var steps = allOps
        .Where(o => o.Type != OperationTypes.Execution)
        .OrderBy(o => InsertionOrder(o))
        .Select(o => new TestStep(o, _serializer))
        .ToList();

    return new TestResult<TOutput>(
        status: status,
        result: result,
        error: execOp.ContextDetails?.Error,
        durableExecutionArn: arn,
        invocationCount: -1,                          // unknown for cloud
        steps: steps);
}
```

`InvocationCount = -1` is intentional — the field is meaningful only for the local runner's drive-loop.

### Pagination, throttling, retries

- The `marker` loop handles `NextMarker` pagination — the runtime itself paginates state during replay; same idiom here.
- Retries rely on the AWS SDK's built-in retry policy (default exponential backoff with jitter). No second retry layer.
- Poll interval defaults to 2 seconds (matching Java); customers tune via `CloudTestRunnerOptions.PollInterval`.

---

## 6. Step inspection model

Flat list, parent-id linked. Same shape as JS / Python / Java.

### `TestResult<TOutput>`

```csharp
public sealed class TestResult<TOutput>
{
    public InvocationStatus Status { get; }
    public TOutput? Result { get; }                   // throws if Status != Succeeded
    public ErrorObject? Error { get; }
    public string DurableExecutionArn { get; }
    public int InvocationCount { get; }               // local: meaningful; cloud: -1
    public IReadOnlyList<TestStep> Steps { get; }     // every operation except EXECUTION

    public TestStep GetStep(string name);             // first match — throws if missing
    public TestStep? FindStep(string name);           // null if missing
    public IReadOnlyList<TestStep> GetSteps(string name);     // all matches
    public TestStep GetStepById(string operationId);
    public IReadOnlyList<TestStep> GetChildren(TestStep parent);

    public void EnsureSucceeded();                    // throws TestExecutionFailedException if not
}
```

### `TestStep`

```csharp
public sealed class TestStep
{
    public string Id { get; }
    public string? Name { get; }
    public string? ParentId { get; }
    public OperationKind Kind { get; }
    public string? SubKind { get; }                   // Parallel | Map | WaitForCallback | Child | null
    public OperationStatus Status { get; }
    public int Attempt { get; }                       // 1-based for steps; 0 for non-step kinds
    public DateTimeOffset? StartedAt { get; }
    public DateTimeOffset? EndedAt { get; }
    public TimeSpan? Duration => StartedAt.HasValue && EndedAt.HasValue ? EndedAt - StartedAt : null;

    public T? GetResult<T>();                         // routes by Kind to the right *Details.Result
    public ErrorObject? GetError();                   // routes by Kind to the right *Details.Error
    public DateTimeOffset? GetWaitEndsAt();
    public string? GetCallbackId();
    public string? GetChainedInvokeFunctionName();

    public IReadOnlyList<TestStep> Children { get; }  // parent_id-keyed
}

public enum OperationKind { Step, Wait, Callback, ChainedInvoke, Context, Execution }

public static class OperationStatus
{
    public const string Started   = "STARTED";
    public const string Succeeded = "SUCCEEDED";
    public const string Failed    = "FAILED";
    public const string Pending   = "PENDING";
    public const string TimedOut  = "TIMED_OUT";
    public const string Cancelled = "CANCELLED";
    public const string Stopped   = "STOPPED";
    public const string Ready     = "READY";
}
```

`OperationStatus` is a `static class` of string constants rather than an enum so it stays in lockstep with the runtime's `OperationStatuses` (also string-valued). No translation layer.

### `GetResult<T>` — kind-aware typed accessor

```csharp
public T? GetResult<T>()
{
    var serialized = Kind switch
    {
        OperationKind.Step          => _operation.StepDetails?.Result,
        OperationKind.ChainedInvoke => _operation.ChainedInvokeDetails?.Result,
        OperationKind.Context       => _operation.ContextDetails?.Result,
        OperationKind.Callback      => _operation.CallbackDetails?.Result,
        _                           => null,
    };
    return serialized is null ? default : _serializer.Deserialize<T>(serialized);
}
```

`GetError` mirrors the same dispatch.

### Branch addressing

```csharp
result.GetStep("validate_order");                     // first by name — throws if missing
result.FindStep("validate_order");                    // null if missing
result.GetSteps("process_item");                      // all matches (per-item map / parallel branch)
result.GetStepById("op-12345");                       // by exact op id

var mapOp = result.GetStep("process_all_items");
foreach (var (child, i) in mapOp.Children.Select((c, i) => (c, i)))
    Assert.Equal($"expected-{i}", child.GetResult<string>());
```

### Worked end-to-end example

```csharp
[Fact]
public async Task ParallelMapWorkflow_AllBranchesProcessIndependently()
{
    await using var runner = new DurableTestRunner<BatchRequest, BatchResult>(
        handler: new BatchFunction().Handler,
        options: new TestRunnerOptions { SkipTime = true });

    var result = await runner.RunAsync(new BatchRequest { Items = ["a", "b", "c"] });
    result.EnsureSucceeded();

    Assert.Equal(3, result.Result!.SuccessCount);

    var parallel = result.GetStep("process_batch");
    Assert.Equal(OperationKind.Context, parallel.Kind);
    Assert.Equal("PARALLEL", parallel.SubKind);

    var branches = parallel.Children;
    Assert.Equal(3, branches.Count);

    foreach (var (branch, item) in branches.Zip(["a", "b", "c"], (b, i) => (b, i)))
    {
        Assert.Equal(OperationStatus.Succeeded, branch.Status);
        Assert.Equal($"processed-{item}", branch.GetResult<string>());
        Assert.Equal(1, branch.Attempt);
    }

    var wait = result.GetStep("settle_window");
    Assert.Equal(OperationKind.Wait, wait.Kind);
    Assert.Equal(OperationStatus.Succeeded, wait.Status);
}
```

### Failure inspection

```csharp
var result = await runner.RunAsync(new OrderEvent { OrderId = "BAD" });

Assert.Equal(InvocationStatus.Failed, result.Status);
Assert.Equal("OrderValidationException", result.Error!.ErrorType);

var validate = result.GetStep("validate_order");
Assert.Equal(OperationStatus.Failed, validate.Status);
Assert.Equal(3, validate.Attempt);
Assert.Equal("OrderValidationException", validate.GetError()?.ErrorType);
```

### What we don't add

- Computed aggregates (`GetTotalDuration`, `GetSuccessCount`) — LINQ over `Steps` covers the case.
- `TestStep.Parent` — closes the loop with `Children` but adds reference cycles for marginal value.
- `print()` / debug-formatter — `[DebuggerDisplay]` covers IDE inspection.
- Typed `WaitDetails` accessor returning a struct — `GetWaitEndsAt()` is sufficient.

---

## 7. Safety, errors, edge cases

### `MaxInvocations`

Default 100 (matches Java). Counts handler invocations. When exceeded, throws `TestExecutionLimitException` with diagnostics:

```text
TestExecutionLimitException: Workflow did not reach a terminal state within 100 invocations.

Possible causes:
  - Workflow uses WaitForCallbackAsync — call StartAsync/WaitForCallbackAsync/SendCallbackSuccessAsync
    instead of RunAsync.
  - Workflow uses InvokeAsync('process-payment') and 'process-payment' isn't registered —
    call runner.RegisterFunction("process-payment", handler).
  - Workflow has an infinite retry loop.
  - Workflow uses WaitForConditionAsync that never returns true.

Set TestRunnerOptions.MaxInvocations to a higher value if your workflow is legitimately long.
Last invocation status: PENDING. Total operations recorded: 47.
```

### `Timeout`

Default 30 seconds local, 5 minutes cloud. Implemented via `CancellationTokenSource.CancelAfter`. Local timeouts are almost never hit in healthy tests because `SkipTime = true` collapses every wait. Throws `TimeoutException`.

### What's *not* a budget

No max-operations limit (the runtime's checkpoint payload limits already cap state — 6MB per AWSSDK.Lambda model). No max-callback limit. No memory budget.

### Error taxonomy

| Exception | When | Who catches |
|---|---|---|
| `TestExecutionFailedException` | `result.EnsureSucceeded()` and `Status != Succeeded` | Test author |
| `TestExecutionLimitException` | `MaxInvocations` exceeded | Test author |
| `TimeoutException` | Wall-clock budget exceeded | Test author |
| `UnregisteredSiblingFunctionException` | `InvokeAsync("name")` and `"name"` not registered | Test author |
| `OperationCanceledException` | `CancellationToken` triggered | Test framework |
| `CloudTestException` | Cloud-only — response missing `DurableExecutionArn` | Test author |
| `InvalidOperationException` | API misuse (`SendCallbackSuccessAsync` for unknown id, etc.) | Test author |
| `ArgumentException` family | Constructor / option validation | Test author |
| `AmazonLambdaException` | Cloud-only — underlying AWS SDK error | Test author / framework |

```csharp
public sealed class TestExecutionFailedException : Exception
{
    public InvocationStatus FinalStatus { get; }
    public ErrorObject? FailureError { get; }
    public IReadOnlyList<TestStep> Steps { get; }
}
```

### Workflow failures vs runner failures

When the workflow throws, the runtime serializes the exception into an `ErrorObject` and emits a `Failed` envelope. The runner returns a `TestResult` with `Status = Failed`; it does *not* re-throw. Test code chooses:

```csharp
// Style 1: explicit
var result = await runner.RunAsync(input);
Assert.Equal(InvocationStatus.Failed, result.Status);
Assert.Equal("ValidationException", result.Error?.ErrorType);

// Style 2: assert success
var result = await runner.RunAsync(input);
result.EnsureSucceeded();
```

Step-level failures don't necessarily fail the workflow:

```csharp
var result = await runner.RunAsync(input);
result.EnsureSucceeded();   // workflow as a whole succeeded

var fetch = result.GetStep("fetch_inventory");
Assert.Equal(OperationStatus.Failed, fetch.Status);
Assert.Equal(3, fetch.Attempt);

var fallback = result.GetStep("use_cached_inventory");
Assert.Equal(OperationStatus.Succeeded, fallback.Status);
```

### Concurrency

The runner is **not thread-safe across overlapping `RunAsync`/`StartAsync` calls on the same instance**. One runner = one execution at a time. Tests that run workflows in parallel construct multiple runners.

### Disposal

`DurableTestRunner` and `CloudDurableTestRunner` implement `IAsyncDisposable`. `await using` is documented and recommended; tests that omit it aren't broken (orchestration tasks observe timeout cancellation) but may leak background work briefly past the test method.

### Edge-case table

| Case | Handling |
|---|---|
| Workflow returns `null`/default | `TestResult.Result == default(TOutput)`. |
| Workflow has no operations | `TestResult.Steps` empty. `InvocationCount = 1`. |
| `Wait(TimeSpan.Zero)` | `ScheduledEndTimestamp = now`; replay completes immediately even with `SkipTime = false`. |
| `WaitAsync` with `SkipTime = false` | Real wall-clock wait. Used for chaos-style tests. |
| `context.ConfigureLogger(...)` | Honored — runtime serializer/logger config flows through unchanged. |
| Multiple `[DurableExecution]` handlers in the same test class | Each test constructs its own runner. |
| `RunInChildContextAsync` | Child context is another operation kind in the flat list; child operations linked via `parent_id` and accessible via `parallel.Children`. |
| `MapAsync` with `ItemBatcher` | Each batch is one operation with multiple "child" ops (per-item steps). |
| `WaitForConditionAsync` (DOTNET-8665) | Treated as a wait-like operation that auto-advances when `SkipTime = true`. |
| `InvokeAsync` for a registered durable sibling | Spawns a child `DurableTestRunner`; child's steps live in the parent's `ChainedInvokeDetails.Result` payload. |
| `InvokeAsync` for a registered sibling that fails | Child returns `Failed`; parent sees `ChainedInvokeDetails.Error`. |
| `SendCallbackSuccessAsync` *before* the workflow reaches the callback point | Throws `InvalidOperationException`. |
| `WaitForResultAsync` for an arn whose execution already completed | Returns the cached result. |
| Two `WaitForCallbackAsync` blocks with the same name | First call returns the first; second call returns the second. |
| Anonymous callback (no name) | `WaitForCallbackAsync(name: null)` returns FIFO. |
| `MaxInvocations = 0` | `ArgumentOutOfRangeException` from constructor. |
| `RegisterFunction` called after `RunAsync` started | Registry snapshotted at start; late registration takes effect on the next run. |

### Failure-mode coverage table

| Failure mode | Caught by |
|---|---|
| Infinite retry inside a step | `MaxInvocations` |
| Workflow that never resolves | `MaxInvocations` |
| Test forgets `SendCallbackSuccessAsync` after `StartAsync` | `Timeout` on `WaitForResultAsync` |
| Workflow throws unhandled exception | Returned in `TestResult.Status = Failed` |
| Sibling function not registered | `UnregisteredSiblingFunctionException` synchronously |
| Bad checkpoint token | Runtime's existing transient-token carve-out → re-invoke once → `MaxInvocations` |
| User cancels via `CancellationToken` | `OperationCanceledException` |
| AWS API throws (cloud) | `AmazonLambdaException` |

---

## 8. Testing the testing SDK

### Layer 1 — unit tests against the runtime SDK with a mock `IDurableServiceClient`

Project: `Amazon.Lambda.DurableExecution.Testing.Tests`. Verifies orchestration logic independent of the runtime engine, with a mocked `IDurableServiceClient`.

Coverage:

- `RunAsync` happy path returns Succeeded after one invocation.
- `RunAsync` returns Failed when handler throws.
- `MaxInvocations` exhaustion throws `TestExecutionLimitException` with the diagnostic message.
- `Timeout` throws `TimeoutException`.
- `StartAsync` + `WaitForResultAsync` round-trip.
- `WaitForCallbackAsync` blocks until matching callback id appears.
- `SendCallbackSuccessAsync` resumes the orchestrator.
- `UnregisteredSiblingFunctionException` thrown when chained-invoke target is unknown.
- Cancellation propagates correctly.

### Layer 2 — integration tests with the real runtime engine + in-memory backend

Same project, `/Integration/` folder. Real `DurableFunction.WrapAsync`, real `ExecutionState`, real `CheckpointBatcher`, with `InMemoryDurableServiceClient` as the seam.

Coverage:

- Handler using every operation kind reaches Succeeded.
- Step retries advance time correctly with `SkipTime = true`.
- A failing-then-succeeding step records `Attempt = 2` and is `SUCCEEDED`.
- `MapAsync` with 10 items produces 10 child operations, all reachable via `mapOp.Children`.
- `WaitForCallbackAsync` workflow driven via `StartAsync` / `WaitForCallbackAsync` / `SendCallbackSuccessAsync` / `WaitForResultAsync` completes.
- `InvokeAsync` to a registered durable sibling completes; parent's `ChainedInvokeDetails.Result` deserializes correctly.
- `InvokeAsync` to a registered plain (non-durable) sibling completes.
- Replay-consistency violations surface `NonDeterministicExecutionException` exactly as production does.

This is the most important layer — it proves the `IDurableServiceClient` injection covers the full runtime surface end-to-end.

### Layer 3 — snapshot tests of generated handler shape

Add to `Amazon.Lambda.Annotations.SourceGenerators.Tests`. For a `[LambdaFunction] [DurableExecution]` method, the generated wrapper exposes a publicly-callable handler matching the runner's expected signature. If the source generator changes the wrapper shape, this test fails and forces a lockstep update.

### Layer 4 — cloud integration tests

Project: `Amazon.Lambda.DurableExecution.Testing.Integration.Tests`. Runs against a real AWS account, gated by credentials, wired into `integ-tests` MSBuild target. Deploys a sample durable function, invokes `CloudDurableTestRunner`, tears down.

Coverage:

- A simple deployed workflow runs to Succeeded via `CloudDurableTestRunner.RunAsync`.
- `StartAsync` returns a `DurableExecutionArn` extractable from the `InvokeResponse`.
- `WaitForCallbackAsync` discovers a real callback id from polled state.
- `SendCallbackSuccessAsync` calls `SendDurableExecutionCallbackSuccessAsync` and the workflow resumes.
- A failing workflow returns `Failed` with the real `ErrorObject` populated.
- Polling correctly handles `NextMarker` pagination.
- Timeout cancels mid-poll cleanly.

Runs nightly, not on every PR.

### Test infrastructure reuse

| Asset | Source |
|---|---|
| `TestLambdaContext`, `TestLambdaLogger` | `Amazon.Lambda.TestUtilities` |
| `DefaultLambdaJsonSerializer` | `Amazon.Lambda.Serialization.SystemTextJson` |
| Moq-based `IAmazonLambda` fakes | Pattern from `Amazon.Lambda.DurableExecution.Tests` |
| Verify (snapshot tests) | Already in use for source-generator tests |

### Project layout

```text
Libraries/test/Amazon.Lambda.DurableExecution.Testing.Tests/
├── Unit/
│   ├── DurableTestRunnerTests.cs
│   ├── CheckpointProcessorTests.cs
│   ├── FunctionRegistryTests.cs
│   ├── TestResultTests.cs
│   └── TestStepTests.cs
├── Integration/
│   ├── EveryOperationKindTests.cs
│   ├── CallbackWorkflowTests.cs
│   ├── SiblingInvokeTests.cs
│   ├── MapAsyncTests.cs
│   ├── ParallelAsyncTests.cs
│   ├── ChildContextTests.cs
│   └── ReplayConsistencyTests.cs
├── CloudRunner/
│   ├── CloudDurableTestRunnerTests.cs
│   ├── HistoryPagingTests.cs
│   └── CallbackBufferingTests.cs
└── Fakes/
    └── (test-only helpers and tiny sample workflows)

Libraries/test/Amazon.Lambda.DurableExecution.Testing.Integration.Tests/
└── (cloud integration suites, AWS-credentialed)
```

### CI wiring

- Unit + integration tests (Layers 1–3) run as part of the existing `unit-tests` target.
- Cloud integration tests (Layer 4) run in `integ-tests` only.
- ≥80% line coverage at v1; orchestration loop and `CheckpointProcessor` target ≥95%.

---

## 9. Cross-SDK comparison

| Aspect | JavaScript | Python | Java | .NET (this design) |
|---|---|---|---|---|
| Local-runner ctor | `new LocalDurableTestRunner({ handlerFunction })` | `DurableFunctionTestRunner(handler)` (context manager) | `LocalDurableTestRunner.create(Class<I>, BiFunction)` | `new DurableTestRunner<TIn,TOut>(handler, options)` |
| Run | `await runner.run({ payload })` | `runner.run(input, timeout)` | `runner.runUntilComplete(input)` | `await runner.RunAsync(input, timeout, ct)` |
| Two-call shape | implicit (Promises) | `run_async` + `wait_for_callback` + `send_callback_*` + `wait_for_result` | `runUntilComplete` (auto-advances) | `StartAsync` + `WaitForCallbackAsync` + `SendCallback*Async` + `WaitForResultAsync` |
| Interception | Worker-thread service-client interface | In-process `DurableServiceClient` injection | Standalone orchestrator (no production engine) | In-process `IDurableServiceClient` injection |
| Time skipping | Sinon fake timers + queue scheduler | Background scheduler thread | Manual `advanceTime()` | Mechanical: write already-elapsed timestamps |
| Sibling registration | `registerDurableFunction` + `registerFunction` | none | none | `RegisterDurableFunction` + `RegisterFunction` |
| Cloud backend | `Invoke` + `GetDurableExecutionHistory` | `InvokeDurable` + `GetDurableExecutionState` | `Invoke` + `GetDurableExecutionHistory` | `InvokeAsync` + `GetDurableExecutionStateAsync` |
| Step inspection | Flat list, parent-id linked | Flat list, parent-id linked | Flat list, parent-id linked | Flat list, parent-id linked |
| Safety limit | None explicit | `timeout` per call | `MAX_INVOCATIONS = 100` | `MaxInvocations = 100` + `Timeout` |

JavaScript and Python both converged on **service-client interface injection**; .NET adopts the same. JavaScript adds a worker thread for event-loop fidelity (a JS-specific concern); Python and .NET don't need one because their async models already handle continuation timing correctly.

---

## 10. Implementation summary

### Runtime-package changes (`Amazon.Lambda.DurableExecution`)

1. New file `Services/IDurableServiceClient.cs` defining the `internal interface`.
2. `LambdaDurableServiceClient` declares `: IDurableServiceClient` (no body changes).
3. New `internal static Task<DurableExecutionInvocationOutput> WrapAsync<TIn,TOut>(workflow, invocationInput, ctx, IDurableServiceClient)` overload in `DurableFunction.cs`.
4. `[InternalsVisibleTo("Amazon.Lambda.DurableExecution.Testing", PublicKey=...)]` added alongside the existing `.Tests` entry.

### New package (`Amazon.Lambda.DurableExecution.Testing`)

Public types: `DurableTestRunner<TIn,TOut>`, `CloudDurableTestRunner<TIn,TOut>`, `IDurableTestRunner<TIn,TOut>`, `TestResult<TOut>`, `TestStep`, `TestRunnerOptions`, `CloudTestRunnerOptions`, `OperationKind`, `OperationStatus`, `TestExecutionFailedException`, `TestExecutionLimitException`, `UnregisteredSiblingFunctionException`, `CloudTestException`.

Internal types: `InMemoryDurableServiceClient`, `InMemoryOperationStore`, `CheckpointProcessor`, `ExecutionOrchestrator`, `FunctionRegistry`.

### New test projects

- `Amazon.Lambda.DurableExecution.Testing.Tests` (Layers 1–3) → `unit-tests` MSBuild target.
- `Amazon.Lambda.DurableExecution.Testing.Integration.Tests` (Layer 4) → `integ-tests` MSBuild target.

### Estimate

Per the parent design doc: **~1.5 weeks** for full Local + Cloud + RegisterFunction + step inspection. This design doesn't change that estimate — reusing the production engine via the `IDurableServiceClient` seam keeps the testing-package code small (~800–1200 lines).
