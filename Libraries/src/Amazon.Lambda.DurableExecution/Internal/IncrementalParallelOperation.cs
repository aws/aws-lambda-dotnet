// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using Amazon.Lambda;
using Amazon.Lambda.Core;
using SdkContextOptions = Amazon.Lambda.Model.ContextOptions;
using SdkOperationUpdate = Amazon.Lambda.Model.OperationUpdate;

namespace Amazon.Lambda.DurableExecution.Internal;

/// <summary>
/// Which replay branch the operation is on, decided once from the parent CONTEXT
/// checkpoint at construction.
/// </summary>
internal enum ParallelExecutionMode
{
    /// <summary>No terminal parent checkpoint: run branches (fresh, or STARTED/PENDING
    /// where each branch replays from its own checkpoint). The parent SUCCEED is
    /// written by <see cref="IncrementalParallelOperation.CompleteAsync"/>.</summary>
    Run,

    /// <summary>Parent already terminal: reconstruct branch outcomes from the frozen
    /// <see cref="BatchSummary"/> (re-running a branch only to recover a value that
    /// was stripped on overflow). The parent is NOT re-checkpointed.</summary>
    Terminal
}

/// <summary>
/// Type-erased outcome of a single branch, gathered by the orchestrator to build
/// the parent <see cref="BatchSummary"/> without knowing each branch's <c>T</c>.
/// </summary>
internal readonly struct BranchOutcome
{
    public int Index { get; init; }
    public string? Name { get; init; }
    public BatchItemStatus Status { get; init; }

    /// <summary>Serialized branch result (succeeded branches only).</summary>
    public string? SerializedResult { get; init; }

    /// <summary>Branch error (failed branches only).</summary>
    public ErrorObject? Error { get; init; }

    public static BranchOutcome Success(int index, string? name, string? serialized) =>
        new() { Index = index, Name = name, Status = BatchItemStatus.Succeeded, SerializedResult = serialized };

    public static BranchOutcome Failure(int index, string? name, ErrorObject error) =>
        new() { Index = index, Name = name, Status = BatchItemStatus.Failed, Error = error };

    public static BranchOutcome Skipped(int index, string? name) =>
        new() { Index = index, Name = name, Status = BatchItemStatus.Started };
}

/// <summary>
/// Type-erased view the orchestrator holds over each branch handle, so it can
/// await settlement and read per-branch identity/status without the branch's
/// generic parameter.
/// </summary>
internal interface IParallelBranchController
{
    int Index { get; }
    string Name { get; }
    BatchItemStatus Status { get; }

    /// <summary>
    /// Completes (never faults for a graceful per-branch failure) with the branch's
    /// <see cref="BranchOutcome"/>. Faults only for workflow-level errors
    /// (e.g. <see cref="NonDeterministicExecutionException"/>) or control-token
    /// cancellation, which the orchestrator surfaces.
    /// </summary>
    Task<BranchOutcome> Settlement { get; }
}

/// <summary>
/// Typed, awaitable handle for a single branch of an
/// <see cref="IncrementalParallelOperation"/>. Backs the public
/// <see cref="IParallelBranch{T}"/>; also exposes the type-erased
/// <see cref="IParallelBranchController"/> the orchestrator uses to aggregate.
/// </summary>
internal sealed class IncrementalParallelBranch<T> : IParallelBranch<T>, IParallelBranchController
{
    private readonly TaskCompletionSource<T> _result =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ILambdaSerializer _serializer;
    private readonly string _childSubType;

    // Frozen status wins over the live re-run outcome on the overflow-recovery path
    // (Terminal mode): the checkpointed verdict is authoritative even if a
    // non-deterministic body re-executes to a different result.
    private BatchItemStatus? _frozenStatus;
    private volatile int _status = (int)BatchItemStatus.Started;

    public IncrementalParallelBranch(int index, string name, ILambdaSerializer serializer, string childSubType)
    {
        Index = index;
        Name = name;
        _serializer = serializer;
        _childSubType = childSubType;

        // Per-branch failures are intentionally consumed via CompleteAsync, so a
        // caller may never await this handle. Observe the fault here so a discarded
        // failed handle can never surface as an UnobservedTaskException.
        _ = _result.Task.ContinueWith(
            static t => { _ = t.Exception; },
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    public string Name { get; }
    public int Index { get; }
    public BatchItemStatus Status => _frozenStatus ?? (BatchItemStatus)_status;
    public Task<BranchOutcome> Settlement { get; private set; } = null!;

    public TaskAwaiter<T> GetAwaiter() => _result.Task.GetAwaiter();

    /// <summary>
    /// Run mode: wrap a live child-context execution. <paramref name="frozenStatus"/>
    /// is set only on the overflow-recovery path, where the checkpointed status is
    /// authoritative and the run merely recovers the stripped value.
    /// </summary>
    public void Launch(
        Func<Task<T>> run,
        CancellationToken shortCircuitToken,
        CancellationToken controlToken,
        BatchItemStatus? frozenStatus = null)
    {
        _frozenStatus = frozenStatus;
        Settlement = ExecuteAsync(run, shortCircuitToken, controlToken);
    }

    /// <summary>
    /// Terminal mode: resolve the branch directly from the frozen summary without
    /// running it (the common, non-overflow reconstruct path).
    /// </summary>
    public void ResolveFromInline(BatchItemStatus status, string? serializedResult, ErrorObject? error)
    {
        _frozenStatus = status;
        switch (status)
        {
            case BatchItemStatus.Succeeded:
                _result.TrySetResult(Deserialize(serializedResult));
                Settlement = Task.FromResult(BranchOutcome.Success(Index, Name, serializedResult));
                break;
            case BatchItemStatus.Failed:
                _result.TrySetException(BuildError(error));
                Settlement = Task.FromResult(BranchOutcome.Failure(Index, Name, error ?? new ErrorObject { ErrorMessage = "Branch failed" }));
                break;
            default:
                _result.TrySetException(SkippedError());
                Settlement = Task.FromResult(BranchOutcome.Skipped(Index, Name));
                break;
        }
    }

    private async Task<BranchOutcome> ExecuteAsync(
        Func<Task<T>> run,
        CancellationToken shortCircuitToken,
        CancellationToken controlToken)
    {
        try
        {
            var value = await run().ConfigureAwait(false);
            if (_frozenStatus is null) _status = (int)BatchItemStatus.Succeeded;
            _result.TrySetResult(value);
            return BranchOutcome.Success(Index, Name, Serialize(value));
        }
        catch (ChildContextException ex)
        {
            if (_frozenStatus is null) _status = (int)BatchItemStatus.Failed;
            _result.TrySetException(ex);
            return BranchOutcome.Failure(Index, Name, ErrorObject.FromException(ex));
        }
        catch (DurableExecutionException)
        {
            // Workflow-level error (e.g. NonDeterministicExecutionException): not a
            // graceful per-branch failure. Fault the settlement so the orchestrator
            // surfaces it out of CompleteAsync.
            throw;
        }
        catch (OperationCanceledException)
            when (shortCircuitToken.IsCancellationRequested && !controlToken.IsCancellationRequested)
        {
            // Cooperative bail: a sibling satisfied the CompletionConfig before this
            // branch acquired its concurrency slot (or its body honored the bail
            // token). Record it as skipped — never a failure.
            if (_frozenStatus is null) _status = (int)BatchItemStatus.Started;
            _result.TrySetException(SkippedError());
            return BranchOutcome.Skipped(Index, Name);
        }
        catch (OperationCanceledException) when (controlToken.IsCancellationRequested)
        {
            // Caller-cancel or workflow shutdown: propagate.
            _result.TrySetCanceled();
            throw;
        }
        catch (OperationCanceledException ex)
        {
            var wrapped = Wrap(ex);
            if (_frozenStatus is null) _status = (int)BatchItemStatus.Failed;
            _result.TrySetException(wrapped);
            return BranchOutcome.Failure(Index, Name, ErrorObject.FromException(wrapped));
        }
        catch (Exception ex)
        {
            var wrapped = Wrap(ex);
            if (_frozenStatus is null) _status = (int)BatchItemStatus.Failed;
            _result.TrySetException(wrapped);
            return BranchOutcome.Failure(Index, Name, ErrorObject.FromException(wrapped));
        }
    }

    private ChildContextException Wrap(Exception ex) => new(ex.Message, ex)
    {
        SubType = _childSubType,
        ErrorType = ex.GetType().FullName
    };

    private ChildContextException BuildError(ErrorObject? error) =>
        new(error?.ErrorMessage ?? "Branch failed")
        {
            SubType = _childSubType,
            ErrorType = error?.ErrorType,
            ErrorData = error?.ErrorData,
            OriginalStackTrace = error?.StackTrace
        };

    private DurableExecutionException SkippedError() => new(
        $"Parallel branch '{Name}' (index {Index}) did not execute: the parallel " +
        $"operation completed before it started (completion-policy short-circuit). " +
        $"Inspect the branch's Status before awaiting it.");

    private string Serialize(T value)
    {
        using var ms = new MemoryStream();
        _serializer.Serialize(value, ms);
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private T Deserialize(string? serialized)
    {
        if (serialized == null) return default!;
        var bytes = Encoding.UTF8.GetBytes(serialized);
        using var ms = new MemoryStream(bytes);
        return _serializer.Deserialize<T>(ms);
    }
}

/// <summary>
/// Incremental, heterogeneous parallel orchestrator implementing
/// <see cref="IDurableParallel"/>. Each branch runs as a
/// <see cref="ChildContextOperation{T}"/> under the SAME deterministic child
/// operation-ID scheme (<c>hash("{parentId}-{index}")</c>) and the SAME parent
/// <see cref="BatchSummary"/> checkpoint shape as the batch
/// <see cref="ParallelOperation{T}"/>, so a checkpoint written by one is
/// reconstructable by the other. Branch identity is positional: register the same
/// branches in the same order across replays.
/// </summary>
internal sealed class IncrementalParallelOperation : IDurableParallel
{
    private readonly string _operationId;
    private readonly string? _name;
    private readonly string? _parentId;
    private readonly CompletionPolicy _policy;
    private readonly int? _maxConcurrency;
    private readonly bool _isVirtual;
    private readonly ILambdaSerializer _serializer;
    private readonly Func<string, string?, bool, IDurableContext> _childContextFactory;
    private readonly ExecutionState _state;
    private readonly TerminationManager _termination;
    private readonly WorkflowCancellation _workflowCancellation;
    private readonly string _durableExecutionArn;
    private readonly CheckpointBatcher? _batcher;

    private readonly object _lock = new();
    private readonly List<IParallelBranchController> _branches = new();
    private readonly SemaphoreSlim? _semaphore;
    private readonly CancellationTokenSource _shortCircuitCts = new();
    private readonly CancellationTokenSource _dispatchCts;

    private readonly ParallelExecutionMode _mode;
    private readonly BatchSummary? _frozenSummary;
    private readonly Task _startTask;

    private int _succeeded;
    private int _failed;
    private int _registeredCount;
    private bool _sealed;
    private volatile bool _sealedVolatile;
    private bool _disposed;
    private Task<IBatchResult>? _completion;

    public IncrementalParallelOperation(
        string operationId,
        string? name,
        string? parentId,
        ParallelConfig config,
        ILambdaSerializer serializer,
        Func<string, string?, bool, IDurableContext> childContextFactory,
        ExecutionState state,
        TerminationManager termination,
        WorkflowCancellation workflowCancellation,
        string durableExecutionArn,
        CheckpointBatcher? batcher = null)
    {
        _operationId = operationId;
        _name = name;
        _parentId = parentId;
        _policy = new CompletionPolicy(config.CompletionConfig);
        _maxConcurrency = config.MaxConcurrency;
        _isVirtual = config.NestingType == NestingType.Flat;
        _serializer = serializer;
        _childContextFactory = childContextFactory;
        _state = state;
        _termination = termination;
        _workflowCancellation = workflowCancellation;
        _durableExecutionArn = durableExecutionArn;
        _batcher = batcher;

        _semaphore = _maxConcurrency is { } mc ? new SemaphoreSlim(mc, mc) : null;
        _dispatchCts = CancellationTokenSource.CreateLinkedTokenSource(
            _shortCircuitCts.Token, workflowCancellation.Token);

        // The parent operation position has been reached — mirror the base
        // DurableOperation.ExecuteAsync bookkeeping for the parent CONTEXT op.
        _state.ValidateReplayConsistency(_operationId, OperationTypes.Context, _name);
        _state.TrackReplay(_operationId);

        var existing = _state.GetOperation(_operationId);
        if (existing == null)
        {
            // Fresh: emit the parent CONTEXT START so the service has a parent
            // record if a branch suspends. Enqueued once here so it is ordered
            // before any branch's child START.
            _mode = ParallelExecutionMode.Run;
            _startTask = EnqueueAsync(new SdkOperationUpdate
            {
                Id = _operationId,
                ParentId = _parentId,
                Type = OperationTypes.Context,
                Action = OperationAction.START,
                SubType = OperationSubTypes.Parallel,
                Name = _name
            });
        }
        else
        {
            // Mirror ConcurrentOperation.ReplayAsync: only SUCCEEDED reconstructs,
            // only STARTED/PENDING re-run, and any other status is a replay
            // mismatch. The parent parallel only ever checkpoints SUCCEED, so a
            // FAILED/CANCELLED/STOPPED/TIMED_OUT parent must never be silently
            // re-run (which would overwrite the prior terminal outcome).
            switch (existing.Status)
            {
                case OperationStatuses.Succeeded:
                    _mode = ParallelExecutionMode.Terminal;
                    _frozenSummary = BatchSummaryCodec.ParseSummary(existing.ContextDetails?.Result);
                    _startTask = Task.CompletedTask;
                    break;
                case OperationStatuses.Started:
                case OperationStatuses.Pending:
                    // Children replay from their own checkpoints; the parent START
                    // is not re-emitted (the original is authoritative).
                    _mode = ParallelExecutionMode.Run;
                    _startTask = Task.CompletedTask;
                    break;
                default:
                    throw new NonDeterministicExecutionException(
                        $"Parallel operation '{_name ?? _operationId}' has unexpected status " +
                        $"'{existing.Status}' on replay.");
            }
        }
    }

    public IParallelBranch<T> BranchAsync<T>(
        string name,
        Func<IDurableContext, CancellationToken, Task<T>> func)
    {
        if (name == null) throw new ArgumentNullException(nameof(name));
        if (func == null) throw new ArgumentNullException(nameof(func));

        lock (_lock)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(IDurableParallel));
            if (_sealed)
                throw new InvalidOperationException(
                    "Cannot register a branch after the parallel operation has been sealed by CompleteAsync() or disposal.");

            var index = _branches.Count;                 // zero-based branch index
            var childOpId = OperationIdGenerator.HashOperationId($"{_operationId}-{index + 1}");
            var handle = new IncrementalParallelBranch<T>(index, name, _serializer, OperationSubTypes.ParallelBranch);

            var summaryEntry = FindSummaryUnit(index);

            // Strict name-drift check: a branch's name must be stable at its index
            // across deployments (matches the batch Parallel reconstruct check).
            if (summaryEntry?.Name != null && summaryEntry.Name != name)
            {
                throw new NonDeterministicExecutionException(
                    $"Non-deterministic execution detected for parallel branch {index} of operation " +
                    $"'{_name ?? _operationId}': expected name '{name}' but found '{summaryEntry.Name}' " +
                    $"from a previous invocation. Code must not change the order or name of branches " +
                    $"between deployments.");
            }

            if (_mode == ParallelExecutionMode.Terminal)
            {
                ResolveTerminalBranch(handle, name, childOpId, func, summaryEntry);
            }
            else
            {
                LaunchRunBranch(handle, name, childOpId, func);
            }

            _branches.Add(handle);
            _registeredCount = _branches.Count;
            return handle;
        }
    }

    public Task<IBatchResult> CompleteAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            // Cache the in-progress task (not just the finished result) so two
            // concurrent CompleteAsync calls — or DisposeAsync racing one — share a
            // single completion and enqueue exactly one parent SUCCEED.
            if (_completion != null) return _completion;
            _sealed = true;
            _sealedVolatile = true;
            _completion = CompleteCoreAsync(cancellationToken);
            return _completion;
        }
    }

    private async Task<IBatchResult> CompleteCoreAsync(CancellationToken cancellationToken)
    {
        // Registration is sealed: the denominator is now known, so re-evaluate the
        // completion policy (including percentage-based tolerance, which is
        // suppressed pre-seal) and signal any in-flight branches to bail.
        if (ShouldStopDispatchingNow())
        {
            try { _shortCircuitCts.Cancel(); }
            catch (ObjectDisposedException) { }
        }

        // Ensure the parent START is durably enqueued even for an empty operation.
        await _startTask.ConfigureAwait(false);

        var controllers = SnapshotControllers();

        // Await every branch settlement. Task.WhenAll surfaces only the first
        // exception; swallow here and inspect each below so a workflow-level fault
        // is surfaced deterministically and graceful failures aggregate.
        if (controllers.Count > 0)
        {
            try { await Task.WhenAll(controllers.Select(c => c.Settlement)).ConfigureAwait(false); }
            catch { /* inspected below */ }
        }

        foreach (var c in controllers)
        {
            var s = c.Settlement;
            if (s.IsFaulted && s.Exception is { } agg)
            {
                foreach (var inner in agg.InnerExceptions)
                {
                    if (inner is DurableExecutionException dex && inner is not ChildContextException)
                        throw dex;
                }
            }
        }

        // A torn-down operation propagates cancellation rather than a synthesized verdict.
        _workflowCancellation.Token.ThrowIfCancellationRequested();
        cancellationToken.ThrowIfCancellationRequested();

        IBatchResult result = _mode == ParallelExecutionMode.Terminal
            ? BuildTerminalResult(controllers)
            : await BuildAndCheckpointRunResultAsync(controllers, cancellationToken).ConfigureAwait(false);

        return result;
    }

    public async ValueTask DisposeAsync()
    {
        Task<IBatchResult>? completion;
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;
            // If CompleteAsync was never called, complete now so the parent's
            // terminal checkpoint is written — otherwise replay would see a STARTED
            // parent forever and re-run the whole operation.
            completion = _completion;
        }

        try
        {
            completion ??= CompleteAsync(CancellationToken.None);
            await completion.ConfigureAwait(false);
        }
        catch
        {
            // DisposeAsync must never throw. A completion fault (e.g. a
            // NonDeterministicExecutionException, or the secondary effect of a
            // BranchAsync that already threw during registration) is either
            // already surfaced to a caller that awaited CompleteAsync, or will
            // resurface on the next invocation's replay. Swallow it here so
            // `await using` teardown stays clean.
        }
        finally
        {
            _shortCircuitCts.Dispose();
            _dispatchCts.Dispose();
            _semaphore?.Dispose();
        }
    }

    // ── Run mode ────────────────────────────────────────────────────────

    private void LaunchRunBranch<T>(
        IncrementalParallelBranch<T> handle,
        string name,
        string childOpId,
        Func<IDurableContext, CancellationToken, Task<T>> func,
        BatchItemStatus? frozenStatus = null)
    {
        async Task<T> Run()
        {
            // Parent START must be enqueued before this branch's child START.
            await _startTask.ConfigureAwait(false);

            if (_semaphore != null)
            {
                await _semaphore.WaitAsync(_dispatchCts.Token).ConfigureAwait(false);
            }

            try
            {
                // A short-circuit may have fired while waiting on the semaphore.
                _dispatchCts.Token.ThrowIfCancellationRequested();

                var childOp = new ChildContextOperation<T>(
                    childOpId,
                    name,
                    _operationId,
                    func,
                    new ChildContextConfig { SubType = OperationSubTypes.ParallelBranch },
                    _serializer,
                    _childContextFactory,
                    _state,
                    _termination,
                    _workflowCancellation,
                    _durableExecutionArn,
                    _batcher,
                    _shortCircuitCts.Token,
                    isVirtual: _isVirtual);

                // Branch child ops receive CancellationToken.None here — they re-link
                // workflow-shutdown and the cooperative-bail token internally, and
                // their checkpoint writes must not observe shutdown mid-flush.
                return await childOp.ExecuteAsync(CancellationToken.None).ConfigureAwait(false);
            }
            finally
            {
                _semaphore?.Release();
            }
        }

        handle.Launch(Run, _shortCircuitCts.Token, _workflowCancellation.Token, frozenStatus);
        ObserveSettlement(handle.Settlement);
    }

    private void ObserveSettlement(Task<BranchOutcome> settlement)
    {
        _ = settlement.ContinueWith(
            OnBranchSettled,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private void OnBranchSettled(Task<BranchOutcome> settlement)
    {
        if (settlement.Status != TaskStatus.RanToCompletion) return;

        switch (settlement.Result.Status)
        {
            case BatchItemStatus.Succeeded: Interlocked.Increment(ref _succeeded); break;
            case BatchItemStatus.Failed: Interlocked.Increment(ref _failed); break;
        }

        // The deciding completion usually lands after all currently-registered
        // branches were dispatched, so re-check here and signal stragglers to bail.
        if (ShouldStopDispatchingNow())
        {
            try { _shortCircuitCts.Cancel(); }
            catch (ObjectDisposedException) { }
        }
    }

    // During incremental registration the "total" is the number registered so far;
    // percentage-based tolerance is evaluated against that running total. MinSuccessful
    // and count-based tolerance don't depend on the total.
    // During incremental registration the denominator is unknown, so percentage-based
    // failure tolerance must NOT drive short-circuiting (a premature ratio like 1/1
    // could skip branches that would have lowered the final ratio). MinSuccessful and
    // absolute-count tolerance are denominator-independent and always apply; the
    // percentage component is enabled only once registration is sealed, and the final
    // verdict (ComputeCompletionReason) always uses the true total.
    private bool ShouldStopDispatchingNow() => _policy.ShouldStopDispatching(
        Volatile.Read(ref _succeeded), Volatile.Read(ref _failed), Volatile.Read(ref _registeredCount),
        evaluatePercentage: _sealedVolatile);

    private async Task<IBatchResult> BuildAndCheckpointRunResultAsync(
        IReadOnlyList<IParallelBranchController> controllers,
        CancellationToken cancellationToken)
    {
        var outcomes = new List<BranchOutcome>(controllers.Count);
        foreach (var c in controllers)
        {
            var s = c.Settlement;
            outcomes.Add(s.Status == TaskStatus.RanToCompletion
                ? s.Result
                : BranchOutcome.Skipped(c.Index, c.Name)); // defensive; faults handled above
        }

        var reason = ComputeCompletionReason(outcomes);
        await CheckpointParentSucceedAsync(outcomes, reason, cancellationToken).ConfigureAwait(false);
        return BuildResult(outcomes, reason);
    }

    private CompletionReason ComputeCompletionReason(IReadOnlyList<BranchOutcome> outcomes)
    {
        var succeeded = 0;
        var failed = 0;
        foreach (var o in outcomes)
        {
            if (o.Status == BatchItemStatus.Succeeded) succeeded++;
            else if (o.Status == BatchItemStatus.Failed) failed++;
        }

        var total = outcomes.Count;
        var started = total - succeeded - failed;
        return _policy.Evaluate(succeeded, failed, started, total);
    }

    private async Task CheckpointParentSucceedAsync(
        IReadOnlyList<BranchOutcome> outcomes,
        CompletionReason reason,
        CancellationToken cancellationToken)
    {
        BatchSummary Build(bool includeInline)
        {
            var s = new BatchSummary
            {
                CompletionReason = BatchSummaryCodec.SerializeCompletionReason(reason),
                Units = new List<BatchUnitSummary>(outcomes.Count)
            };
            foreach (var o in outcomes)
            {
                var unit = new BatchUnitSummary
                {
                    Index = o.Index,
                    Name = o.Name,
                    Status = BatchSummaryCodec.SerializeStatus(o.Status)
                };
                if (includeInline)
                {
                    if (o.Status == BatchItemStatus.Succeeded) unit.Result = o.SerializedResult;
                    else if (o.Status == BatchItemStatus.Failed) unit.Error = o.Error;
                }
                s.Units.Add(unit);
            }
            return s;
        }

        var summary = Build(includeInline: true);
        var payload = BatchSummaryCodec.ToPayload(summary);

        var overflow = BatchSummaryCodec.IsOverflow(payload);
        if (overflow)
        {
            summary = Build(includeInline: false);
            payload = BatchSummaryCodec.ToPayload(summary);
        }

        await EnqueueAsync(new SdkOperationUpdate
        {
            Id = _operationId,
            ParentId = _parentId,
            Type = OperationTypes.Context,
            Action = OperationAction.SUCCEED,
            SubType = OperationSubTypes.Parallel,
            Name = _name,
            Payload = payload,
            ContextOptions = overflow ? new SdkContextOptions { ReplayChildren = true } : null
        }, cancellationToken).ConfigureAwait(false);
    }

    // ── Terminal (reconstruct) mode ─────────────────────────────────────

    private void ResolveTerminalBranch<T>(
        IncrementalParallelBranch<T> handle,
        string name,
        string childOpId,
        Func<IDurableContext, CancellationToken, Task<T>> func,
        BatchUnitSummary? summaryEntry)
    {
        // A branch registered now but absent from the frozen summary (registered
        // after the original seal) never ran — surface it as skipped.
        if (summaryEntry == null)
        {
            handle.ResolveFromInline(BatchItemStatus.Started, null, null);
            return;
        }

        var status = BatchSummaryCodec.DeserializeStatus(summaryEntry.Status);

        switch (status)
        {
            case BatchItemStatus.Succeeded when summaryEntry.Result != null:
                handle.ResolveFromInline(BatchItemStatus.Succeeded, summaryEntry.Result, null);
                break;
            case BatchItemStatus.Failed when summaryEntry.Error != null:
                handle.ResolveFromInline(BatchItemStatus.Failed, null, summaryEntry.Error);
                break;
            case BatchItemStatus.Succeeded:
            case BatchItemStatus.Failed:
                // Overflow: the inline value/error was stripped. Re-run the branch to
                // recover it from the branch's own checkpoint; the frozen status stays
                // authoritative.
                LaunchRunBranch(handle, name, childOpId, func, frozenStatus: status);
                break;
            default:
                handle.ResolveFromInline(BatchItemStatus.Started, null, null);
                break;
        }
    }

    private IBatchResult BuildTerminalResult(IReadOnlyList<IParallelBranchController> controllers)
    {
        // Prefer the frozen summary (authoritative for status + completion reason).
        // Fall back to the registered controllers when the payload is missing/corrupt.
        if (_frozenSummary != null)
        {
            // Positional replay contract: the same branches must be registered in the
            // same order every invocation. A different count (a branch added or removed
            // vs. the sealed run) would silently skip or double-count units, so reject it.
            if (_registeredCount != _frozenSummary.Units.Count)
            {
                throw new NonDeterministicExecutionException(
                    $"Non-deterministic execution detected for parallel operation " +
                    $"'{_name ?? _operationId}': registered {_registeredCount} branch(es) on replay " +
                    $"but the checkpoint recorded {_frozenSummary.Units.Count}. Code must register the " +
                    $"same branches in the same order between deployments.");
            }

            var succeeded = 0;
            var failed = 0;
            var started = 0;
            foreach (var u in _frozenSummary.Units)
            {
                switch (BatchSummaryCodec.DeserializeStatus(u.Status))
                {
                    case BatchItemStatus.Succeeded: succeeded++; break;
                    case BatchItemStatus.Failed: failed++; break;
                    default: started++; break;
                }
            }
            var reason = BatchSummaryCodec.DeserializeCompletionReason(_frozenSummary.CompletionReason);
            return new IncrementalBatchResult(reason, succeeded, failed, started, _frozenSummary.Units.Count);
        }

        var outcomes = new List<BranchOutcome>(controllers.Count);
        foreach (var c in controllers)
        {
            var s = c.Settlement;
            outcomes.Add(s.Status == TaskStatus.RanToCompletion ? s.Result : BranchOutcome.Skipped(c.Index, c.Name));
        }
        return BuildResult(outcomes, ComputeCompletionReason(outcomes));
    }

    // ── Shared helpers ──────────────────────────────────────────────────

    private static IBatchResult BuildResult(IReadOnlyList<BranchOutcome> outcomes, CompletionReason reason)
    {
        var succeeded = 0;
        var failed = 0;
        var started = 0;
        foreach (var o in outcomes)
        {
            switch (o.Status)
            {
                case BatchItemStatus.Succeeded: succeeded++; break;
                case BatchItemStatus.Failed: failed++; break;
                default: started++; break;
            }
        }
        return new IncrementalBatchResult(reason, succeeded, failed, started, outcomes.Count);
    }

    private BatchUnitSummary? FindSummaryUnit(int index)
    {
        if (_frozenSummary == null) return null;
        foreach (var u in _frozenSummary.Units)
        {
            if (u.Index == index) return u;
        }
        return null;
    }

    private IReadOnlyList<IParallelBranchController> SnapshotControllers()
    {
        lock (_lock) return _branches.ToArray();
    }

    private Task EnqueueAsync(SdkOperationUpdate update, CancellationToken cancellationToken = default)
        => _batcher?.EnqueueAsync(update, cancellationToken) ?? Task.CompletedTask;
}

/// <summary>
/// Non-generic <see cref="IBatchResult"/> returned by
/// <see cref="IncrementalParallelOperation.CompleteAsync"/>. Per-branch typed
/// values are retrieved from the individual <see cref="IParallelBranch{T}"/>
/// handles; this type carries only the aggregate bookkeeping.
/// </summary>
internal sealed class IncrementalBatchResult : IBatchResult
{
    public IncrementalBatchResult(
        CompletionReason completionReason,
        int successCount,
        int failureCount,
        int startedCount,
        int totalCount)
    {
        CompletionReason = completionReason;
        SuccessCount = successCount;
        FailureCount = failureCount;
        StartedCount = startedCount;
        TotalCount = totalCount;
    }

    public CompletionReason CompletionReason { get; }
    public bool HasFailure => FailureCount > 0;
    public int SuccessCount { get; }
    public int FailureCount { get; }
    public int StartedCount { get; }
    public int TotalCount { get; }
}
