// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.IO;
using System.Text;
using System.Text.Json;
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
        cancellationToken.ThrowIfCancellationRequested();

        var branchCount = _branches.Count;
        var slots = new BranchOutcome[branchCount];
        var dispatched = new bool[branchCount];

        var maxConcurrency = _config.MaxConcurrency ?? branchCount;
        // Optimisation: when MaxConcurrency >= branchCount, skip the semaphore
        // entirely. Behaviour is identical, allocations are lower.
        var semaphore = (maxConcurrency >= branchCount) ? null : new SemaphoreSlim(maxConcurrency, maxConcurrency);

        var minSuccessful = _config.CompletionConfig.MinSuccessful;
        var toleratedFailureCount = _config.CompletionConfig.ToleratedFailureCount;
        var toleratedFailurePercentage = _config.CompletionConfig.ToleratedFailurePercentage;

        var succeeded = 0;
        var failed = 0;

        var inFlight = new List<Task>(branchCount);

        // Branches run with the parent's token so cooperative cancellation
        // still propagates into user code, but we must NOT abandon already-
        // dispatched branches while they're still writing checkpoints — that
        // would diverge between the original run and replay. The dispatch
        // loop and Task.WhenAll below therefore await every in-flight task
        // even when cancellation fires; the semaphore is disposed only after
        // those branches have settled (success, failure, or cooperative OCE).
        try
        {
            try
            {
                for (var i = 0; i < branchCount; i++)
                {
                    // Volatile reads pair with the Interlocked.Increment writes
                    // in the onComplete callback. Reads are non-atomic across
                    // the two counters: at worst we observe slightly stale
                    // values and dispatch one extra branch before the next
                    // completion forces a re-check. That's acceptable — the
                    // post-loop ComputeCompletionReason is the source of truth.
                    var succSnap = Volatile.Read(ref succeeded);
                    var failSnap = Volatile.Read(ref failed);
                    if (ShouldStopDispatching(succSnap, failSnap, branchCount,
                            minSuccessful, toleratedFailureCount, toleratedFailurePercentage))
                    {
                        break;
                    }

                    if (semaphore != null)
                    {
                        await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                        // Re-check after acquiring: the wait may have unblocked
                        // because earlier branches finished and short-circuited
                        // the operation.
                        succSnap = Volatile.Read(ref succeeded);
                        failSnap = Volatile.Read(ref failed);
                        if (ShouldStopDispatching(succSnap, failSnap, branchCount,
                                minSuccessful, toleratedFailureCount, toleratedFailurePercentage))
                        {
                            semaphore.Release();
                            break;
                        }
                    }

                    var index = i;
                    dispatched[index] = true;
                    inFlight.Add(RunBranchAsync(index, slots, semaphore, cancellationToken,
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
                // exceptional path (parent-token cancellation mid-dispatch, or
                // a synchronous throw out of the loop) — before the semaphore
                // is disposed. Otherwise surviving branches' Release() calls
                // hit ObjectDisposedException, the tasks become unobserved,
                // and they keep writing checkpoints out from under us.
                //
                // We deliberately DO NOT cancel already-running branches when
                // a short-circuit fires — orphan branches that continue
                // writing checkpoints would diverge between the original run
                // and replay. Letting them finish guarantees determinism: all
                // dispatched branches end up Succeeded or Failed. Only
                // un-dispatched branches surface as Started.
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
                        // state and we want to inspect each one individually
                        // below to decide whether to surface a workflow-level
                        // error. The Task objects themselves still carry their
                        // exceptions, so this swallow does not orphan them.
                    }
                }
            }
        }
        finally
        {
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

        // Re-throw any pending parent-token cancellation now that branches
        // have settled and the semaphore has been disposed cleanly.
        cancellationToken.ThrowIfCancellationRequested();

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

        var completionReason = ComputeCompletionReason(items, branchCount);
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
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Parent-token cancellation: per cross-cutting decision Q10,
                // OCE escapes unwrapped. Don't write a slot — Task.WhenAll
                // observes this and the orchestrator re-throws after settling.
                throw;
            }
            catch (OperationCanceledException ex)
            {
                // Branch-internal cancellation that is NOT tied to the parent
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

    private static bool ShouldStopDispatching(
        int succeeded,
        int failed,
        int totalBranches,
        int? minSuccessful,
        int? toleratedFailureCount,
        double? toleratedFailurePercentage)
    {
        // Min-successful: short-circuit the moment we have enough wins.
        if (minSuccessful is { } min && succeeded >= min)
            return true;

        // Failure thresholds short-circuit on too many losses.
        if (toleratedFailureCount is { } tfc && failed > tfc)
            return true;

        if (toleratedFailurePercentage is { } tfp && totalBranches > 0)
        {
            var ratio = (double)failed / totalBranches;
            if (ratio > tfp) return true;
        }

        return false;
    }

    private CompletionReason ComputeCompletionReason(IReadOnlyList<IBatchItem<T>> items, int totalCount)
    {
        var failed = 0;
        var succeeded = 0;
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

        // Failure tolerance: only short-circuit-by-failure when at least one
        // failure threshold is explicitly set. The factory CompletionConfig.AllSuccessful()
        // sets ToleratedFailureCount = 0 to opt into fail-fast; an "empty"
        // CompletionConfig (all properties null) is permissive.
        if (_config.CompletionConfig.ToleratedFailureCount is { } tfc && failed > tfc)
            return CompletionReason.FailureToleranceExceeded;

        if (_config.CompletionConfig.ToleratedFailurePercentage is { } tfp && totalCount > 0)
        {
            var ratio = (double)failed / totalCount;
            if (ratio > tfp) return CompletionReason.FailureToleranceExceeded;
        }

        // Min-successful satisfied (and we didn't run all branches): MinSuccessfulReached.
        if (_config.CompletionConfig.MinSuccessful is { } min && succeeded >= min && started > 0)
        {
            return CompletionReason.MinSuccessfulReached;
        }

        // Every dispatched branch finished one way or the other (or all-completed
        // without any failure criteria).
        return CompletionReason.AllCompleted;
    }

    private async Task CheckpointParentResultAsync(
        BatchResult<T> result,
        CompletionReason completionReason,
        CancellationToken cancellationToken)
    {
        var summary = new ParallelSummary
        {
            CompletionReason = SerializeCompletionReason(completionReason),
            Branches = new List<ParallelBranchSummary>(result.All.Count)
        };
        for (var i = 0; i < result.All.Count; i++)
        {
            var item = result.All[i];
            summary.Branches.Add(new ParallelBranchSummary
            {
                Index = item.Index,
                Name = item.Name,
                Status = SerializeStatus(item.Status)
            });
        }

        var payload = JsonSerializer.Serialize(summary, ParallelJsonContext.Default.ParallelSummary);
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
        var summary = ParseSummary(parent.ContextDetails?.Result);

        var items = new List<IBatchItem<T>>(_branches.Count);
        for (var i = 0; i < _branches.Count; i++)
        {
            var branchOpId = OperationIdGenerator.HashOperationId($"{OperationId}-{i + 1}");
            var branchOp = State.GetOperation(branchOpId);
            var summaryEntry = summary?.Branches.FirstOrDefault(b => b.Index == i);

            BatchItemStatus status = summaryEntry != null
                ? DeserializeStatus(summaryEntry.Status)
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
            ? DeserializeCompletionReason(summary.CompletionReason)
            : ComputeCompletionReason(items, _branches.Count);

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

    private static ParallelSummary? ParseSummary(string? payload)
    {
        if (string.IsNullOrEmpty(payload)) return null;
        try
        {
            return JsonSerializer.Deserialize(payload, ParallelJsonContext.Default.ParallelSummary);
        }
        catch (JsonException)
        {
            // Tolerate older / corrupted payloads — fall back to inferring status
            // from per-branch checkpoints.
            return null;
        }
    }

    private static string SerializeStatus(BatchItemStatus status) => status switch
    {
        BatchItemStatus.Succeeded => "SUCCEEDED",
        BatchItemStatus.Failed    => "FAILED",
        BatchItemStatus.Started   => "STARTED",
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };

    private static BatchItemStatus DeserializeStatus(string? wire) => wire switch
    {
        "SUCCEEDED" => BatchItemStatus.Succeeded,
        "FAILED"    => BatchItemStatus.Failed,
        "STARTED"   => BatchItemStatus.Started,
        _           => BatchItemStatus.Started
    };

    private static string SerializeCompletionReason(CompletionReason reason) => reason switch
    {
        CompletionReason.AllCompleted             => "ALL_COMPLETED",
        CompletionReason.MinSuccessfulReached     => "MIN_SUCCESSFUL_REACHED",
        CompletionReason.FailureToleranceExceeded => "FAILURE_TOLERANCE_EXCEEDED",
        _ => throw new ArgumentOutOfRangeException(nameof(reason))
    };

    private static CompletionReason DeserializeCompletionReason(string? wire) => wire switch
    {
        "ALL_COMPLETED"              => CompletionReason.AllCompleted,
        "MIN_SUCCESSFUL_REACHED"     => CompletionReason.MinSuccessfulReached,
        "FAILURE_TOLERANCE_EXCEEDED" => CompletionReason.FailureToleranceExceeded,
        _                            => CompletionReason.AllCompleted
    };

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
