// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.IO;
using System.Text;
using Amazon.Lambda;
using Amazon.Lambda.Core;
using SdkErrorObject = Amazon.Lambda.Model.ErrorObject;
using SdkOperationUpdate = Amazon.Lambda.Model.OperationUpdate;

namespace Amazon.Lambda.DurableExecution.Internal;

/// <summary>
/// Durable parallel operation. Runs N user-supplied branches concurrently
/// (each as a <see cref="ChildContextOperation{T}"/>) under a shared
/// <see cref="CompletionConfig"/> and concurrency limit, persisting the
/// aggregate result so subsequent invocations replay it without re-executing.
/// </summary>
/// <remarks>
/// Replay branches — example: <c>await ctx.ParallelAsync(funcs, name: "fetch")</c>
/// <list type="bullet">
///   <item><b>Fresh</b>: no prior state → sync-flush parent CONTEXT START →
///       dispatch branches respecting MaxConcurrency → wait for in-flight to
///       complete after CompletionConfig short-circuit → emit parent CONTEXT
///       SUCCEED with summary payload (<see cref="ParallelSummary"/>).</item>
///   <item><b>SUCCEEDED</b>: parent payload supplies the snapshot of per-
///       branch statuses + completion reason; per-branch results are
///       deserialised from the children's own CONTEXT checkpoints.</item>
///   <item><b>FAILED</b>: same reconstruction; throws
///       <see cref="ParallelException"/> carrying the rebuilt
///       <see cref="IBatchResult{T}"/>.</item>
///   <item><b>STARTED</b> / <b>PENDING</b>: re-execute (children replay from
///       their own checkpoints).</item>
/// </list>
/// Per-branch errors do NOT abort the parallel directly — the orchestrator
/// catches each branch's <see cref="ChildContextException"/>, records it as a
/// failed <see cref="IBatchItem{T}"/>, and consults the
/// <see cref="CompletionConfig"/> after every completion. Only when the
/// completion config marks the run as
/// <see cref="CompletionReason.FailureToleranceExceeded"/> does the parallel
/// throw.
/// </remarks>
internal sealed class ParallelOperation<T> : DurableOperation<IBatchResult<T>>
{
    private readonly IReadOnlyList<DurableBranch<T>> _branches;
    private readonly ParallelConfig _config;
    private readonly CompletionPolicy _policy;
    private readonly ILambdaSerializer _serializer;
    private readonly Func<string, IDurableContext> _childContextFactory;
    private readonly WorkflowCancellation _workflowCancellation;

    public ParallelOperation(
        string operationId,
        string? name,
        string? parentId,
        IReadOnlyList<DurableBranch<T>> branches,
        ParallelConfig config,
        ILambdaSerializer serializer,
        Func<string, IDurableContext> childContextFactory,
        ExecutionState state,
        TerminationManager termination,
        WorkflowCancellation workflowCancellation,
        string durableExecutionArn,
        CheckpointBatcher? batcher = null)
        : base(operationId, name, parentId, state, termination, durableExecutionArn, batcher)
    {
        _branches = branches;
        _config = config;
        _policy = new CompletionPolicy(config.CompletionConfig);
        _serializer = serializer;
        _childContextFactory = childContextFactory;
        _workflowCancellation = workflowCancellation;
    }

    protected override string OperationType => OperationTypes.Context;

    protected override async Task<IBatchResult<T>> StartAsync(CancellationToken cancellationToken)
    {
        // Sync-flush parent CONTEXT START. Mirrors ChildContextOperation: if a
        // branch suspends (e.g., a Wait inside a branch), the service needs to
        // know the parallel parent existed.
        await EnqueueAsync(new SdkOperationUpdate
        {
            Id = OperationId,
            Type = OperationTypes.Context,
            Action = OperationAction.START,
            SubType = OperationSubTypes.Parallel,
            Name = Name
        }, cancellationToken);

        return await ExecuteBranchesAsync(cancellationToken);
    }

    protected override Task<IBatchResult<T>> ReplayAsync(Operation existing, CancellationToken cancellationToken)
    {
        switch (existing.Status)
        {
            case OperationStatuses.Succeeded:
                return Task.FromResult(ReconstructFromCheckpoints(existing, throwOnFailure: false));

            case OperationStatuses.Failed:
                // Reconstruct so the caller (and ParallelException.Result) sees
                // the per-branch outcomes; then throw.
                var failed = ReconstructFromCheckpoints(existing, throwOnFailure: false);
                throw BuildParallelException(failed);

            case OperationStatuses.Started:
            case OperationStatuses.Pending:
                // Re-run: branches replay from their own checkpoints.
                return ExecuteBranchesAsync(cancellationToken);

            default:
                throw new NonDeterministicExecutionException(
                    $"Parallel operation '{Name ?? OperationId}' has unexpected status '{existing.Status}' on replay.");
        }
    }

    private async Task<IBatchResult<T>> ExecuteBranchesAsync(CancellationToken cancellationToken)
    {
        // Combine the caller's token with the workflow-shutdown token for the
        // parallel's OWN control flow: the dispatch loop's semaphore waits, the
        // post-settle re-throw, and each branch's OCE classification.
        //
        // CRITICAL: childOp.ExecuteAsync below still receives the *caller* token
        // only. ChildContextOperation re-links workflow-shutdown itself for the
        // user Func, and its checkpoint writes (CONTEXT FAIL/SUCCEED) must NOT
        // observe shutdown, otherwise teardown could abort a branch's successful
        // checkpoint mid-flush.
        using var controlCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, _workflowCancellation.Token);
        var controlToken = controlCts.Token;

        controlToken.ThrowIfCancellationRequested();

        var branchCount = _branches.Count;
        var slots = new BranchOutcome[branchCount];
        var dispatched = new bool[branchCount];

        var maxConcurrency = _config.MaxConcurrency ?? branchCount;
        // Optimisation: when MaxConcurrency >= branchCount, skip the semaphore
        // entirely. Behaviour is identical, allocations are lower.
        var semaphore = (maxConcurrency >= branchCount) ? null : new SemaphoreSlim(maxConcurrency, maxConcurrency);

        var succeeded = 0;
        var failed = 0;

        var inFlight = new List<Task>(branchCount);

        // Reads the live counters and asks the completion policy whether the run
        // is already decided. Volatile reads pair with the Interlocked.Increment
        // writes in the onComplete callback. Reads are non-atomic across the two
        // counters: at worst we observe slightly stale values and dispatch one
        // extra branch before the next completion forces a re-check. That's
        // acceptable — the post-loop _policy.Evaluate is the source of truth.
        bool ShouldStopDispatchingNow() => _policy.ShouldStopDispatching(
            Volatile.Read(ref succeeded), Volatile.Read(ref failed), branchCount);

        // Branches run with the caller's token (re-linked to workflow-shutdown
        // inside ChildContextOperation) so cooperative cancellation still
        // propagates into user code, but we must NOT abandon already-dispatched
        // branches while they're still writing checkpoints — that would diverge
        // between the original run and replay. The finally block therefore awaits
        // every in-flight task even when cancellation fires, and only then
        // disposes the semaphore (after branches have settled — success, failure,
        // or cooperative OCE).
        try
        {
            for (var i = 0; i < branchCount; i++)
            {
                if (ShouldStopDispatchingNow())
                    break;

                if (semaphore != null)
                {
                    await semaphore.WaitAsync(controlToken).ConfigureAwait(false);
                    // Re-check after acquiring: the wait may have unblocked
                    // because earlier branches finished and short-circuited
                    // the operation.
                    if (ShouldStopDispatchingNow())
                    {
                        semaphore.Release();
                        break;
                    }
                }

                var index = i;
                dispatched[index] = true;
                inFlight.Add(RunBranchAsync(index, slots, semaphore, cancellationToken, controlToken,
                    onComplete: outcome =>
                    {
                        if (outcome.Status == BatchItemStatus.Succeeded)
                            Interlocked.Increment(ref succeeded);
                        else if (outcome.Status == BatchItemStatus.Failed)
                            Interlocked.Increment(ref failed);
                    }));
            }
        }
        finally
        {
            // CRITICAL: wait for every dispatched branch — even on the
            // exceptional path (control-token cancellation mid-dispatch, or a
            // synchronous throw out of the loop) — before the semaphore is
            // disposed. Otherwise surviving branches' Release() calls hit
            // ObjectDisposedException, the tasks become unobserved, and they
            // keep writing checkpoints out from under us.
            //
            // We deliberately DO NOT cancel already-running branches when a
            // short-circuit fires — orphan branches that continue writing
            // checkpoints would diverge between the original run and replay.
            // Letting them finish guarantees determinism: all dispatched
            // branches end up Succeeded or Failed. Only un-dispatched branches
            // surface as Started.
            if (inFlight.Count > 0)
            {
                try
                {
                    await Task.WhenAll(inFlight).ConfigureAwait(false);
                }
                catch
                {
                    // Swallow here — Task.WhenAll only surfaces the first
                    // exception, but every branch task is now in a terminal
                    // state and we want to inspect each one individually below
                    // to decide whether to surface a workflow-level error. The
                    // Task objects themselves still carry their exceptions, so
                    // this swallow does not orphan them.
                }
            }

            semaphore?.Dispose();
        }

        // Surface any workflow-level exception (e.g. NonDeterministicExecutionException)
        // raised inside a branch. RunBranchAsync re-throws DurableExecutionException
        // (other than ChildContextException which is captured into the slot) so the
        // task faults with that exception. Take the first such failure: these are
        // structural errors, not "branch failed gracefully" outcomes.
        foreach (var t in inFlight)
        {
            if (t.IsFaulted && t.Exception is { } agg)
            {
                foreach (var inner in agg.InnerExceptions)
                {
                    if (inner is DurableExecutionException dex && inner is not ChildContextException)
                    {
                        throw dex;
                    }
                }
            }
        }

        // Re-throw any pending cancellation (caller-cancel or workflow shutdown)
        // now that branches have settled and the semaphore has been disposed
        // cleanly. Surfacing it here means a torn-down parallel propagates an
        // OperationCanceledException instead of synthesizing a spurious
        // FailureToleranceExceeded verdict from branches that merely unwound.
        controlToken.ThrowIfCancellationRequested();

        // Build BatchItems for every branch in original order.
        var items = new List<IBatchItem<T>>(branchCount);
        for (var i = 0; i < branchCount; i++)
        {
            if (dispatched[i])
            {
                var outcome = slots[i];
                items.Add(new BatchItem<T>
                {
                    Index = i,
                    Name = _branches[i].Name,
                    Status = outcome.Status,
                    Result = outcome.Status == BatchItemStatus.Succeeded ? outcome.Result : default,
                    Error = outcome.Status == BatchItemStatus.Failed ? outcome.Error : null
                });
            }
            else
            {
                items.Add(new BatchItem<T>
                {
                    Index = i,
                    Name = _branches[i].Name,
                    Status = BatchItemStatus.Started,
                    Result = default,
                    Error = null
                });
            }
        }

        var completionReason = EvaluateCompletion(items, branchCount);
        var result = new BatchResult<T>(items, completionReason);

        await CheckpointParentResultAsync(result, completionReason, cancellationToken);

        if (completionReason == CompletionReason.FailureToleranceExceeded)
        {
            throw BuildParallelException(result);
        }

        return result;
    }

    private async Task RunBranchAsync(
        int index,
        BranchOutcome[] slots,
        SemaphoreSlim? semaphore,
        CancellationToken cancellationToken,
        CancellationToken controlToken,
        Action<BranchOutcome> onComplete)
    {
        try
        {
            var branch = _branches[index];
            var branchOpId = OperationIdGenerator.HashOperationId($"{OperationId}-{index + 1}");

            var childOp = new ChildContextOperation<T>(
                branchOpId,
                branch.Name,
                OperationId,
                branch.Func,
                new ChildContextConfig { SubType = OperationSubTypes.ParallelBranch },
                _serializer,
                _childContextFactory,
                State,
                Termination,
                _workflowCancellation,
                DurableExecutionArn,
                Batcher);

            try
            {
                var result = await childOp.ExecuteAsync(cancellationToken).ConfigureAwait(false);
                slots[index] = new BranchOutcome { Status = BatchItemStatus.Succeeded, Result = result };
            }
            catch (ChildContextException ex)
            {
                slots[index] = new BranchOutcome { Status = BatchItemStatus.Failed, Error = ex };
            }
            catch (DurableExecutionException)
            {
                // E.g. NonDeterministicExecutionException — these are not
                // "branch failed gracefully" but workflow-level problems.
                // Surface them: re-throw out of the parallel without writing
                // a slot (the orchestrator's outer flow handles it).
                throw;
            }
            catch (OperationCanceledException) when (controlToken.IsCancellationRequested)
            {
                // Control-token cancellation — caller-cancel OR workflow
                // shutdown (a sibling op suspended, a checkpoint failed)
                throw;
            }
            catch (OperationCanceledException ex)
            {
                // Branch-internal cancellation that is NOT tied to the control
                // token (e.g. the branch's own CancellationTokenSource fired).
                // Treat it as a normal per-branch failure rather than killing
                // the parallel as cancelled.
                var wrapped = new ChildContextException(ex.Message, ex)
                {
                    SubType = OperationSubTypes.ParallelBranch,
                    ErrorType = ex.GetType().FullName
                };
                slots[index] = new BranchOutcome { Status = BatchItemStatus.Failed, Error = wrapped };
            }
            catch (Exception ex)
            {
                // Wrap unexpected exceptions as ChildContextException — they're
                // per-branch failures from the user's POV.
                var wrapped = new ChildContextException(ex.Message, ex)
                {
                    SubType = OperationSubTypes.ParallelBranch,
                    ErrorType = ex.GetType().FullName
                };
                slots[index] = new BranchOutcome { Status = BatchItemStatus.Failed, Error = wrapped };
            }

            onComplete(slots[index]);
        }
        finally
        {
            // Defensive: with the new structure the semaphore is only disposed
            // after Task.WhenAll(inFlight) has settled, so this Release should
            // always succeed. ObjectDisposedException would indicate a bug
            // elsewhere, but we tolerate it here so the task doesn't fault
            // with a noise exception that masks the real one.
            try
            {
                semaphore?.Release();
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }

    private CompletionReason EvaluateCompletion(IReadOnlyList<IBatchItem<T>> items, int totalCount)
    {
        var succeeded = 0;
        var failed = 0;
        var started = 0;

        foreach (var item in items)
        {
            switch (item.Status)
            {
                case BatchItemStatus.Succeeded: succeeded++; break;
                case BatchItemStatus.Failed:    failed++;    break;
                case BatchItemStatus.Started:   started++;   break;
            }
        }

        return _policy.Evaluate(succeeded, failed, started, totalCount);
    }

    private async Task CheckpointParentResultAsync(
        BatchResult<T> result,
        CompletionReason completionReason,
        CancellationToken cancellationToken)
    {
        var summary = ParallelSummaryCodec.Build(result.All, completionReason);
        var payload = ParallelSummaryCodec.ToPayload(summary);
        var failed = completionReason == CompletionReason.FailureToleranceExceeded;

        await EnqueueAsync(new SdkOperationUpdate
        {
            Id = OperationId,
            Type = OperationTypes.Context,
            Action = failed ? OperationAction.FAIL : OperationAction.SUCCEED,
            SubType = OperationSubTypes.Parallel,
            Name = Name,
            Payload = failed ? null : payload,
            Error = failed ? BuildAggregateError(result) : null
        }, cancellationToken);
    }

    private IBatchResult<T> ReconstructFromCheckpoints(Operation parent, bool throwOnFailure)
    {
        var summary = ParallelSummaryCodec.FromPayload(parent.ContextDetails?.Result);

        var items = new List<IBatchItem<T>>(_branches.Count);
        for (var i = 0; i < _branches.Count; i++)
        {
            var branchOpId = OperationIdGenerator.HashOperationId($"{OperationId}-{i + 1}");
            var branchOp = State.GetOperation(branchOpId);
            var summaryEntry = summary?.Branches.FirstOrDefault(b => b.Index == i);

            BatchItemStatus status = summaryEntry != null
                ? ParallelSummaryCodec.ReadStatus(summaryEntry.Status)
                : InferStatusFromBranchOp(branchOp);

            // Prefer the name that was checkpointed at the moment the batch
            // resolved. This is the only authoritative source for branches
            // reported as Started (no per-branch checkpoint exists to consult),
            // and it lets us detect branch-name drift between deployments.
            var currentName = _branches[i].Name;
            var checkpointedName = summaryEntry?.Name;
            if (checkpointedName != null && currentName != null && checkpointedName != currentName)
            {
                throw new NonDeterministicExecutionException(
                    $"Non-deterministic execution detected for parallel branch {i} of operation " +
                    $"'{Name ?? OperationId}': expected name '{currentName}' but found '{checkpointedName}' " +
                    $"from a previous invocation. Code must not change the order or name of parallel " +
                    $"branches between deployments.");
            }
            var itemName = checkpointedName ?? currentName;

            T? branchResult = default;
            DurableExecutionException? branchError = null;

            if (status == BatchItemStatus.Succeeded && branchOp?.ContextDetails?.Result != null)
            {
                branchResult = DeserializeBranchResult(branchOp.ContextDetails.Result);
            }
            else if (status == BatchItemStatus.Failed && branchOp?.ContextDetails?.Error != null)
            {
                var err = branchOp.ContextDetails.Error;
                branchError = new ChildContextException(err.ErrorMessage ?? "Branch failed")
                {
                    SubType = branchOp.SubType ?? OperationSubTypes.ParallelBranch,
                    ErrorType = err.ErrorType,
                    ErrorData = err.ErrorData,
                    OriginalStackTrace = err.StackTrace
                };
            }

            items.Add(new BatchItem<T>
            {
                Index = i,
                Name = itemName,
                Status = status,
                Result = branchResult,
                Error = branchError
            });
        }

        var completionReason = summary != null
            ? ParallelSummaryCodec.ReadCompletionReason(summary.CompletionReason)
            : EvaluateCompletion(items, _branches.Count);

        var result = new BatchResult<T>(items, completionReason);

        if (throwOnFailure && completionReason == CompletionReason.FailureToleranceExceeded)
        {
            throw BuildParallelException(result);
        }

        return result;
    }

    private static BatchItemStatus InferStatusFromBranchOp(Operation? branchOp)
    {
        if (branchOp == null) return BatchItemStatus.Started;
        return branchOp.Status switch
        {
            OperationStatuses.Succeeded => BatchItemStatus.Succeeded,
            OperationStatuses.Failed    => BatchItemStatus.Failed,
            _                           => BatchItemStatus.Started
        };
    }

    private static ParallelException BuildParallelException(IBatchResult<T> result)
    {
        return new ParallelException(
            $"Parallel operation failed: failure tolerance exceeded ({result.FailureCount} of {result.TotalCount} branches failed).")
        {
            Result = result,
            CompletionReason = result.CompletionReason
        };
    }

    private static SdkErrorObject BuildAggregateError(IBatchResult<T> result)
    {
        return new SdkErrorObject
        {
            ErrorType = typeof(ParallelException).FullName,
            ErrorMessage = $"Parallel operation failed: {result.FailureCount} of {result.TotalCount} branches failed."
        };
    }

    private T DeserializeBranchResult(string serialized)
    {
        var bytes = Encoding.UTF8.GetBytes(serialized);
        using var ms = new MemoryStream(bytes);
        return _serializer.Deserialize<T>(ms);
    }

    /// <summary>
    /// Internal scratch space tracking each branch's outcome as it lands in
    /// the executor; copied into the user-facing <see cref="BatchItem{T}"/>
    /// once every dispatched branch has settled.
    /// </summary>
    private struct BranchOutcome
    {
        public BatchItemStatus Status;
        public T? Result;
        public DurableExecutionException? Error;
    }
}
