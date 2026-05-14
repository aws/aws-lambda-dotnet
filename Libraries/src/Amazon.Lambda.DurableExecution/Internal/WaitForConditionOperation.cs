// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using Amazon.Lambda.Core;
using Microsoft.Extensions.Logging;
using SdkErrorObject = Amazon.Lambda.Model.ErrorObject;
using SdkOperationUpdate = Amazon.Lambda.Model.OperationUpdate;
using SdkStepOptions = Amazon.Lambda.Model.StepOptions;

namespace Amazon.Lambda.DurableExecution.Internal;

/// <summary>
/// Durable wait-for-condition (polling) operation. Repeatedly invokes a
/// user-supplied check function until an <see cref="IWaitStrategy{TState}"/>
/// decides to stop. Between iterations the workflow is suspended so the
/// Lambda is not billing compute while waiting.
/// </summary>
/// <remarks>
/// Wire format reuses STEP+RETRY exactly:
/// <list type="bullet">
///   <item><c>Type=STEP</c>, <c>SubType="WaitForCondition"</c></item>
///   <item>Each polling iteration emits <c>Action=RETRY</c> with the latest
///       <typeparamref name="TState"/> in <c>Payload</c> and the strategy's
///       chosen delay in <c>StepOptions.NextAttemptDelaySeconds</c>.</item>
///   <item>Termination emits <c>Action=SUCCEED</c> with the final state in
///       <c>Payload</c>; check-function exceptions emit <c>Action=FAIL</c>.</item>
/// </list>
/// Replay branches — example: <c>await ctx.WaitForConditionAsync(check, config, "poll")</c>
/// <list type="bullet">
///   <item><b>Fresh</b>: sync-flush START → run check with <see cref="WaitForConditionConfig{TState}.InitialState"/>
///       → strategy decides Stop/Continue.</item>
///   <item><b>SUCCEEDED</b>: return the deserialized cached state; check is NOT re-run.</item>
///   <item><b>FAILED</b>: re-throw a <see cref="WaitForConditionException"/>
///       (or fall back to <see cref="StepException"/> if the FAIL was caused by
///       the check function throwing — the latter carries the original
///       error type/message).</item>
///   <item><b>PENDING</b> (RETRY scheduled): if the next-attempt timer hasn't
///       fired yet, re-suspend; otherwise read the prior state from
///       <c>StepDetails.Result</c>, advance the attempt counter, and run the
///       check again.</item>
///   <item><b>READY</b>: timer fired and the service re-invoked us. Read the
///       prior state, advance the attempt counter, run the check.</item>
///   <item><b>STARTED</b>: the START checkpoint was written but the very first
///       check attempt didn't complete (Lambda crash / timeout). Re-execute
///       with <see cref="WaitForConditionConfig{TState}.InitialState"/> as
///       the seed.</item>
/// </list>
/// State checkpointing in each RETRY's payload is what makes the polling loop
/// survive Lambda re-invocations deterministically.
/// </remarks>
internal sealed class WaitForConditionOperation<TState> : DurableOperation<TState>
{
    private readonly Func<TState, IConditionCheckContext, Task<TState>> _check;
    private readonly WaitForConditionConfig<TState> _config;
    private readonly ILambdaSerializer _serializer;
    private readonly ILogger _logger;

    public WaitForConditionOperation(
        string operationId,
        string? name,
        string? parentId,
        Func<TState, IConditionCheckContext, Task<TState>> check,
        WaitForConditionConfig<TState> config,
        ILambdaSerializer serializer,
        ILogger logger,
        ExecutionState state,
        TerminationManager termination,
        string durableExecutionArn,
        CheckpointBatcher? batcher = null)
        : base(operationId, name, parentId, state, termination, durableExecutionArn, batcher)
    {
        _check = check;
        _config = config;
        _serializer = serializer;
        _logger = logger;
    }

    protected override string OperationType => OperationTypes.Step;

    protected override Task<TState> StartAsync(CancellationToken cancellationToken)
        => ExecuteIteration(_config.InitialState, attemptNumber: 1, cancellationToken);

    protected override Task<TState> ReplayAsync(Operation existing, CancellationToken cancellationToken)
    {
        switch (existing.Status)
        {
            case OperationStatuses.Succeeded:
                // Polling concluded on a previous invocation; return the
                // cached final state without re-running the check.
                return Task.FromResult(DeserializeState(existing.StepDetails?.Result));

            case OperationStatuses.Failed:
                throw BuildFailureException(existing);

            case OperationStatuses.Pending:
                return ReplayPending(existing, cancellationToken);

            case OperationStatuses.Ready:
                return ReplayReady(existing, cancellationToken);

            case OperationStatuses.Started:
                // START emitted but no RETRY/SUCCEED yet — the very first
                // check attempt was lost. Re-execute with InitialState. Do
                // NOT re-emit START (the original is authoritative).
                return ExecuteIteration(_config.InitialState, attemptNumber: 1, cancellationToken);

            default:
                throw new NonDeterministicExecutionException(
                    $"WaitForCondition operation '{Name ?? OperationId}' has unexpected status '{existing.Status}' on replay.");
        }
    }

    /// <summary>
    /// PENDING means the prior iteration emitted RETRY and the service
    /// scheduled a timer. If the timer hasn't fired we re-suspend; once it
    /// fires, the next iteration runs against the previously checkpointed
    /// state, NOT <see cref="WaitForConditionConfig{TState}.InitialState"/>.
    /// </summary>
    private Task<TState> ReplayPending(Operation pending, CancellationToken cancellationToken)
    {
        var nextAttemptTs = pending.StepDetails?.NextAttemptTimestamp;
        if (nextAttemptTs is { } scheduledMs &&
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() < scheduledMs)
        {
            // Timer still ticking — re-suspend without re-checkpointing.
            return Termination.SuspendAndAwait<TState>(
                TerminationReason.RetryScheduled, $"wait_for_condition:{Name ?? OperationId}");
        }

        var priorState = DeserializeStateOrInitial(pending.StepDetails?.Result);
        var attemptNumber = (pending.StepDetails?.Attempt ?? 0) + 1;
        return ExecuteIteration(priorState, attemptNumber, cancellationToken);
    }

    /// <summary>
    /// READY means the service has re-invoked us post-PENDING — the next
    /// poll is up. Read the latest state from the prior RETRY's payload
    /// and advance the attempt counter.
    /// </summary>
    private Task<TState> ReplayReady(Operation ready, CancellationToken cancellationToken)
    {
        var priorState = DeserializeStateOrInitial(ready.StepDetails?.Result);
        var attemptNumber = (ready.StepDetails?.Attempt ?? 0) + 1;
        return ExecuteIteration(priorState, attemptNumber, cancellationToken);
    }

    private async Task<TState> ExecuteIteration(TState currentState, int attemptNumber, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Emit START on the very first attempt only — and sync-flush so the
        // service has a record of the polling op even if the check function
        // drives termination via, e.g., a wait inside it. Subsequent
        // iterations resume from a RETRY/READY/PENDING checkpoint and skip
        // START.
        if (State.GetOperation(OperationId) == null)
        {
            await EnqueueAsync(new SdkOperationUpdate
            {
                Id = OperationId,
                ParentId = ParentId,
                Type = OperationTypes.Step,
                Action = OperationAction.START,
                SubType = OperationSubTypes.WaitForCondition,
                Name = Name
            }, cancellationToken);
        }

        TState newState;
        try
        {
            var checkContext = new ConditionCheckContext(attemptNumber, _logger);
            newState = await _check(currentState, checkContext);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // The check threw. WaitForCondition has no per-exception retry
            // strategy (Python/JS/Java SDKs all treat check failure as terminal),
            // so checkpoint FAIL and surface the original exception via
            // StepException — same shape as StepOperation's terminal failure.
            await EnqueueAsync(new SdkOperationUpdate
            {
                Id = OperationId,
                ParentId = ParentId,
                Type = OperationTypes.Step,
                Action = OperationAction.FAIL,
                SubType = OperationSubTypes.WaitForCondition,
                Name = Name,
                Error = ToSdkError(ex)
            }, cancellationToken);

            throw new StepException(ex.Message, ex)
            {
                ErrorType = ex.GetType().FullName
            };
        }

        WaitDecision decision;
        try
        {
            decision = _config.WaitStrategy.Decide(newState, attemptNumber);
        }
        catch (WaitForConditionException maxEx)
        {
            // Strategy is signaling max-attempts reached. The strategy
            // didn't have access to LastState; we do — populate it now,
            // checkpoint FAIL, and rethrow.
            var enriched = new WaitForConditionException(
                $"WaitForCondition '{Name ?? OperationId}' exhausted {attemptNumber} attempts without the condition being met.",
                maxEx)
            {
                AttemptsExhausted = attemptNumber,
                LastState = newState
            };

            // Persist the last observed state in Error.ErrorData so a replay
            // that hits this cached FAIL can reconstruct LastState identically
            // to the live throw. The wire protocol forbids a Payload on FAIL
            // updates, so ErrorData is the only field that survives replay.
            await EnqueueAsync(new SdkOperationUpdate
            {
                Id = OperationId,
                ParentId = ParentId,
                Type = OperationTypes.Step,
                Action = OperationAction.FAIL,
                SubType = OperationSubTypes.WaitForCondition,
                Name = Name,
                Error = new SdkErrorObject
                {
                    ErrorType = typeof(WaitForConditionException).FullName,
                    ErrorMessage = enriched.Message,
                    ErrorData = SerializeState(newState)
                }
            }, cancellationToken);

            throw enriched;
        }

        if (!decision.ShouldContinue)
        {
            // Stop() means the condition has been met. Persist the final
            // state and return it to the caller.
            await EnqueueAsync(new SdkOperationUpdate
            {
                Id = OperationId,
                ParentId = ParentId,
                Type = OperationTypes.Step,
                Action = OperationAction.SUCCEED,
                SubType = OperationSubTypes.WaitForCondition,
                Name = Name,
                Payload = SerializeState(newState)
            }, cancellationToken);

            return newState;
        }

        // Continue polling — emit RETRY with the new state in the payload
        // and the next-attempt delay in StepOptions. Sync-flush so the
        // service definitely has the new state and timer scheduled before
        // we suspend.
        var delaySeconds = (int)Math.Max(1, Math.Ceiling(decision.Delay.TotalSeconds));
        await EnqueueAsync(new SdkOperationUpdate
        {
            Id = OperationId,
            ParentId = ParentId,
            Type = OperationTypes.Step,
            Action = OperationAction.RETRY,
            SubType = OperationSubTypes.WaitForCondition,
            Name = Name,
            Payload = SerializeState(newState),
            StepOptions = new SdkStepOptions { NextAttemptDelaySeconds = delaySeconds }
        }, cancellationToken);

        return await Termination.SuspendAndAwait<TState>(
            TerminationReason.RetryScheduled, $"wait_for_condition:{Name ?? OperationId}");
    }

    private TState DeserializeState(string? serialized)
    {
        if (serialized == null) return default!;
        var bytes = Encoding.UTF8.GetBytes(serialized);
        using var ms = new MemoryStream(bytes);
        return _serializer.Deserialize<TState>(ms);
    }

    private TState DeserializeStateOrInitial(string? serialized)
    {
        if (serialized == null) return _config.InitialState;
        try
        {
            var bytes = Encoding.UTF8.GetBytes(serialized);
            using var ms = new MemoryStream(bytes);
            return _serializer.Deserialize<TState>(ms);
        }
        catch (Exception ex)
        {
            // If the serializer can't read the prior state, fall back to
            // InitialState — matches Python's behavior. Log a warning so
            // corrupted payloads / schema migrations are observable instead
            // of silently restarting the polling loop.
            _logger.LogWarning(
                "WaitForCondition operation '{OperationId}' failed to deserialize prior state ({ExceptionType}: {Message}); falling back to InitialState.",
                OperationId, ex.GetType().FullName, ex.Message);
            return _config.InitialState;
        }
    }

    private string SerializeState(TState value)
    {
        using var ms = new MemoryStream();
        _serializer.Serialize(value, ms);
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private Exception BuildFailureException(Operation failedOp)
    {
        var err = failedOp.StepDetails?.Error;
        // Distinguish "max attempts exhausted" (we recorded the type as
        // WaitForConditionException above) from "check function threw"
        // (recorded as the original exception type via StepException).
        if (err?.ErrorType == typeof(WaitForConditionException).FullName)
        {
            // Recover LastState from the FAIL checkpoint's Error.ErrorData.
            // Live execution serializes the most recent state alongside the
            // error so replay surfaces an identically-populated exception.
            // Falls back to null when ErrorData is absent (legacy data
            // pre-dating this serialization) or unreadable.
            object? lastState = null;
            var lastStatePayload = err?.ErrorData;
            if (lastStatePayload != null)
            {
                try
                {
                    var bytes = Encoding.UTF8.GetBytes(lastStatePayload);
                    using var ms = new MemoryStream(bytes);
                    lastState = _serializer.Deserialize<TState>(ms);
                }
                catch (Exception deserEx)
                {
                    _logger.LogWarning(
                        "WaitForCondition operation '{OperationId}' failed to deserialize LastState from FAIL checkpoint ErrorData ({ExceptionType}: {Message}); LastState will be null on the rethrown exception.",
                        OperationId, deserEx.GetType().FullName, deserEx.Message);
                }
            }

            return new WaitForConditionException(err?.ErrorMessage ?? $"WaitForCondition '{Name ?? OperationId}' exhausted attempts.")
            {
                AttemptsExhausted = failedOp.StepDetails?.Attempt ?? 0,
                LastState = lastState
            };
        }

        return new StepException(err?.ErrorMessage ?? "WaitForCondition check function failed")
        {
            ErrorType = err?.ErrorType,
            ErrorData = err?.ErrorData,
            OriginalStackTrace = err?.StackTrace
        };
    }

    private static SdkErrorObject ToSdkError(Exception ex) => new()
    {
        ErrorType = ex.GetType().FullName,
        ErrorMessage = ex.Message,
        StackTrace = ex.StackTrace?.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).ToList()
    };
}

/// <summary>
/// Internal implementation of <see cref="IConditionCheckContext"/>.
/// </summary>
internal sealed class ConditionCheckContext : IConditionCheckContext
{
    public ConditionCheckContext(int attemptNumber, ILogger logger)
    {
        AttemptNumber = attemptNumber;
        Logger = logger;
    }

    public ILogger Logger { get; }
    public int AttemptNumber { get; }
}
