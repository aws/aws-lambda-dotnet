using SdkOperationUpdate = Amazon.Lambda.Model.OperationUpdate;
using SdkWaitOptions = Amazon.Lambda.Model.WaitOptions;

namespace Amazon.Lambda.DurableExecution.Internal;

/// <summary>
/// Durable wait operation. Suspends the workflow for a given duration without
/// consuming compute time; the service schedules a timer and re-invokes Lambda
/// when it fires.
/// </summary>
/// <remarks>
/// Replay semantics — example: <c>await ctx.WaitAsync(TimeSpan.FromHours(1))</c>
/// <list type="bullet">
///   <item>Fresh: emit WAIT START → flush → suspend → service schedules timer.</item>
///   <item>Replay (SUCCEEDED): timer fired, return CompletedTask.</item>
///   <item>Replay (STARTED/PENDING): timer still ticking → re-suspend (or
///       short-circuit if the deadline already elapsed but SUCCEEDED hasn't
///       been stamped yet).</item>
/// </list>
/// See <see cref="DurableExecutionHandler.RunAsync{TResult}"/> for the
/// suspension mechanics (Task.WhenAny race against TerminationManager).
/// </remarks>
internal sealed class WaitOperation : DurableOperation<object?>
{
    private readonly int _waitSeconds;

    public WaitOperation(
        string operationId,
        string? name,
        int waitSeconds,
        ExecutionState state,
        TerminationManager termination,
        string durableExecutionArn,
        CheckpointBatcher? batcher = null)
        : base(operationId, name, state, termination, durableExecutionArn, batcher)
    {
        _waitSeconds = waitSeconds;
    }

    protected override string OperationType => OperationTypes.Wait;

    protected override async Task<object?> StartAsync(CancellationToken cancellationToken)
    {
        State.EnterExecutionMode();

        // Sync-flush WAIT START before suspending — the service can't schedule
        // a timer for a checkpoint it hasn't received.
        await EnqueueAsync(new SdkOperationUpdate
        {
            Id = OperationId,
            Type = OperationTypes.Wait,
            Action = "START",
            SubType = "Wait",
            Name = Name,
            WaitOptions = new SdkWaitOptions { WaitSeconds = _waitSeconds }
        }, cancellationToken);

        return await Termination.SuspendAndAwait<object?>(
            TerminationReason.WaitScheduled, $"wait:{Name ?? OperationId}");
    }

    protected override Task<object?> ReplayAsync(Operation existing, CancellationToken cancellationToken)
    {
        switch (existing.Status)
        {
            case OperationStatuses.Succeeded:
                // Common post-timer case: service stamped the wait as SUCCEEDED
                // and re-invoked Lambda. Workflow proceeds to the next step.
                return Task.FromResult<object?>(null);

            case OperationStatuses.Started:
            case OperationStatuses.Pending:
                // Service hasn't marked the wait complete yet. Either the timer
                // is still ticking, or the deadline elapsed but SUCCEEDED hasn't
                // been stamped yet — treat elapsed deadlines as "done" to avoid
                // a pointless extra round-trip.
                var expiresAtMs = existing.WaitDetails?.ScheduledEndTimestamp;
                if (expiresAtMs is { } ts && DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() >= ts)
                {
                    return Task.FromResult<object?>(null);
                }

                // Timer still ticking — re-suspend without re-checkpointing.
                // The original WAIT START is still authoritative.
                return Termination.SuspendAndAwait<object?>(
                    TerminationReason.WaitScheduled, $"wait:{Name ?? OperationId}");

            default:
                throw new NonDeterministicExecutionException(
                    $"Wait operation '{Name ?? OperationId}' has unexpected status '{existing.Status}' on replay.");
        }
    }
}
