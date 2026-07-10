// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.IO;
using System.Text;
using System.Text.Json;
using Amazon.Lambda;
using Amazon.Lambda.Core;
using SdkContextOptions = Amazon.Lambda.Model.ContextOptions;
using SdkOperationUpdate = Amazon.Lambda.Model.OperationUpdate;

namespace Amazon.Lambda.DurableExecution.Internal;

/// <summary>
/// Shared orchestration base for the concurrent durable operations
/// (<see cref="ParallelOperation{T}"/> and <see cref="MapOperation{TItem, TResult}"/>).
/// Runs N user-supplied units concurrently (each as a
/// <see cref="ChildContextOperation{T}"/>) under a shared
/// <see cref="CompletionConfig"/> and concurrency limit, persisting the
/// aggregate result so subsequent invocations replay it without re-executing.
/// </summary>
/// <remarks>
/// Subclasses supply only what differs between Parallel and Map — the unit count,
/// how to obtain a unit's <c>(name, func)</c>, the parent/child sub-type labels,
/// and the failure-exception factory. All concurrency, completion, checkpoint, and
/// replay logic lives here.
/// <list type="bullet">
///   <item><b>Fresh</b>: no prior state → sync-flush parent CONTEXT START →
///       dispatch units respecting MaxConcurrency → wait for in-flight to
///       complete after CompletionConfig short-circuit → emit parent CONTEXT
///       SUCCEED with summary payload (<see cref="BatchSummary"/>).</item>
///   <item><b>SUCCEEDED</b>: parent payload supplies the snapshot of per-unit
///       statuses + completion reason; per-unit results are deserialised from the
///       children's own CONTEXT checkpoints. The rebuilt
///       <see cref="IBatchResult{T}"/> is returned regardless of completion
///       reason.</item>
///   <item><b>STARTED</b> / <b>PENDING</b>: re-execute (children replay from their
///       own checkpoints).</item>
/// </list>
/// Per-unit errors do NOT abort the operation — the orchestrator catches each
/// unit's <see cref="ChildContextException"/>, records it as a failed
/// <see cref="IBatchItem{T}"/>, and consults the <see cref="CompletionConfig"/>
/// after every completion only to decide whether to stop dispatching. The
/// operation ALWAYS returns an <see cref="IBatchResult{T}"/> — it never throws on
/// failure, matching the JS/Python/Java SDKs. Callers inspect
/// <see cref="IBatchResult.CompletionReason"/> / <see cref="IBatchResult.HasFailure"/>
/// or call <see cref="IBatchResult{T}.ThrowIfError"/> to surface a failure.
/// </remarks>
internal abstract class ConcurrentOperation<T> : DurableOperation<IBatchResult<T>>
{
    private readonly CompletionPolicy _policy;
    private readonly int? _maxConcurrency;
    private readonly WorkflowCancellation _workflowCancellation;

    /// <summary>
    /// True for <see cref="NestingType.Flat"/>: per-unit child contexts emit no
    /// CONTEXT checkpoint, so their results/errors are recorded inline on this
    /// parent operation's <see cref="BatchSummary"/> payload and read back from
    /// there on replay.
    /// </summary>
    private readonly bool _isVirtual;

    /// <summary>Serializer used to deserialize per-unit child results on replay.</summary>
    protected readonly ILambdaSerializer Serializer;

    /// <summary>Factory used to build each unit's inner child context. Takes
    /// <c>(operationId, reportedParentId, isVirtual)</c>.</summary>
    protected readonly Func<string, string?, bool, IDurableContext> ChildContextFactory;

    protected ConcurrentOperation(
        string operationId,
        string? name,
        string? parentId,
        CompletionConfig completionConfig,
        int? maxConcurrency,
        ILambdaSerializer serializer,
        Func<string, string?, bool, IDurableContext> childContextFactory,
        ExecutionState state,
        TerminationManager termination,
        WorkflowCancellation workflowCancellation,
        string durableExecutionArn,
        CheckpointBatcher? batcher = null,
        bool isVirtual = false)
        : base(operationId, name, parentId, state, termination, durableExecutionArn, batcher)
    {
        _policy = new CompletionPolicy(completionConfig);
        _maxConcurrency = maxConcurrency;
        _workflowCancellation = workflowCancellation;
        Serializer = serializer;
        ChildContextFactory = childContextFactory;
        _isVirtual = isVirtual;
    }

    protected override string OperationType => OperationTypes.Context;

    // ── Subclass hooks ──────────────────────────────────────────────────

    /// <summary>The number of units (branches or items) to execute.</summary>
    protected abstract int UnitCount { get; }

    /// <summary>Parent CONTEXT sub-type label (e.g. Parallel / Map).</summary>
    protected abstract string ParentSubType { get; }

    /// <summary>Per-unit child-context sub-type label (e.g. ParallelBranch / MapItem).</summary>
    protected abstract string ChildSubType { get; }

    /// <summary>Singular operation noun used in messages (e.g. "Parallel" / "Map").</summary>
    protected abstract string OperationNoun { get; }

    /// <summary>
    /// Resolves the unit at <paramref name="index"/> into its display name and the
    /// function to run inside the unit's child context.
    /// </summary>
    protected abstract (string? Name, Func<IDurableContext, CancellationToken, Task<T>> Func) GetUnit(int index);

    // ── Orchestration ───────────────────────────────────────────────────

    protected override async Task<IBatchResult<T>> StartAsync(CancellationToken cancellationToken)
    {
        // Sync-flush parent CONTEXT START. Mirrors ChildContextOperation: if a
        // unit suspends (e.g., a Wait inside it), the service needs to know the
        // parent existed.
        await EnqueueAsync(new SdkOperationUpdate
        {
            Id = OperationId,
            ParentId = ParentId,
            Type = OperationTypes.Context,
            Action = OperationAction.START,
            SubType = ParentSubType,
            Name = Name
        }, cancellationToken);

        return await ExecuteUnitsAsync(cancellationToken);
    }

    protected override Task<IBatchResult<T>> ReplayAsync(Operation existing, CancellationToken cancellationToken)
    {
        // Overflow replay: the parent was checkpointed with a stripped summary and
        // ReplayChildren=true because the inline results exceeded the checkpoint
        // limit. Re-execute ONLY the units the frozen summary marks SUCCEEDED or
        // FAILED to recover their stripped result VALUE / Error; units marked
        // STARTED (short-circuited, never dispatched) are skipped. Per-unit status
        // and completion reason stay authoritative from the frozen summary, and the
        // parent — already terminal — is NOT re-checkpointed.
        var replayChildren = existing.ContextDetails?.ReplayChildren == true
            && (existing.Status == OperationStatuses.Succeeded
                || existing.Status == OperationStatuses.Failed);

        switch (existing.Status)
        {
            case OperationStatuses.Succeeded when replayChildren:
            case OperationStatuses.Failed when replayChildren:
                return ReplayChildrenAsync(existing, cancellationToken);

            case OperationStatuses.Succeeded:
                // The parent always checkpoints as SUCCEED — even when
                // CompletionReason is FailureToleranceExceeded. Reconstruct and
                // return the BatchResult; the operation never throws on failure
                // (the caller inspects CompletionReason / calls ThrowIfError).
                return Task.FromResult(ReconstructFromCheckpoints(existing));

            case OperationStatuses.Started:
            case OperationStatuses.Pending:
                // Re-run: units replay from their own checkpoints.
                return ExecuteUnitsAsync(cancellationToken);

            default:
                throw new NonDeterministicExecutionException(
                    $"{OperationNoun} operation '{Name ?? OperationId}' has unexpected status '{existing.Status}' on replay.");
        }
    }

    private async Task<IBatchResult<T>> ExecuteUnitsAsync(CancellationToken cancellationToken)
    {
        // Combine the caller's token with the workflow-shutdown token for the
        // operation's OWN control flow: the dispatch loop's semaphore waits, the
        // post-settle re-throw, and each unit's OCE classification.
        //
        // CRITICAL: childOp.ExecuteAsync below still receives the *caller* token
        // only. ChildContextOperation re-links workflow-shutdown itself for the
        // user func, and its checkpoint writes (CONTEXT FAIL/SUCCEED) must NOT
        // observe shutdown, otherwise teardown could abort a unit's successful
        // checkpoint mid-flush.
        using var controlCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, _workflowCancellation.Token);
        var controlToken = controlCts.Token;

        controlToken.ThrowIfCancellationRequested();

        // Cooperative-bail signal: tripped the moment a CompletionConfig
        // short-circuit is decided. It flows into each unit's user func only
        // (via ChildContextOperation's cooperative-bail token), NOT into the
        // units' checkpoint writes — a unit that honors the token unwinds with an
        // OperationCanceledException we record as Started, while a unit mid-flush
        // of a successful checkpoint still completes. Units that ignore the token
        // simply run to their natural terminal state, exactly as before. We never
        // abandon a dispatched unit, so replay stays deterministic.
        using var shortCircuitCts = new CancellationTokenSource();

        var unitCount = UnitCount;
        var slots = new UnitOutcome[unitCount];
        var dispatched = new bool[unitCount];

        var maxConcurrency = _maxConcurrency ?? unitCount;
        // Optimisation: when MaxConcurrency >= unitCount, skip the semaphore
        // entirely. Behaviour is identical, allocations are lower. (Also covers
        // the empty-collection case, where unitCount == 0 and no unit runs.)
        var semaphore = (maxConcurrency >= unitCount || unitCount == 0)
            ? null
            : new SemaphoreSlim(maxConcurrency, maxConcurrency);

        var succeeded = 0;
        var failed = 0;

        var inFlight = new List<Task>(unitCount);

        // Reads the live counters and asks the completion policy whether the run
        // is already decided. Volatile reads pair with the Interlocked.Increment
        // writes in the onComplete callback. Reads are non-atomic across the two
        // counters: at worst we observe slightly stale values and dispatch one
        // extra unit before the next completion forces a re-check. That's
        // acceptable — the post-loop ComputeCompletionReason is the source of truth.
        bool ShouldStopDispatchingNow() => _policy.ShouldStopDispatching(
            Volatile.Read(ref succeeded), Volatile.Read(ref failed), unitCount);

        // Signal still-running units to bail. Idempotent: the first Cancel wins,
        // racing callbacks are harmless. Tolerate a late call after the CTS is
        // disposed at end-of-scope (a unit completing during teardown).
        void SignalShortCircuit()
        {
            try { shortCircuitCts.Cancel(); }
            catch (ObjectDisposedException) { }
        }

        // Units run with the caller's token (re-linked to workflow-shutdown inside
        // ChildContextOperation) so cooperative cancellation still propagates into
        // user code, but we must NOT abandon already-dispatched units while they're
        // still writing checkpoints — that would diverge between the original run
        // and replay. The finally block therefore awaits every in-flight task even
        // when cancellation fires, and only then disposes the semaphore (after units
        // have settled — success, failure, or cooperative OCE).
        try
        {
            for (var i = 0; i < unitCount; i++)
            {
                if (ShouldStopDispatchingNow())
                {
                    SignalShortCircuit();
                    break;
                }

                if (semaphore != null)
                {
                    await semaphore.WaitAsync(controlToken).ConfigureAwait(false);
                    // Re-check after acquiring: the wait may have unblocked because
                    // earlier units finished and short-circuited the operation.
                    if (ShouldStopDispatchingNow())
                    {
                        semaphore.Release();
                        SignalShortCircuit();
                        break;
                    }
                }

                var index = i;
                dispatched[index] = true;
                inFlight.Add(RunUnitAsync(index, slots, semaphore, cancellationToken, controlToken,
                    shortCircuitCts.Token,
                    onComplete: outcome =>
                    {
                        if (outcome.Status == BatchItemStatus.Succeeded)
                            Interlocked.Increment(ref succeeded);
                        else if (outcome.Status == BatchItemStatus.Failed)
                            Interlocked.Increment(ref failed);

                        // The deciding completion typically lands AFTER every unit
                        // has been dispatched, so the loop is no longer sitting at a
                        // break point. Re-check here and signal any still-running
                        // units to bail.
                        if (ShouldStopDispatchingNow())
                            SignalShortCircuit();
                    }));
            }
        }
        finally
        {
            // CRITICAL: wait for every dispatched unit — even on the exceptional
            // path (control-token cancellation mid-dispatch, or a synchronous throw
            // out of the loop) — before the semaphore is disposed. Otherwise
            // surviving units' Release() calls hit ObjectDisposedException, the
            // tasks become unobserved, and they keep writing checkpoints out from
            // under us.
            //
            // We deliberately DO NOT cancel already-running units when a
            // short-circuit fires — orphan units that continue writing checkpoints
            // would diverge between the original run and replay. Letting them finish
            // guarantees determinism: all dispatched units end up Succeeded or
            // Failed. Only un-dispatched units surface as Started.
            if (inFlight.Count > 0)
            {
                try
                {
                    await Task.WhenAll(inFlight).ConfigureAwait(false);
                }
                catch
                {
                    // Swallow here — Task.WhenAll only surfaces the first exception,
                    // but every unit task is now in a terminal state and we want to
                    // inspect each one individually below to decide whether to
                    // surface a workflow-level error. The Task objects themselves
                    // still carry their exceptions, so this swallow does not orphan
                    // them.
                }
            }

            semaphore?.Dispose();
        }

        // Surface any workflow-level exception (e.g. NonDeterministicExecutionException)
        // raised inside a unit. RunUnitAsync re-throws DurableExecutionException
        // (other than ChildContextException which is captured into the slot) so the
        // task faults with that exception. Take the first such failure: these are
        // structural errors, not "unit failed gracefully" outcomes.
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

        // Re-throw any pending cancellation (caller-cancel or workflow shutdown) now
        // that units have settled and the semaphore has been disposed cleanly.
        // Surfacing it here means a torn-down operation propagates an
        // OperationCanceledException instead of synthesizing a spurious
        // FailureToleranceExceeded verdict from units that merely unwound.
        controlToken.ThrowIfCancellationRequested();

        // Build BatchItems ONLY for units that were dispatched — matching the JS
        // SDK, whose sparse resultItems array never records a never-dispatched
        // branch. A dispatched-then-bailed unit (cooperative short-circuit) keeps
        // its Started slot and stays in All; a never-dispatched unit is omitted
        // entirely. This makes TotalCount = started(in-flight) + succeeded + failed
        // and excludes branches that a CompletionConfig short-circuit skipped.
        var items = new List<IBatchItem<T>>(unitCount);
        for (var i = 0; i < unitCount; i++)
        {
            if (!dispatched[i])
                continue;

            var (unitName, _) = GetUnit(i);
            var outcome = slots[i];
            items.Add(new BatchItem<T>
            {
                Index = i,
                Name = unitName,
                Status = outcome.Status,
                Result = outcome.Status == BatchItemStatus.Succeeded ? outcome.Result : default,
                Error = outcome.Status == BatchItemStatus.Failed ? outcome.Error : null
            });
        }

        var completionReason = ComputeCompletionReason(items, unitCount);
        var result = new BatchResult<T>(items, completionReason);

        await CheckpointParentResultAsync(result, completionReason, cancellationToken);

        // Never throw on failure — always return the aggregate result. The caller
        // inspects CompletionReason / HasFailure or calls ThrowIfError. Matches
        // the JS/Python/Java SDKs.
        return result;
    }

    /// <summary>
    /// Overflow-replay path. The parent was checkpointed with a stripped summary
    /// (per-unit Index/Name/Status retained; Result/Error dropped) and
    /// <c>ReplayChildren=true</c>. Re-executes ONLY the units the frozen summary
    /// marks SUCCEEDED or FAILED — to recover their stripped result value / error
    /// — and skips units marked STARTED so their bodies do not re-run. Per-unit
    /// status and the completion reason come from the frozen summary (authoritative),
    /// not from this run's outcomes; the parent is NOT re-checkpointed.
    /// </summary>
    private async Task<IBatchResult<T>> ReplayChildrenAsync(Operation frozen, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var summary = ParseSummary(frozen.ContextDetails?.Result);
        var unitCount = UnitCount;

        var items = new List<IBatchItem<T>>(unitCount);
        for (var i = 0; i < unitCount; i++)
        {
            var (unitName, _) = GetUnit(i);
            var summaryEntry = summary?.Units.FirstOrDefault(b => b.Index == i);

            // Frozen per-unit status is authoritative.
            var status = summaryEntry != null
                ? DeserializeStatus(summaryEntry.Status)
                : BatchItemStatus.Started;

            // Same unit-name drift check as ReconstructFromCheckpoints: code must
            // not change the order or name of concurrent units between deployments.
            var checkpointedName = summaryEntry?.Name;
            if (checkpointedName != null && unitName != null && checkpointedName != unitName)
            {
                throw new NonDeterministicExecutionException(
                    $"Non-deterministic execution detected for {OperationNoun.ToLowerInvariant()} unit {i} of operation " +
                    $"'{Name ?? OperationId}': expected name '{unitName}' but found '{checkpointedName}' " +
                    $"from a previous invocation. Code must not change the order or name of concurrent " +
                    $"units between deployments.");
            }
            var resolvedName = checkpointedName ?? unitName;

            // STARTED units in the frozen summary were short-circuited (never
            // dispatched) originally — exclude them from the reconstructed All to
            // match the fresh-run view and the JS SDK. (The name-drift check above
            // still runs over the complete summary before we skip.)
            if (status == BatchItemStatus.Started)
                continue;

            T? unitResult = default;
            DurableExecutionException? unitError = null;

            // Re-execute only completed units to recover the stripped value/error.
            if (status == BatchItemStatus.Succeeded || status == BatchItemStatus.Failed)
            {
                var outcome = await RunSingleUnitAsync(i, cancellationToken).ConfigureAwait(false);
                if (status == BatchItemStatus.Succeeded)
                {
                    unitResult = outcome.Result;
                }
                else
                {
                    // Frozen status is authoritative. If a unit frozen as Failed
                    // re-executes to success here (non-deterministic body), it stays
                    // Failed but Error stays null — the original error was stripped on
                    // overflow and only returns if the body re-throws. Recovering a
                    // frozen-Succeeded unit's value is the common, supported case.
                    unitError = outcome.Error;
                }
            }

            items.Add(new BatchItem<T>
            {
                Index = i,
                Name = resolvedName,
                Status = status,
                Result = unitResult,
                Error = unitError
            });
        }

        // Completion reason is pinned from the frozen summary; fall back to
        // recomputing only if the summary is absent/corrupt.
        var completionReason = summary != null
            ? DeserializeCompletionReason(summary.CompletionReason)
            : ComputeCompletionReason(items, unitCount);

        // No re-checkpoint: the parent is already terminal in state. Return the
        // reconstructed result regardless of completion reason — never throw.
        return new BatchResult<T>(items, completionReason);
    }

    private async Task RunUnitAsync(
        int index,
        UnitOutcome[] slots,
        SemaphoreSlim? semaphore,
        CancellationToken cancellationToken,
        CancellationToken controlToken,
        CancellationToken shortCircuitToken,
        Action<UnitOutcome> onComplete)
    {
        try
        {
            var (unitName, unitFunc) = GetUnit(index);
            var childOpId = OperationIdGenerator.HashOperationId($"{OperationId}-{index + 1}");

            var childOp = new ChildContextOperation<T>(
                childOpId,
                unitName,
                OperationId,
                unitFunc,
                new ChildContextConfig { SubType = ChildSubType },
                Serializer,
                ChildContextFactory,
                State,
                Termination,
                _workflowCancellation,
                DurableExecutionArn,
                Batcher,
                shortCircuitToken,
                isVirtual: _isVirtual);

            try
            {
                var result = await childOp.ExecuteAsync(cancellationToken).ConfigureAwait(false);
                slots[index] = new UnitOutcome { Status = BatchItemStatus.Succeeded, Result = result };
            }
            catch (ChildContextException ex)
            {
                slots[index] = new UnitOutcome { Status = BatchItemStatus.Failed, Error = ex };
            }
            catch (DurableExecutionException)
            {
                // E.g. NonDeterministicExecutionException — these are not "unit
                // failed gracefully" but workflow-level problems. Surface them:
                // re-throw out of the operation without writing a slot (the
                // orchestrator's outer flow handles it).
                throw;
            }
            catch (OperationCanceledException) when (
                shortCircuitToken.IsCancellationRequested && !controlToken.IsCancellationRequested)
            {
                // Cooperative bail: this unit honored the short-circuit signal raised
                // when a sibling satisfied the CompletionConfig. It is neither a
                // failure nor an operation-wide cancel — record it as Started so the
                // verdict math treats it like an un-dispatched unit (and so it can
                // never trip a failure threshold). The unit wrote no terminal
                // checkpoint, so replay reconstructs it identically from the parent
                // summary.
                //
                // Ordered BEFORE the control-token clause: a genuine caller-cancel /
                // workflow-shutdown still takes precedence.
                slots[index] = new UnitOutcome { Status = BatchItemStatus.Started };
            }
            catch (OperationCanceledException) when (controlToken.IsCancellationRequested)
            {
                // Control-token cancellation — caller-cancel OR workflow shutdown (a
                // sibling op suspended, a checkpoint failed). Don't write a slot —
                // Task.WhenAll observes this and the orchestrator re-throws after
                // settling.
                throw;
            }
            catch (OperationCanceledException ex)
            {
                // Unit-internal cancellation that is NOT tied to the control token
                // (e.g. the unit's own CancellationTokenSource fired). Treat it as a
                // normal per-unit failure rather than killing the operation as
                // cancelled.
                var wrapped = new ChildContextException(ex.Message, ex)
                {
                    SubType = ChildSubType,
                    ErrorType = ex.GetType().FullName
                };
                slots[index] = new UnitOutcome { Status = BatchItemStatus.Failed, Error = wrapped };
            }
            catch (Exception ex)
            {
                // Wrap unexpected exceptions as ChildContextException — they're
                // per-unit failures from the user's POV.
                var wrapped = new ChildContextException(ex.Message, ex)
                {
                    SubType = ChildSubType,
                    ErrorType = ex.GetType().FullName
                };
                slots[index] = new UnitOutcome { Status = BatchItemStatus.Failed, Error = wrapped };
            }

            onComplete(slots[index]);
        }
        finally
        {
            // Defensive: with this structure the semaphore is only disposed after
            // Task.WhenAll(inFlight) has settled, so this Release should always
            // succeed. ObjectDisposedException would indicate a bug elsewhere, but
            // we tolerate it here so the task doesn't fault with a noise exception
            // that masks the real one.
            try
            {
                semaphore?.Release();
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }

    /// <summary>
    /// Builds and runs a single unit's <see cref="ChildContextOperation{T}"/> and
    /// maps the result/exception to a <see cref="UnitOutcome"/>. Shared by the
    /// concurrent dispatch loop (<see cref="RunUnitAsync"/>) and the overflow
    /// ReplayChildren path (<see cref="ReplayChildrenAsync"/>). Per-unit graceful
    /// failures are captured as <see cref="ChildContextException"/>; workflow-level
    /// and parent-token-cancellation exceptions propagate.
    /// </summary>
    private async Task<UnitOutcome> RunSingleUnitAsync(int index, CancellationToken cancellationToken)
    {
        var (unitName, unitFunc) = GetUnit(index);
        var childOpId = OperationIdGenerator.HashOperationId($"{OperationId}-{index + 1}");

        var childOp = new ChildContextOperation<T>(
            childOpId,
            unitName,
            OperationId,
            unitFunc,
            new ChildContextConfig { SubType = ChildSubType },
            Serializer,
            ChildContextFactory,
            State,
            Termination,
            _workflowCancellation,
            DurableExecutionArn,
            Batcher,
            isVirtual: _isVirtual);

        try
        {
            var result = await childOp.ExecuteAsync(cancellationToken).ConfigureAwait(false);
            return new UnitOutcome { Status = BatchItemStatus.Succeeded, Result = result };
        }
        catch (ChildContextException ex)
        {
            return new UnitOutcome { Status = BatchItemStatus.Failed, Error = ex };
        }
        catch (DurableExecutionException)
        {
            // E.g. NonDeterministicExecutionException — these are not "unit
            // failed gracefully" but workflow-level problems. Surface them:
            // re-throw out of the operation (the orchestrator's outer flow
            // handles it).
            throw;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Parent-token cancellation: per cross-cutting decision Q10, OCE
            // escapes unwrapped. Don't write a slot — Task.WhenAll observes
            // this and the orchestrator re-throws after settling.
            throw;
        }
        catch (OperationCanceledException ex)
        {
            // Unit-internal cancellation that is NOT tied to the parent token
            // (e.g. the unit's own CancellationTokenSource fired). Treat it as
            // a normal per-unit failure rather than killing the operation as
            // cancelled.
            var wrapped = new ChildContextException(ex.Message, ex)
            {
                SubType = ChildSubType,
                ErrorType = ex.GetType().FullName
            };
            return new UnitOutcome { Status = BatchItemStatus.Failed, Error = wrapped };
        }
        catch (Exception ex)
        {
            // Wrap unexpected exceptions as ChildContextException — they're
            // per-unit failures from the user's POV.
            var wrapped = new ChildContextException(ex.Message, ex)
            {
                SubType = ChildSubType,
                ErrorType = ex.GetType().FullName
            };
            return new UnitOutcome { Status = BatchItemStatus.Failed, Error = wrapped };
        }
    }

    private CompletionReason ComputeCompletionReason(IReadOnlyList<IBatchItem<T>> items, int totalCount)
    {
        var succeeded = 0;
        var failed = 0;

        foreach (var item in items)
        {
            switch (item.Status)
            {
                case BatchItemStatus.Succeeded: succeeded++; break;
                case BatchItemStatus.Failed:    failed++;    break;
            }
        }

        // "started" for the policy is the DECLARED total minus what settled — this
        // captures BOTH genuinely in-flight units AND never-dispatched units (which
        // are no longer materialised in items). It preserves the early-stop signal
        // (started > 0 => a CompletionConfig short-circuit skipped work) that drives
        // MinSuccessfulReached, and totalCount stays the declared count so
        // ToleratedFailurePercentage divides by the true branch count.
        var started = totalCount - succeeded - failed;

        return _policy.Evaluate(succeeded, failed, started, totalCount);
    }

    private async Task CheckpointParentResultAsync(
        BatchResult<T> result,
        CompletionReason completionReason,
        CancellationToken cancellationToken)
    {
        // Local builder: includeInline=true writes per-unit Result/Error inline
        // (Flat only); includeInline=false writes the minimal index/name/status
        // map (the shape Nested always uses, and the Flat overflow fallback).
        // The persisted summary keeps EVERY declared unit — including
        // never-dispatched ones tagged STARTED — even though result.All now omits
        // them from the user-facing view. Replay/reconstruct and the unit-name
        // drift check both loop the declared UnitCount and read per-unit
        // status/name from this summary, so it must stay complete. Dispatched
        // units are looked up by Index in result.All; the gaps are the
        // never-dispatched branches.
        BatchSummary BuildSummary(bool includeInline)
        {
            var byIndex = new Dictionary<int, IBatchItem<T>>(result.All.Count);
            foreach (var it in result.All)
                byIndex[it.Index] = it;

            var s = new BatchSummary
            {
                CompletionReason = SerializeCompletionReason(completionReason),
                Units = new List<BatchUnitSummary>(UnitCount)
            };
            for (var i = 0; i < UnitCount; i++)
            {
                var (unitName, _) = GetUnit(i);
                byIndex.TryGetValue(i, out var item);
                var unit = new BatchUnitSummary
                {
                    Index = i,
                    Name = item?.Name ?? unitName,
                    Status = SerializeStatus(item?.Status ?? BatchItemStatus.Started)
                };
                if (includeInline && _isVirtual && item != null)
                {
                    if (item.Status == BatchItemStatus.Succeeded)
                        unit.Result = SerializeResult(item.Result);
                    else if (item.Status == BatchItemStatus.Failed && item.Error != null)
                        unit.Error = ErrorObject.FromException(item.Error);
                }
                s.Units.Add(unit);
            }
            return s;
        }

        var summary = BuildSummary(includeInline: true);
        var payload = JsonSerializer.Serialize(summary, BatchJsonContext.Default.BatchSummary);

        // Flat overflow: the inline per-unit results pushed the summary over the
        // checkpoint limit. Re-emit a stripped summary (statuses only) and flag
        // ReplayChildren so replay reconstructs the values by re-executing units.
        var overflow = _isVirtual
            && Encoding.UTF8.GetByteCount(payload) > DurableConstants.MaxOperationCheckpointBytes;
        if (overflow)
        {
            summary = BuildSummary(includeInline: false);
            payload = JsonSerializer.Serialize(summary, BatchJsonContext.Default.BatchSummary);
        }

        // Always checkpoint as SUCCEED — even when FailureToleranceExceeded.
        // The completion reason lives inside the payload, matching the wire
        // format of the Python/JS/Java SDKs. The exception is thrown SDK-side
        // after this checkpoint.
        await EnqueueAsync(new SdkOperationUpdate
        {
            Id = OperationId,
            ParentId = ParentId,
            Type = OperationTypes.Context,
            Action = OperationAction.SUCCEED,
            SubType = ParentSubType,
            Name = Name,
            Payload = payload,
            ContextOptions = overflow
                ? new SdkContextOptions { ReplayChildren = true }
                : null
        }, cancellationToken);
    }

    private IBatchResult<T> ReconstructFromCheckpoints(Operation parent)
    {
        var summary = ParseSummary(parent.ContextDetails?.Result);

        var items = new List<IBatchItem<T>>(UnitCount);
        for (var i = 0; i < UnitCount; i++)
        {
            var (unitName, _) = GetUnit(i);
            var childOpId = OperationIdGenerator.HashOperationId($"{OperationId}-{i + 1}");
            var childOp = State.GetOperation(childOpId);
            var summaryEntry = summary?.Units.FirstOrDefault(b => b.Index == i);

            BatchItemStatus status = summaryEntry != null
                ? DeserializeStatus(summaryEntry.Status)
                : InferStatusFromChildOp(childOp);

            // Prefer the name that was checkpointed at the moment the batch
            // resolved. This is the only authoritative source for units reported
            // as Started (no per-unit checkpoint exists to consult), and it lets
            // us detect unit-name drift between deployments.
            var checkpointedName = summaryEntry?.Name;
            if (checkpointedName != null && unitName != null && checkpointedName != unitName)
            {
                throw new NonDeterministicExecutionException(
                    $"Non-deterministic execution detected for {OperationNoun.ToLowerInvariant()} unit {i} of operation " +
                    $"'{Name ?? OperationId}': expected name '{unitName}' but found '{checkpointedName}' " +
                    $"from a previous invocation. Code must not change the order or name of concurrent " +
                    $"units between deployments.");
            }
            var resolvedName = checkpointedName ?? unitName;

            // A never-dispatched unit (Started status with no per-unit child
            // checkpoint) is excluded from the reconstructed All, matching the
            // fresh-run view and the JS SDK. A dispatched-then-bailed unit is also
            // Started but HAS a child op, so it stays. The name-drift check above
            // still runs over the complete summary before we skip.
            if (status == BatchItemStatus.Started && childOp == null)
                continue;

            T? unitResult = default;
            DurableExecutionException? unitError = null;

            // Flat (virtual) units have no child checkpoint — their result/error
            // was recorded inline on this summary. Nested units read from the
            // child's own CONTEXT checkpoint. A unit is "inline" when the summary
            // entry carries a Result/Error, which only Flat writes.
            if (_isVirtual && summaryEntry != null)
            {
                if (status == BatchItemStatus.Succeeded && summaryEntry.Result != null)
                {
                    unitResult = DeserializeResult(summaryEntry.Result);
                }
                else if (status == BatchItemStatus.Failed && summaryEntry.Error != null)
                {
                    var err = summaryEntry.Error;
                    unitError = new ChildContextException(err.ErrorMessage ?? "Unit failed")
                    {
                        SubType = ChildSubType,
                        ErrorType = err.ErrorType,
                        ErrorData = err.ErrorData,
                        OriginalStackTrace = err.StackTrace
                    };
                }
            }
            else if (status == BatchItemStatus.Succeeded && childOp?.ContextDetails?.Result != null)
            {
                unitResult = DeserializeResult(childOp.ContextDetails.Result);
            }
            else if (status == BatchItemStatus.Failed && childOp?.ContextDetails?.Error != null)
            {
                var err = childOp.ContextDetails.Error;
                unitError = new ChildContextException(err.ErrorMessage ?? "Unit failed")
                {
                    SubType = childOp.SubType ?? ChildSubType,
                    ErrorType = err.ErrorType,
                    ErrorData = err.ErrorData,
                    OriginalStackTrace = err.StackTrace
                };
            }

            items.Add(new BatchItem<T>
            {
                Index = i,
                Name = resolvedName,
                Status = status,
                Result = unitResult,
                Error = unitError
            });
        }

        var completionReason = summary != null
            ? DeserializeCompletionReason(summary.CompletionReason)
            : ComputeCompletionReason(items, UnitCount);

        return new BatchResult<T>(items, completionReason);
    }

    private static BatchItemStatus InferStatusFromChildOp(Operation? childOp)
    {
        if (childOp == null) return BatchItemStatus.Started;
        return childOp.Status switch
        {
            OperationStatuses.Succeeded => BatchItemStatus.Succeeded,
            OperationStatuses.Failed    => BatchItemStatus.Failed,
            _                           => BatchItemStatus.Started
        };
    }

    private static BatchSummary? ParseSummary(string? payload)
    {
        if (string.IsNullOrEmpty(payload)) return null;
        try
        {
            return JsonSerializer.Deserialize(payload, BatchJsonContext.Default.BatchSummary);
        }
        catch (JsonException)
        {
            // Tolerate older / corrupted payloads — fall back to inferring status
            // from per-unit checkpoints.
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

    private T DeserializeResult(string serialized)
    {
        var bytes = Encoding.UTF8.GetBytes(serialized);
        using var ms = new MemoryStream(bytes);
        return Serializer.Deserialize<T>(ms);
    }

    /// <summary>
    /// Serializes a per-unit result for inline storage in the
    /// <see cref="BatchSummary"/> (Flat units only). Mirrors the SUCCEED-payload
    /// serialization a Nested unit's <see cref="ChildContextOperation{T}"/> would
    /// have written to its own checkpoint.
    /// </summary>
    private string SerializeResult(T? value)
    {
        using var ms = new MemoryStream();
        Serializer.Serialize(value!, ms);
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    /// <summary>
    /// Internal scratch space tracking each unit's outcome as it lands in the
    /// executor; copied into the user-facing <see cref="BatchItem{T}"/> once every
    /// dispatched unit has settled.
    /// </summary>
    private struct UnitOutcome
    {
        public BatchItemStatus Status;
        public T? Result;
        public DurableExecutionException? Error;
    }
}
