using SdkOperationUpdate = Amazon.Lambda.Model.OperationUpdate;

namespace Amazon.Lambda.DurableExecution.Internal;

/// <summary>
/// Abstract base for durable operations (Step, Wait, ...). Subclasses implement
/// <see cref="StartAsync"/> (no prior checkpoint) and <see cref="ReplayAsync"/>
/// (some checkpoint exists); the base handles lookup and dispatch.
/// </summary>
/// <typeparam name="TResult">The operation's result type.</typeparam>
internal abstract class DurableOperation<TResult>
{
    protected readonly ExecutionState State;
    protected readonly TerminationManager Termination;
    protected readonly string OperationId;
    protected readonly string? Name;
    protected readonly string DurableExecutionArn;
    protected readonly CheckpointBatcher? Batcher;

    protected DurableOperation(
        string operationId,
        string? name,
        ExecutionState state,
        TerminationManager termination,
        string durableExecutionArn,
        CheckpointBatcher? batcher = null)
    {
        OperationId = operationId;
        Name = name;
        State = state;
        Termination = termination;
        DurableExecutionArn = durableExecutionArn;
        Batcher = batcher;
    }

    /// <summary>The wire-format operation type (e.g. "STEP", "WAIT").</summary>
    protected abstract string OperationType { get; }

    /// <summary>
    /// Looks up any prior checkpoint for this op and dispatches to
    /// <see cref="StartAsync"/> (none) or <see cref="ReplayAsync"/> (some).
    /// </summary>
    public Task<TResult> ExecuteAsync(CancellationToken cancellationToken)
    {
        State.ValidateReplayConsistency(OperationId, OperationType, Name);

        var existing = State.GetOperation(OperationId);
        return existing == null
            ? StartAsync(cancellationToken)
            : ReplayAsync(existing, cancellationToken);
    }

    /// <summary>First-time execution path: no prior checkpoint exists.</summary>
    protected abstract Task<TResult> StartAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Replay path: a checkpoint from a prior invocation exists. Subclasses
    /// switch on <paramref name="existing"/>.<see cref="Operation.Status"/>
    /// against <see cref="OperationStatuses"/> constants.
    /// </summary>
    protected abstract Task<TResult> ReplayAsync(Operation existing, CancellationToken cancellationToken);

    /// <summary>
    /// Enqueues an outbound checkpoint and awaits its batch flush. No-op when
    /// no batcher is wired (e.g. unit tests that don't exercise flushing).
    /// </summary>
    protected Task EnqueueAsync(SdkOperationUpdate update, CancellationToken cancellationToken = default)
        => Batcher?.EnqueueAsync(update, cancellationToken) ?? Task.CompletedTask;
}
