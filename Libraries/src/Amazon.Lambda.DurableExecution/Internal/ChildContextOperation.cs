// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.IO;
using System.Text;
using Amazon.Lambda;
using Amazon.Lambda.Core;
using SdkContextOptions = Amazon.Lambda.Model.ContextOptions;
using SdkErrorObject = Amazon.Lambda.Model.ErrorObject;
using SdkOperationUpdate = Amazon.Lambda.Model.OperationUpdate;

namespace Amazon.Lambda.DurableExecution.Internal;

/// <summary>
/// Durable child context operation. Runs a user-supplied function inside a
/// nested <see cref="DurableContext"/> with its own deterministic operation-ID
/// space, persisting the function's result so subsequent invocations replay
/// the cached value without re-executing.
/// </summary>
/// <remarks>
/// Replay branches — example: <c>await ctx.RunInChildContextAsync(child =&gt; ..., name: "phase")</c>
/// <list type="bullet">
///   <item><b>Fresh</b>: no prior state → sync-flush CONTEXT START → run user
///       func → on success emit CONTEXT SUCCEED → on failure emit CONTEXT FAIL
///       and throw <see cref="ChildContextException"/>.</item>
///   <item><b>SUCCEEDED</b>: return cached deserialized result; user func is
///       NOT re-executed.</item>
///   <item><b>SUCCEEDED (overflow)</b>: <c>ReplayChildren=true</c> + empty
///       payload (the result was too large to checkpoint inline) → re-run the
///       user func to recover the large result value; terminal checkpoints
///       (SUCCEED/FAIL) are suppressed since the op is already terminal.</item>
///   <item><b>FAILED</b>: throw <see cref="ChildContextException"/> with the
///       recorded error; if <see cref="ChildContextConfig.ErrorMapping"/> is
///       set, the mapped exception is thrown instead.</item>
///   <item><b>STARTED</b> / <b>PENDING</b>: re-run the user func without
///       re-checkpointing START. The child's own operations recover from their
///       own checkpoints, so this is replay propagation; if a wait/callback
///       inside the child is still pending, the user func re-suspends.</item>
/// </list>
/// Unlike <see cref="StepOperation{T}"/>, child contexts have no retry strategy:
/// failure is terminal and surfaces immediately via
/// <see cref="ChildContextException"/>.
/// </remarks>
internal sealed class ChildContextOperation<T> : DurableOperation<T>
{
    private readonly Func<IDurableContext, CancellationToken, Task<T>> _func;
    private readonly ChildContextConfig? _config;
    private readonly ILambdaSerializer _serializer;
    private readonly Func<string, string?, bool, IDurableContext> _childContextFactory;
    private readonly WorkflowCancellation _workflowCancellation;
    private readonly CancellationToken _cooperativeBailToken;
    private readonly bool _isVirtual;
    // Set once on overflow-replay re-execution; never reset.
    private bool _suppressTerminalCheckpoint;

    /// <summary>
    /// The exact SUCCEED-checkpoint payload this (non-virtual) child wrote on a fresh,
    /// non-overflow success — i.e. <c>S(body)</c>, serialized with the child's own
    /// <c>OperationId</c> as the entity id. A Nested Map/Parallel
    /// parent captures this so it can inline the child's ORIGINAL checkpoint payload
    /// verbatim, rather than re-serializing the (already round-tripped) return value.
    /// Re-serializing the return value double-transforms a non-round-tripping serializer
    /// (fresh vs replay diverge) and, for a context-aware serializer such as
    /// <see cref="FileSystemSerializer"/>, writes a SECOND file at the parent's per-unit id
    /// that orphans the child's file. <c>null</c> for virtual (Flat) children, for
    /// overflow-replay re-execution, and before a fresh success has been serialized.
    /// </summary>
    internal string? SerializedResultPayload { get; private set; }

    public ChildContextOperation(
        string operationId,
        string? name,
        string? parentId,
        Func<IDurableContext, CancellationToken, Task<T>> func,
        ChildContextConfig? config,
        ILambdaSerializer serializer,
        Func<string, string?, bool, IDurableContext> childContextFactory,
        ExecutionState state,
        TerminationManager termination,
        WorkflowCancellation workflowCancellation,
        string durableExecutionArn,
        CheckpointBatcher? batcher = null,
        CancellationToken cooperativeBailToken = default,
        bool isVirtual = false)
        : base(operationId, name, parentId, state, termination, durableExecutionArn, batcher)
    {
        _func = func;
        _config = config;
        _serializer = serializer;
        _childContextFactory = childContextFactory;
        _workflowCancellation = workflowCancellation;
        _cooperativeBailToken = cooperativeBailToken;
        _isVirtual = isVirtual;
    }

    protected override string OperationType => OperationTypes.Context;

    protected override async Task<T> StartAsync(CancellationToken cancellationToken)
    {
        // Virtual (NestingType.Flat) branches emit no CONTEXT checkpoint of their
        // own — the parallel/map orchestrator records their outcome inline on the
        // parent payload. Inner operations still checkpoint (re-parented to the
        // non-virtual ancestor via the virtual child generator's reported
        // ParentId), so a suspend inside a virtual branch is still recoverable.
        if (!_isVirtual)
        {
            // Sync-flush CONTEXT START before user code so the service has a record
            // of the parent context if the inner func suspends (e.g. a Wait inside
            // the child terminates the workflow before SUCCEED is reached).
            await EnqueueAsync(new SdkOperationUpdate
            {
                Id = OperationId,
                ParentId = ParentId,
                Type = OperationTypes.Context,
                Action = OperationAction.START,
                SubType = _config?.SubType,
                Name = Name
            }, cancellationToken);
        }

        return await ExecuteFunc(cancellationToken);
    }

    protected override Task<T> ReplayAsync(Operation existing, CancellationToken cancellationToken)
    {
        switch (existing.Status)
        {
            case OperationStatuses.Succeeded:
                // Overflow: the result was too large to checkpoint inline
                // (ReplayChildren=true, empty payload). Re-run the body to recover
                // the value; the body's inner ops replay from their own
                // checkpoints. Do NOT re-emit the (already terminal) SUCCEED.
                if (existing.ContextDetails?.ReplayChildren == true)
                {
                    return ExecuteFuncNoCheckpoint(cancellationToken);
                }
                // Side-effecting code runs at most once: replay returns the
                // cached result without invoking the user func.
                return Task.FromResult(DeserializeResult(existing.ContextDetails?.Result));

            case OperationStatuses.Failed:
                throw MapFailureException(BuildChildContextException(existing));

            case OperationStatuses.Started:
            case OperationStatuses.Pending:
                // Re-run the user func: the child's own operations replay from
                // their own checkpoints. Do NOT re-checkpoint START — the
                // original is still authoritative. If something inside the
                // child is still pending (Wait, callback, retry) the user func
                // will re-suspend on its own.
                return ExecuteFunc(cancellationToken);

            default:
                throw new NonDeterministicExecutionException(
                    $"Child context operation '{Name ?? OperationId}' has unexpected status '{existing.Status}' on replay.");
        }
    }

    private Task<T> ExecuteFuncNoCheckpoint(CancellationToken cancellationToken)
    {
        _suppressTerminalCheckpoint = true;
        return ExecuteFunc(cancellationToken);
    }

    private async Task<T> ExecuteFunc(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // For a virtual (Flat) branch, inner operations report this branch's own
        // ParentId — the non-virtual parallel/map ancestor — since the branch
        // itself emits no CONTEXT checkpoint to reference. For a normal child
        // context the reported parent is ignored (it roots at OperationId).
        var childContext = _childContextFactory(OperationId, ParentId, _isVirtual);

        // Link the caller's token with the workflow-shutdown token, plus the
        // optional cooperative-bail token (a parallel parent signals this when
        // a CompletionConfig short-circuit fires, asking still-running branches
        // to unwind early). The user func observes all three signals; the SDK's
        // checkpoint writes (CONTEXT FAIL / SUCCEED below) continue to use the
        // caller's token only, so a bail or shutdown can never abort a branch
        // that is mid-flush of a successful checkpoint.
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, _workflowCancellation.Token, _cooperativeBailToken);

        T result;
        try
        {
            result = await _func(childContext, linked.Token);
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested)
        {
            // Cancellation owned by the linked source — caller cancel or workflow
            // shutdown. Do NOT checkpoint CONTEXT FAIL: the termination signal
            // (or upstream cancel) owns the outcome.
            throw;
        }
        catch (NonDeterministicExecutionException)
        {
            // Replay-mismatch from an inner operation means the entire execution
            // is corrupt — checkpointing this as CONTEXT FAIL would freeze the
            // mismatch into history and prevent future invocations from
            // re-detecting it. Bubble up untouched.
            throw;
        }
        catch (StepInterruptedException)
        {
            // AtMostOncePerRetry crash recovery: a step inside the child saw a
            // STARTED checkpoint with no terminal record and routed through its
            // retry strategy. The step has already checkpointed its own outcome;
            // wrapping this as CONTEXT FAIL would mask that. Bubble up so the
            // step's strategy / replay flow stays authoritative.
            throw;
        }
        catch (Exception ex)
        {
            // Virtual branches suppress the FAIL checkpoint but still propagate
            // the exception — the orchestrator records the failure inline on the
            // parent payload. Overflow-replay re-execution also suppresses it: the
            // op is already terminal (SUCCEEDED) in the store, so re-emitting a
            // FAIL would corrupt that record (mirrors ReplayChildrenAsync, which
            // never re-checkpoints). The exception still propagates below.
            if (!_isVirtual && !_suppressTerminalCheckpoint)
            {
                await EnqueueAsync(new SdkOperationUpdate
                {
                    Id = OperationId,
                    ParentId = ParentId,
                    Type = OperationTypes.Context,
                    Action = OperationAction.FAIL,
                    SubType = _config?.SubType,
                    Name = Name,
                    Error = ToSdkError(ex)
                }, cancellationToken);
            }

            throw MapFailureException(new ChildContextException(ex.Message, ex)
            {
                SubType = _config?.SubType,
                ErrorType = ex.GetType().FullName,
                OriginalStackTrace = ex.StackTrace?.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).ToList()
            });
        }

        // Virtual branches suppress the SUCCEED checkpoint; the orchestrator
        // serializes the result inline on the parent payload instead.
        // _suppressTerminalCheckpoint is set on overflow replay re-execution: the
        // child is already terminal in the store, so we re-run only to recover the
        // in-memory value and must NOT re-emit a SUCCEED.
        if (!_isVirtual && !_suppressTerminalCheckpoint)
        {
            var serialized = SerializeResult(result);

            // Capture the child's own SUCCEED-checkpoint payload so a Nested Map/Parallel
            // parent can inline it verbatim (serialized here with THIS child's OperationId
            // as the entity id). Captured before the overflow decision so the parent sees
            // the same S(body) it would otherwise re-derive: on overflow the parent inlines
            // this (large) payload, overflows in turn, and recovers via ReplayChildren.
            SerializedResultPayload = serialized;

            // Overflow: result too large to checkpoint inline. Emit an empty
            // payload + ReplayChildren so replay re-executes this body to recover
            // the value (mirrors the concurrent-operation overflow strategy).
            var overflow = Encoding.UTF8.GetByteCount(serialized) > DurableConstants.MaxOperationCheckpointBytes;

            // Non-overflow: round-trip the just-written checkpoint payload so the
            // value the workflow observes on this fresh execution matches replay
            // (where the result is always deserialized from the checkpoint). This
            // makes a custom (possibly non-round-tripping) ChildContextConfig.Serializer's
            // transform visible in the child-context result on the first run.
            // Behavior change: the returned object is no longer the same instance
            // the child body produced. See the AutoVer change note. Overflow skips
            // this (the payload was stripped; the value is recovered by replay).
            //
            // Deserialize BEFORE emitting the SUCCEED: a serializer whose deserialize
            // fails is then surfaced as a normal child failure (wrapped in
            // ChildContextException, with no SUCCEED recorded) instead of throwing
            // against an already-SUCCEEDED operation — which would diverge from
            // replay and, for a Map/Parallel Nested unit, record the unit Failed
            // against its own SUCCEEDED checkpoint.
            T roundTripped = result;
            if (!overflow)
            {
                try
                {
                    roundTripped = DeserializeResult(serialized);
                }
                catch (Exception ex)
                {
                    // The body SUCCEEDED (side effects ran) but its result could not be
                    // deserialized back. This is TERMINAL — emit a CONTEXT FAIL so the op
                    // is terminal in the store and its side-effecting body is NOT re-run on
                    // replay. Previously this threw with NO terminal checkpoint, leaving a
                    // top-level child STARTED (its body re-runs on replay) and a Nested unit
                    // recorded Failed against its own — now correctly absent — SUCCEED. This
                    // mirrors the body-failure catch above and StepOperation.FailStepTerminallyAsync.
                    // We are inside the non-virtual, non-suppressed block, so FAIL is the correct
                    // terminal record. If this FAIL enqueue itself throws, it propagates straight
                    // out (no retry loop, no recursion) — a broken batcher surfaces its own error.
                    await EnqueueAsync(new SdkOperationUpdate
                    {
                        Id = OperationId,
                        ParentId = ParentId,
                        Type = OperationTypes.Context,
                        Action = OperationAction.FAIL,
                        SubType = _config?.SubType,
                        Name = Name,
                        Error = ToSdkError(ex)
                    }, cancellationToken);

                    throw MapFailureException(new ChildContextException(ex.Message, ex)
                    {
                        SubType = _config?.SubType,
                        ErrorType = ex.GetType().FullName,
                        OriginalStackTrace = ex.StackTrace?.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).ToList()
                    });
                }
            }

            await EnqueueAsync(new SdkOperationUpdate
            {
                Id = OperationId,
                ParentId = ParentId,
                Type = OperationTypes.Context,
                Action = OperationAction.SUCCEED,
                SubType = _config?.SubType,
                Name = Name,
                Payload = overflow ? string.Empty : serialized,
                ContextOptions = overflow
                    ? new SdkContextOptions { ReplayChildren = true }
                    : null
            }, cancellationToken);

            if (!overflow)
            {
                return roundTripped;
            }
        }

        return result;
    }

    private Exception MapFailureException(ChildContextException ex)
    {
        var mapper = _config?.ErrorMapping;
        if (mapper == null) return ex;

        var mapped = mapper(ex);
        return mapped ?? ex;
    }

    private ChildContextException BuildChildContextException(Operation failedOp)
    {
        var err = failedOp.ContextDetails?.Error;
        return new ChildContextException(err?.ErrorMessage ?? "Child context failed")
        {
            SubType = failedOp.SubType ?? _config?.SubType,
            ErrorType = err?.ErrorType,
            ErrorData = err?.ErrorData,
            OriginalStackTrace = err?.StackTrace
        };
    }

    private T DeserializeResult(string? serialized)
    {
        if (serialized == null) return default!;
        var bytes = Encoding.UTF8.GetBytes(serialized);
        using var ms = new MemoryStream(bytes);
        return LambdaSerializerHelper.Deserialize<T>(
            _serializer, ms, new DurableSerializationContext(OperationId, DurableExecutionArn));
    }

    private string SerializeResult(T value)
    {
        using var ms = new MemoryStream();
        LambdaSerializerHelper.Serialize(
            _serializer, value, ms, new DurableSerializationContext(OperationId, DurableExecutionArn));
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private static SdkErrorObject ToSdkError(Exception ex) => new()
    {
        ErrorType = ex.GetType().FullName,
        ErrorMessage = ex.Message,
        StackTrace = ex.StackTrace?.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).ToList()
    };
}
