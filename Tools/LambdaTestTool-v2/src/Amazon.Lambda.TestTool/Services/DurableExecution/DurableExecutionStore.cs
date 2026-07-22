// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using Amazon.Lambda.DurableExecution;

namespace Amazon.Lambda.TestTool.Services.DurableExecution;

/// <summary>
/// Root of the durable-execution service emulator. Owns one operation store and one checkpoint
/// processor per durable execution (keyed by ARN), mints execution ARNs, and serves paged
/// state reads.
/// </summary>
/// <remarks>
/// Registered as a singleton in <c>TestToolProcess</c> so the checkpoint/state HTTP endpoints
/// (see <see cref="DurableServiceApi"/>) and — in a later phase — the re-invocation driver
/// share a single backing store. Phase 1 exposes only the data-plane operations the running
/// function calls (<c>CheckpointDurableExecution</c> / <c>GetDurableExecutionState</c>); the
/// start hook and drive loop arrive in Phase 2.
/// </remarks>
internal sealed class DurableExecutionStore
{
    /// <summary>
    /// Max operations returned in a single <c>GetDurableExecutionState</c> page. The real
    /// service pages large histories; a small page here exercises the SDK's paging path
    /// (NextMarker follow-up calls) during local testing.
    /// </summary>
    internal const int StatePageSize = 100;

    private readonly bool _skipTime;
    private readonly ConcurrentDictionary<string, ExecutionContext> _executions = new();
    private long _executionCounter;

    // CallbackId -> (arn, operationId). Populated as checkpoints mint CALLBACK ops so the callback
    // HTTP endpoints can resolve a callback back onto the right operation of the right execution.
    private readonly ConcurrentDictionary<string, CallbackTarget> _callbacks = new();

    private readonly record struct CallbackTarget(string Arn, string OperationId);

    /// <summary>Outcome of attempting to resolve an inbound callback.</summary>
    public enum CallbackResolution { Resolved, UnknownCallback, AlreadyResolved }

    public DurableExecutionStore(bool skipTime)
    {
        _skipTime = skipTime;
    }

    private sealed class ExecutionContext
    {
        public required InMemoryOperationStore Store { get; init; }
        public required CheckpointProcessor Processor { get; init; }
    }

    private ExecutionContext GetOrCreate(string arn)
    {
        return _executions.GetOrAdd(arn, _ =>
        {
            var store = new InMemoryOperationStore();
            return new ExecutionContext
            {
                Store = store,
                Processor = new CheckpointProcessor(store, _skipTime)
            };
        });
    }

    /// <summary>True once an execution with this ARN has been checkpointed at least once.</summary>
    public bool Exists(string arn) => _executions.ContainsKey(arn);

    /// <summary>
    /// Mints a synthetic durable-execution ARN for a newly-started execution. The shape mirrors
    /// the service's (<c>arn:…:function:NAME:QUALIFIER/durable-execution/GROUP/NAME</c>) so the
    /// SDK's ARN handling and the emulator's own routing exercise the real slash-bearing form.
    /// </summary>
    public string MintArn(string functionName, string executionName, string? qualifier = null)
    {
        var n = Interlocked.Increment(ref _executionCounter);
        var qual = string.IsNullOrEmpty(qualifier) ? "$LATEST" : qualifier;
        var group = "local";
        var name = string.IsNullOrEmpty(executionName) ? $"exec-{n}" : executionName;
        return $"arn:aws:lambda:us-east-1:000000000000:function:{functionName}:{qual}/durable-execution/{group}/{name}";
    }

    /// <summary>
    /// Applies a batch of checkpoint updates. Returns the rotated checkpoint token and the
    /// operations created/modified by this batch (the <c>NewExecutionState.Operations</c> the
    /// SDK merges back into its in-memory state — carrying, e.g., freshly minted callback IDs).
    /// </summary>
    public (string NewToken, IReadOnlyList<Operation> NewOperations) Checkpoint(
        string arn,
        string? checkpointToken,
        IReadOnlyList<WireOperationUpdate> updates)
    {
        var ctx = GetOrCreate(arn);
        var result = ctx.Processor.Process(arn, checkpointToken, updates);

        // Index any freshly-minted callback IDs so inbound SendDurableExecutionCallback* requests
        // can be routed back to the originating operation.
        foreach (var op in result.NewOperations)
        {
            if (op.Type == OperationTypes.Callback && op.CallbackDetails?.CallbackId is { } callbackId)
                _callbacks.TryAdd(callbackId, new CallbackTarget(arn, op.Id!));
        }

        return result;
    }

    /// <summary>
    /// Resolves a pending callback by its ID, setting the CALLBACK operation to SUCCEEDED (with
    /// <paramref name="result"/>) or FAILED (with <paramref name="error"/>). Returns the outcome
    /// and, when resolved, the ARN of the affected execution so the caller can wake its driver.
    /// </summary>
    public (CallbackResolution Outcome, string? Arn) ResolveCallback(
        string callbackId, string? result, ErrorObject? error)
    {
        if (!_callbacks.TryGetValue(callbackId, out var target))
            return (CallbackResolution.UnknownCallback, null);

        var ctx = GetOrCreate(target.Arn);
        var op = ctx.Store.GetOperation(target.Arn, target.OperationId);
        if (op is null)
            return (CallbackResolution.UnknownCallback, null);

        // A callback is only resolvable once; a second success/failure is a no-op.
        if (op.Status is OperationStatuses.Succeeded or OperationStatuses.Failed)
            return (CallbackResolution.AlreadyResolved, target.Arn);

        op.CallbackDetails ??= new CallbackDetails();
        op.EndTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (error is null)
        {
            op.Status = OperationStatuses.Succeeded;
            op.CallbackDetails.Result = result;
            op.CallbackDetails.Error = null;
        }
        else
        {
            op.Status = OperationStatuses.Failed;
            op.CallbackDetails.Error = error;
        }
        ctx.Store.Upsert(target.Arn, op);
        return (CallbackResolution.Resolved, target.Arn);
    }

    /// <summary>True if the callback ID is known to the emulator.</summary>
    public bool CallbackExists(string callbackId) => _callbacks.ContainsKey(callbackId);

    /// <summary>
    /// Returns one page of the execution's operation history starting at <paramref name="marker"/>
    /// (an integer offset encoded as a string; null/empty = start). The returned NextMarker is
    /// null when the page reaches the end of the history.
    /// </summary>
    public (IReadOnlyList<Operation> Operations, string? NextMarker) GetState(string arn, string? marker)
    {
        var ctx = GetOrCreate(arn);
        var all = ctx.Store.GetAllOperations(arn);

        var offset = 0;
        if (!string.IsNullOrEmpty(marker) && int.TryParse(marker, out var parsed) && parsed > 0)
            offset = parsed;

        if (offset >= all.Count)
            return (Array.Empty<Operation>(), null);

        var page = all.Skip(offset).Take(StatePageSize).ToList();
        var nextOffset = offset + page.Count;
        var nextMarker = nextOffset < all.Count ? nextOffset.ToString() : null;
        return (page, nextMarker);
    }

    /// <summary>Current checkpoint token for the execution (creates an empty execution if new).</summary>
    public string CurrentToken(string arn) => GetOrCreate(arn).Store.CurrentToken(arn);

    /// <summary>
    /// Seeds the top-level EXECUTION operation carrying the user input payload. Idempotent:
    /// re-driving (e.g. a replay pass) must not reset the op or clobber recorded state. Mirrors
    /// <c>ExecutionOrchestrator.SeedExecutionOperation</c> (operation id <c>exec-0</c>).
    /// </summary>
    public void SeedExecution(string arn, string? inputPayload)
    {
        var ctx = GetOrCreate(arn);
        if (ctx.Store.GetOperation(arn, ExecutionOperationId) is not null)
            return;

        ctx.Store.Upsert(arn, new Operation
        {
            Id = ExecutionOperationId,
            Type = OperationTypes.Execution,
            Status = OperationStatuses.Started,
            ExecutionDetails = new ExecutionDetails { InputPayload = inputPayload }
        });
    }

    /// <summary>The operation id of the seeded top-level EXECUTION op.</summary>
    internal const string ExecutionOperationId = "exec-0";

    /// <summary>Snapshot of all operations recorded for the execution.</summary>
    public IReadOnlyList<Operation> GetAllOperations(string arn) => GetOrCreate(arn).Store.GetAllOperations(arn);

    /// <summary>Chained-invokes started but not resolved (Phase 4). Non-empty ⇒ unsupported workflow.</summary>
    public IReadOnlyList<CheckpointProcessor.PendingInvoke> DrainPendingInvokes(string arn)
        => GetOrCreate(arn).Processor.DrainPendingInvokes();
}
