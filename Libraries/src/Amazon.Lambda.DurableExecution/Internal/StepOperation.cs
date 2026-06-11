// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.IO;
using System.Text;
using Amazon.Lambda;
using Amazon.Lambda.Core;
using Microsoft.Extensions.Logging;
using SdkErrorObject = Amazon.Lambda.Model.ErrorObject;
using SdkOperationUpdate = Amazon.Lambda.Model.OperationUpdate;
using SdkStepOptions = Amazon.Lambda.Model.StepOptions;

namespace Amazon.Lambda.DurableExecution.Internal;

/// <summary>
/// Durable step operation. Runs the user's function (with retry support),
/// persisting its result so subsequent invocations replay the cached value
/// without re-executing.
/// </summary>
/// <remarks>
/// Replay branches — example: <c>await ctx.StepAsync(ChargeCard, "charge")</c>
/// <list type="bullet">
///   <item><b>Fresh</b>: no prior state → run func → emit SUCCEED → return.</item>
///   <item><b>SUCCEEDED</b>: return cached result; func is NOT re-executed.</item>
///   <item><b>FAILED</b>: re-throw the recorded exception.</item>
///   <item><b>PENDING</b> (retry timer not yet fired): re-suspend without
///       running func; service re-invokes once <c>NextAttemptTimestamp</c> elapses.</item>
///   <item><b>STARTED</b> + AtMostOncePerRetry: crash recovery — treat as a
///       failed attempt, route through retry strategy.</item>
///   <item><b>READY</b>: service has post-PENDING re-invoked us; the retry
///       timer fired and the next attempt is up. Run it.</item>
/// </list>
/// Serialization is delegated to the <see cref="ILambdaSerializer"/> registered on
/// <see cref="ILambdaContext.Serializer"/>. AOT-safe and reflection-based callers
/// share the same code path: the AOT story is determined entirely by the serializer
/// the user registered with the runtime (e.g.,
/// <c>SourceGeneratorLambdaJsonSerializer&lt;TContext&gt;</c>).
/// </remarks>
internal sealed class StepOperation<T> : DurableOperation<T>
{
    private readonly Func<IStepContext, CancellationToken, Task<T>> _func;
    private readonly StepConfig? _config;
    private readonly ILambdaSerializer _serializer;
    private readonly ILogger _logger;
    private readonly WorkflowCancellation _workflowCancellation;

    public StepOperation(
        string operationId,
        string? name,
        string? parentId,
        Func<IStepContext, CancellationToken, Task<T>> func,
        StepConfig? config,
        ILambdaSerializer serializer,
        ILogger logger,
        ExecutionState state,
        TerminationManager termination,
        WorkflowCancellation workflowCancellation,
        string durableExecutionArn,
        CheckpointBatcher? batcher = null)
        : base(operationId, name, parentId, state, termination, durableExecutionArn, batcher)
    {
        _func = func;
        _config = config;
        _serializer = serializer;
        _logger = logger;
        _workflowCancellation = workflowCancellation;
    }

    protected override string OperationType => OperationTypes.Step;

    protected override Task<T> StartAsync(CancellationToken cancellationToken)
        => ExecuteFunc(attemptNumber: 1, cancellationToken);

    protected override Task<T> ReplayAsync(Operation existing, CancellationToken cancellationToken)
    {
        switch (existing.Status)
        {
            case OperationStatuses.Succeeded:
                // Side-effecting code runs at most once: replay returns the
                // cached result without invoking func.
                return Task.FromResult(DeserializeResult(existing.StepDetails?.Result));

            case OperationStatuses.Failed:
                // Retries were exhausted or never configured — re-throw so the
                // user's catch-block flow matches the original execution.
                throw CreateStepException(existing);

            case OperationStatuses.Pending:
                return ReplayPending(existing, cancellationToken);

            case OperationStatuses.Started:
                return ReplayStarted(existing, cancellationToken);

            case OperationStatuses.Ready:
                return ReplayReady(existing, cancellationToken);

            default:
                // CANCELLED / STOPPED / unrecognized status. Re-running the
                // step would re-execute side effects and silently mask a
                // service-state we don't know how to interpret. Fail loud.
                throw new NonDeterministicExecutionException(
                    $"Step operation '{Name ?? OperationId}' has unexpected status '{existing.Status}' on replay.");
        }
    }

    /// <summary>
    /// READY means the service has post-PENDING re-invoked us — the retry
    /// timer fired and the step is eligible to run its next attempt. No
    /// timer check is needed (the service has already decided we're up);
    /// just advance the attempt counter and execute.
    /// </summary>
    private Task<T> ReplayReady(Operation ready, CancellationToken cancellationToken)
    {
        var attemptNumber = (ready.StepDetails?.Attempt ?? 0) + 1;
        return ExecuteFunc(attemptNumber, cancellationToken);
    }

    /// <summary>
    /// PENDING means a retry was scheduled (RETRY checkpoint). The service's
    /// transition to READY when the timer fires is the authoritative "timer
    /// fired" signal; we still get re-invoked in PENDING only if the service
    /// re-invokes slightly early. The wall-clock check below is a safety net
    /// for that case — clock skew can't cause a missed retry because if our
    /// clock is fast we just run early, and if it's slow we re-suspend and
    /// the service's READY transition takes over.
    /// </summary>
    private Task<T> ReplayPending(Operation pending, CancellationToken cancellationToken)
    {
        var nextAttemptTs = pending.StepDetails?.NextAttemptTimestamp;
        var attemptNumber = (pending.StepDetails?.Attempt ?? 0) + 1;

        if (nextAttemptTs is { } scheduledMs &&
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() < scheduledMs)
        {
            // Retry timer hasn't fired yet — re-suspend so we don't bill compute
            // while the timer ticks. Service re-invokes once the timer elapses.
            return Termination.SuspendAndAwait<T>(
                TerminationReason.RetryScheduled, $"retry:{Name ?? OperationId}");
        }

        return ExecuteFunc(attemptNumber, cancellationToken);
    }

    /// <summary>
    /// STARTED means a START checkpoint was written but no SUCCEED/FAIL exists.
    /// For AtMostOncePerRetry this signals a crash mid-step — treat as failure
    /// and route through retry. For AtLeastOncePerRetry just re-execute.
    /// </summary>
    private Task<T> ReplayStarted(Operation started, CancellationToken cancellationToken)
    {
        var attemptNumber = (started.StepDetails?.Attempt ?? 0) + 1;

        if (_config?.Semantics == StepSemantics.AtMostOncePerRetry)
        {
            // Re-running func would risk a duplicate side effect (e.g. double
            // charge). Treat the lost result as a failure; let the retry
            // strategy decide whether to try again or give up.
            //
            // Surface as StepInterruptedException so user strategies can
            // distinguish "my code threw" from "a prior attempt crashed before
            // recording a terminal record".
            var error = started.StepDetails?.Error;
            var ex = error != null
                ? new StepInterruptedException(error.ErrorMessage ?? "Step failed on previous attempt") { ErrorType = error.ErrorType }
                : new StepInterruptedException("Step result lost during AtMostOncePerRetry replay");
            return HandleStepFailureAsync(ex, attemptNumber, cancellationToken);
        }

        return ExecuteFunc(attemptNumber, cancellationToken);
    }

    private async Task<T> ExecuteFunc(int attemptNumber, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Emit a START checkpoint before running user code, unless we're already
        // resuming a STARTED record (which means an earlier attempt wrote it).
        //
        // AtMostOncePerRetry: SYNC flush. If Lambda crashes before SUCCEED is
        // flushed, ReplayStarted routes through retry instead of re-executing.
        // A queued-but-unflushed START is indistinguishable from "never ran" if
        // we die, so the sync flush is correctness-load-bearing here.
        //
        // AtLeastOncePerRetry (default): FIRE-AND-FORGET. Replay correctness
        // doesn't depend on the START — SUCCEED alone is sufficient — so this
        // is purely telemetry (attempt timing, retry count visible in history).
        if (State.GetOperation(OperationId)?.Status != OperationStatuses.Started)
        {
            var startUpdate = new SdkOperationUpdate
            {
                Id = OperationId,
                ParentId = ParentId,
                Type = OperationTypes.Step,
                Action = OperationAction.START,
                SubType = OperationSubTypes.Step,
                Name = Name
            };

            if (_config?.Semantics == StepSemantics.AtMostOncePerRetry)
            {
                await EnqueueAsync(startUpdate, cancellationToken);
            }
            else
            {
                FireAndForget(EnqueueAsync(startUpdate, cancellationToken));
            }
        }


        // Link the caller's token with the workflow-shutdown token so the user
        // step body observes both upstream cancel intent and SDK-driven workflow
        // teardown. The linked token is passed to the user Func only; checkpoint
        // writes still use the caller's token (workflow shutdown must NOT abort
        // a successful step's SUCCEED checkpoint — see cancellation-design.md §7).
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, _workflowCancellation.Token);

        try
        {
            var stepContext = new StepContext(OperationId, attemptNumber, _logger);

            // Step-scoped metadata so structured log providers tag user code
            // lines with the operation id, name, and current attempt. Wrap
            // only the user-func call — checkpoint emission shouldn't carry
            // step metadata into any side-channel logging.
            T result;
            using (_logger.BeginScope(new Dictionary<string, object>
            {
                ["operationId"] = OperationId,
                ["operationName"] = Name ?? string.Empty,
                ["attempt"] = attemptNumber,
            }))
            {
                result = await _func(stepContext, linked.Token);
            }

            await EnqueueAsync(new SdkOperationUpdate
            {
                Id = OperationId,
                ParentId = ParentId,
                Type = OperationTypes.Step,
                Action = OperationAction.SUCCEED,
                SubType = OperationSubTypes.Step,
                Name = Name,
                Payload = SerializeResult(result)
            }, cancellationToken);

            return result;
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested)
        {
            // Cancellation owned by the linked source (caller-cancel or workflow
            // shutdown). Do NOT checkpoint FAIL and do NOT consult the retry
            // strategy — the termination signal that fired (if any) owns the
            // suspend/abort decision; an upstream caller-cancel propagates up
            // as a fault on the workflow user task.
            throw;
        }
        catch (Exception ex)
        {
            // Funnel into the retry/fail decision tree. May checkpoint RETRY and
            // suspend (Pending), or checkpoint FAIL and rethrow to user. A user-
            // thrown OperationCanceledException unrelated to our linked token
            // falls through here and is treated as a normal step failure.
            return await HandleStepFailureAsync(ex, attemptNumber, cancellationToken);
        }
    }

    /// <summary>
    /// Funnels a step failure into the retry/fail decision. May checkpoint
    /// RETRY and suspend (Pending), or checkpoint FAIL and rethrow.
    /// </summary>
    private async Task<T> HandleStepFailureAsync(Exception ex, int attemptNumber, CancellationToken cancellationToken)
    {
        var retryStrategy = _config?.RetryStrategy;
        if (retryStrategy != null)
        {
            var decision = retryStrategy.ShouldRetry(ex, attemptNumber);
            if (decision.ShouldRetry)
            {
                // Service requires NextAttemptDelaySeconds >= 1. Built-in
                // strategies already produce >=1s delays; this guard only
                // matters for user-supplied IRetryStrategy / FromDelegate.
                var requestedSeconds = decision.Delay.TotalSeconds;
                var delaySeconds = (int)Math.Max(1, Math.Ceiling(requestedSeconds));
                if (requestedSeconds < 1)
                {
                    _logger.LogWarning(
                        "Retry delay for step '{StepName}' attempt {Attempt} was {Requested:F3}s (< 1s); coerced to {Coerced}s.",
                        Name ?? OperationId, attemptNumber, requestedSeconds, delaySeconds);
                }
                await EnqueueAsync(new SdkOperationUpdate
                {
                    Id = OperationId,
                    ParentId = ParentId,
                    Type = OperationTypes.Step,
                    Action = OperationAction.RETRY,
                    SubType = OperationSubTypes.Step,
                    Name = Name,
                    Error = ToSdkError(ex),
                    StepOptions = new SdkStepOptions { NextAttemptDelaySeconds = delaySeconds }
                }, cancellationToken);
                return await Termination.SuspendAndAwait<T>(
                    TerminationReason.RetryScheduled, $"retry:{Name ?? OperationId}");
            }
        }

        await EnqueueAsync(new SdkOperationUpdate
        {
            Id = OperationId,
            ParentId = ParentId,
            Type = OperationTypes.Step,
            Action = OperationAction.FAIL,
            SubType = OperationSubTypes.Step,
            Name = Name,
            Error = ToSdkError(ex)
        }, cancellationToken);

        throw new StepException(ex.Message, ex)
        {
            ErrorType = ex.GetType().FullName
        };
    }

    private T DeserializeResult(string? serialized)
    {
        if (serialized == null) return default!;
        var bytes = Encoding.UTF8.GetBytes(serialized);
        using var ms = new MemoryStream(bytes);
        return _serializer.Deserialize<T>(ms);
    }

    private string SerializeResult(T value)
    {
        using var ms = new MemoryStream();
        _serializer.Serialize(value, ms);
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private static StepException CreateStepException(Operation failedOp)
    {
        var err = failedOp.StepDetails?.Error;
        return new StepException(err?.ErrorMessage ?? "Step failed")
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

    /// <summary>
    /// Discards a Task but observes any exception so it doesn't surface as an
    /// <c>UnobservedTaskException</c>. Used for fire-and-forget START checkpoints
    /// under AtLeastOncePerRetry semantics. The actual error still propagates
    /// via <c>CheckpointBatcher._terminalError</c>: the next sync EnqueueAsync
    /// or DrainAsync will rethrow with the original cause.
    /// </summary>
    private static void FireAndForget(Task task)
    {
        _ = task.ContinueWith(
            static t => _ = t.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }
}
