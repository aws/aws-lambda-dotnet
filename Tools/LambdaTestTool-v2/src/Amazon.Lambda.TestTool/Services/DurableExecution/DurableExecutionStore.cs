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
        return ctx.Processor.Process(arn, checkpointToken, updates);
    }

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
