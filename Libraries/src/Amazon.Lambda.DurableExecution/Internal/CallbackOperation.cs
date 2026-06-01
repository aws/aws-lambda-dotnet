// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.IO;
using System.Text;
using Amazon.Lambda;
using Amazon.Lambda.Core;
using SdkCallbackOptions = Amazon.Lambda.Model.CallbackOptions;
using SdkOperationUpdate = Amazon.Lambda.Model.OperationUpdate;

namespace Amazon.Lambda.DurableExecution.Internal;

/// <summary>
/// Durable callback operation. Sync-flushes a <c>CALLBACK START</c> checkpoint
/// (the service stamps a freshly-allocated <c>CallbackId</c> onto the response,
/// which the batcher merges back into <see cref="ExecutionState"/>), then hands
/// the user an <see cref="ICallback{T}"/> they can later
/// <see cref="ICallback{T}.GetResultAsync(System.Threading.CancellationToken)"/>
/// to suspend on.
/// </summary>
/// <remarks>
/// Replay branches — example:
/// <code>
/// var cb = await ctx.CreateCallbackAsync&lt;ApprovalResult&gt;(name: "approval");
/// // ... external system told to use cb.CallbackId ...
/// var result = await cb.GetResultAsync();
/// </code>
/// <list type="bullet">
///   <item><b>Fresh</b>: no prior state → sync-flush <c>CALLBACK START</c>;
///       the service responds with a CallbackId (merged into state by the
///       batcher); construct the <see cref="ICallback{T}"/> and return it.
///       <see cref="GetResultAsync"/> then suspends.</item>
///   <item><b>STARTED</b>: a CallbackId is already on the checkpoint; reuse it.
///       <see cref="GetResultAsync"/> suspends (the external system hasn't
///       responded yet) — service re-invokes once it does.</item>
///   <item><b>SUCCEEDED / FAILED / TIMED_OUT</b>: terminal — construct the
///       <see cref="ICallback{T}"/> with the cached state and return.
///       <see cref="GetResultAsync"/> immediately deserializes / throws.</item>
/// </list>
/// CRITICAL: <c>CreateCallbackAsync</c> always succeeds — it returns the
/// <see cref="ICallback{T}"/> handle regardless of terminal state. Errors are
/// deferred to <see cref="ICallback{T}.GetResultAsync(System.Threading.CancellationToken)"/>
/// so user code between <c>CreateCallbackAsync</c> and the result-await runs
/// deterministically across replays.
/// <para>
/// LIFETIME: the handle returned to user code IS the operation object, so it
/// transitively roots <see cref="ExecutionState"/>, <see cref="CheckpointBatcher"/>,
/// and <see cref="TerminationManager"/>. This is invocation-scoped by design —
/// do not store an <see cref="ICallback{T}"/> across invocations (e.g. in a
/// static field on a warm Lambda container). The batcher is disposed when the
/// workflow returns and the captured state belongs to that invocation only;
/// re-using the handle later will read disposed/stale machinery.
/// </para>
/// Serialization is delegated to the <see cref="ILambdaSerializer"/> registered on
/// <see cref="ILambdaContext.Serializer"/>. AOT-safe and reflection-based callers
/// share the same code path: the AOT story is determined entirely by the serializer
/// the user registered with the runtime (e.g.,
/// <c>SourceGeneratorLambdaJsonSerializer&lt;TContext&gt;</c>).
/// </remarks>
internal sealed class CallbackOperation<T> : DurableOperation<ICallback<T>>, ICallback<T>
{
    private readonly CallbackConfig? _config;
    private readonly ILambdaSerializer _serializer;

    private string? _callbackId;

    public CallbackOperation(
        string operationId,
        string? name,
        string? parentId,
        CallbackConfig? config,
        ILambdaSerializer serializer,
        ExecutionState state,
        TerminationManager termination,
        string durableExecutionArn,
        CheckpointBatcher? batcher = null)
        : base(operationId, name, parentId, state, termination, durableExecutionArn, batcher)
    {
        _config = config;
        _serializer = serializer;
    }

    protected override string OperationType => OperationTypes.Callback;

    /// <summary>
    /// Set when an existing terminal-state checkpoint was observed during
    /// dispatch. <see cref="GetResultAsync"/> reads this directly to short-
    /// circuit deserialization (or throw the recorded error) without suspending.
    /// </summary>
    private Operation? _terminalReplay;

    /// <inheritdoc />
    public string CallbackId => _callbackId
        ?? throw new InvalidOperationException(
            "CallbackId is unavailable. Ensure CreateCallbackAsync has completed before reading CallbackId.");

    protected override async Task<ICallback<T>> StartAsync(CancellationToken cancellationToken)
    {
        // Sync-flush the START so the service can allocate a CallbackId for us.
        // The batcher's onNewOperations hook merges the service's response into
        // ExecutionState, so reading state.GetOperation(OperationId) right after
        // the await sees the populated CallbackDetails.
        await EnqueueAsync(new SdkOperationUpdate
        {
            Id = OperationId,
            Type = OperationTypes.Callback,
            Action = OperationAction.START,
            SubType = OperationSubTypes.Callback,
            Name = Name,
            CallbackOptions = BuildCallbackOptions()
        }, cancellationToken);

        var stamped = State.GetOperation(OperationId);
        var callbackId = stamped?.CallbackDetails?.CallbackId;
        if (string.IsNullOrEmpty(callbackId))
        {
            // Service didn't return a CallbackId — this is a service-contract
            // violation, not user error. Surface as a non-deterministic error
            // so the workflow fails fast rather than silently NRE-ing later.
            throw new NonDeterministicExecutionException(
                $"Callback operation '{Name ?? OperationId}' was started but the service did not return a CallbackId.");
        }

        _callbackId = callbackId;

        // If the service already reported a terminal state on the START response
        // (the external system replied synchronously, or timeout was instant),
        // record it for GetResultAsync to short-circuit on.
        if (IsTerminalStatus(stamped?.Status))
        {
            _terminalReplay = stamped;
        }

        return this;
    }

    protected override Task<ICallback<T>> ReplayAsync(Operation existing, CancellationToken cancellationToken)
    {
        var callbackId = existing.CallbackDetails?.CallbackId;
        if (string.IsNullOrEmpty(callbackId))
        {
            throw new NonDeterministicExecutionException(
                $"Callback operation '{Name ?? OperationId}' has no CallbackId on its checkpoint.");
        }

        _callbackId = callbackId;

        // CRITICAL: we must NOT raise on terminal state here.
        // CreateCallbackAsync always returns the ICallback handle so any user
        // code between create and GetResult runs deterministically across
        // replays. Defer status inspection to GetResultAsync below.
        switch (existing.Status)
        {
            case OperationStatuses.Succeeded:
            case OperationStatuses.Failed:
            case OperationStatuses.TimedOut:
                _terminalReplay = existing;
                break;

            case OperationStatuses.Started:
            case OperationStatuses.Pending:
                // External system hasn't responded yet — GetResultAsync will
                // suspend so the service can re-invoke once it does.
                break;

            default:
                throw new NonDeterministicExecutionException(
                    $"Callback operation '{Name ?? OperationId}' has unexpected status '{existing.Status}' on replay.");
        }

        return Task.FromResult<ICallback<T>>(this);
    }

    /// <inheritdoc />
    public async Task<T> GetResultAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Terminal-state checkpoint already observed by Start/Replay — return
        // (or throw) immediately without suspending.
        if (_terminalReplay != null)
        {
            return ResolveTerminal(_terminalReplay);
        }

        // A later checkpoint in this same invocation (e.g. WaitForCallback's
        // submitter step flush) may have merged a terminal status into
        // ExecutionState via NewExecutionState. Re-read once before suspending
        // so we avoid a wasted reinvocation when the answer is already here.
        var current = State.GetOperation(OperationId);
        if (IsTerminalStatus(current?.Status))
        {
            return ResolveTerminal(current!);
        }

        // No terminal state yet. Suspend the workflow; the service re-invokes
        // when the external system delivers a result.
        return await Termination.SuspendAndAwait<T>(
            TerminationReason.CallbackPending,
            $"callback:{Name ?? OperationId}");
    }

    private T ResolveTerminal(Operation op)
    {
        switch (op.Status)
        {
            case OperationStatuses.Succeeded:
                var serialized = op.CallbackDetails?.Result;
                if (serialized == null) return default!;
                {
                    var bytes = Encoding.UTF8.GetBytes(serialized);
                    using var ms = new MemoryStream(bytes);
                    return _serializer.Deserialize<T>(ms);
                }

            case OperationStatuses.Failed:
                throw BuildFailedException(op);

            case OperationStatuses.TimedOut:
                throw BuildTimeoutException(op);

            default:
                // Should be unreachable — _terminalReplay is only set for terminal statuses.
                throw new NonDeterministicExecutionException(
                    $"Callback operation '{Name ?? OperationId}' has unexpected status '{op.Status}' on result resolution.");
        }
    }

    private CallbackFailedException BuildFailedException(Operation op)
    {
        var err = op.CallbackDetails?.Error;
        var message = err?.ErrorMessage ?? "Callback failed";
        return new CallbackFailedException(message)
        {
            CallbackId = op.CallbackDetails?.CallbackId,
            ErrorType = err?.ErrorType,
            ErrorData = err?.ErrorData,
            OriginalStackTrace = err?.StackTrace,
        };
    }

    private CallbackTimeoutException BuildTimeoutException(Operation op)
    {
        var err = op.CallbackDetails?.Error;
        var message = err?.ErrorMessage ?? "Callback timed out";
        return new CallbackTimeoutException(message)
        {
            CallbackId = op.CallbackDetails?.CallbackId,
            ErrorType = err?.ErrorType,
            ErrorData = err?.ErrorData,
            OriginalStackTrace = err?.StackTrace,
        };
    }

    private SdkCallbackOptions? BuildCallbackOptions()
    {
        if (_config == null) return null;
        if (_config.Timeout == TimeSpan.Zero && _config.HeartbeatTimeout == TimeSpan.Zero) return null;

        var options = new SdkCallbackOptions();
        if (_config.Timeout > TimeSpan.Zero)
            options.TimeoutSeconds = (int)Math.Max(1, Math.Ceiling(_config.Timeout.TotalSeconds));
        if (_config.HeartbeatTimeout > TimeSpan.Zero)
            options.HeartbeatTimeoutSeconds = (int)Math.Max(1, Math.Ceiling(_config.HeartbeatTimeout.TotalSeconds));
        return options;
    }

    private static bool IsTerminalStatus(string? status) =>
        status == OperationStatuses.Succeeded
        || status == OperationStatuses.Failed
        || status == OperationStatuses.TimedOut;
}
