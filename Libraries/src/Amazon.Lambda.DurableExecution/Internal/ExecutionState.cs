namespace Amazon.Lambda.DurableExecution.Internal;

/// <summary>
/// Replay state of the current invocation.
/// </summary>
internal enum ExecutionMode
{
    /// <summary>Re-deriving prior operations from checkpointed state.</summary>
    Replay,
    /// <summary>Executing fresh code that hasn't been checkpointed before.</summary>
    Execution
}

/// <summary>
/// In-memory store of the operations replayed from <see cref="InitialExecutionState"/>.
/// Read-only after load (apart from <see cref="EnterExecutionMode"/>); outbound
/// checkpoints are owned by <see cref="CheckpointBatcher"/>.
/// </summary>
internal sealed class ExecutionState
{
    private readonly Dictionary<string, Operation> _operations = new();

    public ExecutionMode Mode { get; private set; } = ExecutionMode.Replay;

    public int CheckpointedOperationCount => _operations.Count;

    public void LoadFromCheckpoint(InitialExecutionState? initialState)
    {
        if (initialState?.Operations == null)
        {
            Mode = ExecutionMode.Execution;
            return;
        }

        AddOperations(initialState.Operations);

        if (_operations.Count == 0)
        {
            Mode = ExecutionMode.Execution;
        }
    }

    public void AddOperations(IEnumerable<Operation> operations)
    {
        foreach (var op in operations)
        {
            if (op.Id == null) continue;
            _operations[op.Id] = op;
        }
    }

    /// <summary>
    /// Returns the checkpointed record for <paramref name="operationId"/>, or null
    /// if none. Callers should switch on <see cref="Operation.Status"/> against
    /// <see cref="OperationStatuses"/> constants to decide replay behavior.
    /// </summary>
    public Operation? GetOperation(string operationId)
    {
        _operations.TryGetValue(operationId, out var op);
        return op;
    }

    public void ValidateReplayConsistency(string operationId, string expectedType, string? expectedName)
    {
        if (Mode != ExecutionMode.Replay) return;

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

    public bool HasOperation(string operationId) => _operations.ContainsKey(operationId);

    /// <summary>
    /// Transitions to <see cref="ExecutionMode.Execution"/>. Called by an operation
    /// that's about to run fresh (not-yet-checkpointed) code. Idempotent.
    /// </summary>
    public void EnterExecutionMode() => Mode = ExecutionMode.Execution;
}
