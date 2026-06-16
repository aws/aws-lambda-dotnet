// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

namespace Amazon.Lambda.DurableExecution.Internal;

/// <summary>
/// In-memory store of the operations replayed from <see cref="InitialExecutionState"/>
/// plus replay-mode tracking. Outbound checkpoints are owned by
/// <see cref="CheckpointBatcher"/>; this type is the inbound side only.
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item>At construction the workflow is "replaying" if and only if any user-replayable
///       op is present. The service always sends one <c>EXECUTION</c>-type op
///       carrying the input payload — that's bookkeeping, not user history,
///       so it doesn't count.</item>
///   <item><see cref="TrackReplay"/> is called by every <c>DurableOperation.ExecuteAsync</c>
///       at the top of the call. Once every checkpointed completed
///       non-<c>EXECUTION</c> op has been visited, the workflow has caught up
///       to the replay frontier and <see cref="IsReplaying"/> flips to <c>false</c>
///       for the rest of the invocation.</item>
/// </list>
/// <para>
/// Thread safety: two paths reach this type concurrently. (1) The
/// <see cref="CheckpointBatcher"/> background worker invokes
/// <see cref="AddOperations"/> (via the <c>onNewOperations</c> hook) while the
/// workflow thread reads via <see cref="GetOperation"/> / <see cref="HasOperation"/> —
/// e.g. the fire-and-forget <c>StepOperation</c> path. (2)
/// <see cref="ParallelOperation{T}"/> dispatches N branches concurrently, each
/// running its own <see cref="ChildContextOperation{T}"/>, so
/// <see cref="TrackReplay"/>, <see cref="ValidateReplayConsistency"/>,
/// <see cref="GetOperation"/>, <see cref="HasOperation"/> and the
/// <see cref="IsReplaying"/> getter are reachable from multiple threads at once.
/// All read/write access to <c>_operations</c>, <c>_visitedOperations</c>,
/// <c>_isReplaying</c> and <c>_remainingReplayOps</c> is therefore guarded by a
/// single private lock. Every guarded path is an O(1) dictionary lookup, set
/// insert, or short iteration, so contention stays brief; we use a plain
/// <c>lock</c> rather than <see cref="System.Threading.SemaphoreSlim"/> because
/// none of the guarded code paths are async, and rather than
/// <c>ConcurrentDictionary</c> because <see cref="LoadFromCheckpoint"/> performs
/// a compound add-then-scan.
/// </para>
/// </remarks>
internal sealed class ExecutionState
{
    private readonly object _lock = new();
    private readonly Dictionary<string, Operation> _operations = new();
    private readonly HashSet<string> _visitedOperations = new();
    private bool _isReplaying;
    private int _remainingReplayOps;

    public int CheckpointedOperationCount
    {
        get { lock (_lock) return _operations.Count; }
    }

    /// <summary>
    /// True when the workflow is re-deriving prior operations from checkpointed
    /// state. False when running fresh (not-yet-checkpointed) code.
    /// </summary>
    public bool IsReplaying
    {
        get { lock (_lock) return _isReplaying; }
    }

    public void LoadFromCheckpoint(InitialExecutionState? initialState)
    {
        lock (_lock)
        {
            if (initialState?.Operations != null)
            {
                AddOperationsLocked(initialState.Operations);
            }

            // We're "replaying" when there are completed ops (SUCCEEDED, FAILED,
            // CANCELLED, STOPPED) we need to re-derive before resuming live work.
            // The service-side EXECUTION op (input payload bookkeeping) is always
            // present and doesn't count. If the only ops are in-progress
            // (READY/PENDING/STARTED), there's nothing to re-derive — the next
            // user call IS the next thing to run — so IsReplaying starts false.
            var (_, terminalCount) = ScanReplayableLocked();
            _remainingReplayOps = terminalCount;
            _isReplaying = terminalCount > 0;
        }
    }

    public void AddOperations(IEnumerable<Operation> operations)
    {
        lock (_lock)
        {
            AddOperationsLocked(operations);
        }
    }

    /// <summary>
    /// Returns the checkpointed record for <paramref name="operationId"/>, or null
    /// if none. Callers should switch on <see cref="Operation.Status"/> against
    /// <see cref="OperationStatuses"/> constants to decide replay behavior.
    /// </summary>
    public Operation? GetOperation(string operationId)
    {
        lock (_lock)
        {
            _operations.TryGetValue(operationId, out var op);
            return op;
        }
    }

    public bool HasOperation(string operationId)
    {
        lock (_lock)
        {
            return _operations.ContainsKey(operationId);
        }
    }

    /// <summary>
    /// Records that the workflow has reached <paramref name="operationId"/>.
    /// Once every checkpointed completed non-<c>EXECUTION</c> op has been
    /// visited the workflow has caught up to the replay frontier and
    /// <see cref="IsReplaying"/> flips to false. Idempotent: calling more than
    /// once with the same id has no additional effect.
    /// </summary>
    public void TrackReplay(string operationId)
    {
        lock (_lock)
        {
            if (!_isReplaying) return;
            if (!_visitedOperations.Add(operationId)) return;
            if (!_operations.TryGetValue(operationId, out var op)) return;
            if (op.Type == OperationTypes.Execution) return;
            if (!IsTerminalStatus(op.Status)) return;

            if (--_remainingReplayOps <= 0)
                _isReplaying = false;
        }
    }

    public void ValidateReplayConsistency(string operationId, string expectedType, string? expectedName)
    {
        lock (_lock)
        {
            // Independent of IsReplaying: as long as a checkpoint record exists
            // for this id, its type/name must match what user code is asking for.
            // If the only checkpointed ops are in-progress (PENDING/READY/STARTED),
            // IsReplaying is false but the records still exist and code drift can
            // still produce a mismatch.
            if (!_operations.TryGetValue(operationId, out var op)) return;

            if (op.Type != null && op.Type != expectedType)
            {
                throw new NonDeterministicExecutionException(
                    $"Non-deterministic execution detected for operation '{operationId}': " +
                    $"expected type '{expectedType}' but found '{op.Type}' from a previous invocation. " +
                    $"Code must not change the order or type of durable operations between deployments.");
            }

            if (expectedName != null && op.Name != null && op.Name != expectedName)
            {
                throw new NonDeterministicExecutionException(
                    $"Non-deterministic execution detected for operation '{operationId}': " +
                    $"expected name '{expectedName}' but found '{op.Name}' from a previous invocation. " +
                    $"Code must not change the order or type of durable operations between deployments.");
            }
        }
    }

    private void AddOperationsLocked(IEnumerable<Operation> operations)
    {
        foreach (var op in operations)
        {
            if (op.Id == null) continue;
            _operations[op.Id] = op;
        }
    }

    private (bool HasReplayable, int TerminalCount) ScanReplayableLocked()
    {
        var has = false;
        var count = 0;
        foreach (var op in _operations.Values)
        {
            if (op.Type == OperationTypes.Execution) continue;
            has = true;
            if (IsTerminalStatus(op.Status)) count++;
        }
        return (has, count);
    }

    private static bool IsTerminalStatus(string? status) =>
        status == OperationStatuses.Succeeded
        || status == OperationStatuses.Failed
        || status == OperationStatuses.Cancelled
        || status == OperationStatuses.Stopped
        || status == OperationStatuses.TimedOut;
}
