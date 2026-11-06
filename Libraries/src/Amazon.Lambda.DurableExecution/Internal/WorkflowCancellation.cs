// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Amazon.Lambda.Core;

namespace Amazon.Lambda.DurableExecution.Internal;

/// <summary>
/// Why the workflow-scoped token was cancelled. The first reason wins.
/// </summary>
internal enum WorkflowCancellationReason
{
    None = 0,

    /// <summary>The <see cref="TerminationManager"/> resolved (suspend or checkpoint failure).</summary>
    Terminated = 1,

    /// <summary>The invocation deadline passed (Lambda Managed Instances only).</summary>
    DeadlineExceeded = 2,

    /// <summary>The wrapper finished the invocation and is tearing it down.</summary>
    Teardown = 3,
}

/// <summary>
/// Workflow-scoped cancellation source. Cancels when the
/// <see cref="TerminationManager"/> resolves, when the optional invocation
/// deadline passes, or when <see cref="DurableFunction"/> tears the invocation
/// down. Abandoned user-<c>Func</c> bodies (the WhenAny loser in
/// <see cref="DurableExecutionHandler"/>) unwind via
/// <see cref="OperationCanceledException"/> instead of running to completion on
/// the threadpool.
/// </summary>
/// <remarks>
/// One instance per durable function invocation, constructed and disposed by
/// <see cref="DurableFunction"/>. Operation classes that invoke user
/// <c>Func</c>s build a per-call linked CTS combining the caller's token with
/// <see cref="Token"/> and pass the linked token into the user code.
/// <para>
/// Checkpoint writes, batcher flushes, and other SDK-internal work do NOT
/// observe this token: successful work must persist even when the workflow is
/// being torn down.
/// </para>
/// <para>
/// Rules that matter when several invocations share one process (Lambda
/// Managed Instances), where a stuck SDK thread blocks a runtime processing loop:
/// </para>
/// <list type="bullet">
///   <item>The SDK never runs cancellation callbacks it does not own on its own
///       threads. User code and the service client register callbacks on these
///       tokens (directly, or through linked tokens), and a callback can block or
///       throw. Every cancel the SDK starts uses
///       <see cref="CancellationTokenSource.CancelAsync"/>: the token reports
///       <c>IsCancellationRequested</c> immediately, and callbacks run on the
///       threadpool. Their exceptions are observed and logged.</item>
///   <item>The SDK's own decisions are published before any foreign code runs:
///       admission is closed, the deadline is recorded and
///       <see cref="DeadlineTask"/> is signalled first.</item>
///   <item>The deadline is a monotonic timestamp, not just a timer. A timer
///       callback can run late when the threadpool is busy, so
///       <see cref="IsDeadlineExceeded"/> also compares the clock, and fires the
///       deadline itself when the timer has not yet.</item>
///   <item>Cancellation sources are disposed only after callback delivery has
///       finished, so no registered callback is ever skipped. <see cref="Token"/>
///       is captured at construction, so abandoned user code that reads it after
///       disposal gets a cancelled token instead of an
///       <see cref="ObjectDisposedException"/>.</item>
/// </list>
/// </remarks>
internal sealed class WorkflowCancellation : IDisposable
{
    private readonly CancellationTokenSource _cts = new();
    private readonly CancellationToken _token;
    private readonly CancellationTokenSource? _deadlineCts;
    private readonly long _deadlineTimestamp;
    private readonly Timer? _deadlineTimer;
    private readonly TaskCompletionSource _deadlineTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly object _cancelLock = new();
    private bool _sourcesReleased;
    private Task? _cancelDelivery;
    private Task? _deadlineDelivery;
    private int _reason;
    private int _tornDown;
    private int _deadlineFired;
    private int _disposed;

    public CancellationToken Token => _token;

    /// <summary>
    /// The reason the token was cancelled, or <see cref="WorkflowCancellationReason.None"/>.
    /// </summary>
    public WorkflowCancellationReason Reason => (WorkflowCancellationReason)Volatile.Read(ref _reason);

    /// <summary>
    /// True once the invocation deadline passed. Compares the clock as well as
    /// checking whether the deadline fired, so it is accurate even when the
    /// threadpool delays the timer or a suspension cancelled the workflow first.
    /// When the clock shows the deadline has passed but the timer has not run,
    /// this fires the deadline, so every observer sees one consistent outcome.
    /// </summary>
    public bool IsDeadlineExceeded
    {
        get
        {
            if (_deadlineCts is null) return false;
            if (Volatile.Read(ref _deadlineFired) == 1) return true;
            if (Stopwatch.GetTimestamp() < _deadlineTimestamp) return false;
            TriggerDeadline();
            return true;
        }
    }

    /// <summary>
    /// True once the invocation is being torn down (the wrapper finished or the
    /// deadline passed). New durable operations are rejected from this point.
    /// </summary>
    public bool IsTornDown => Volatile.Read(ref _tornDown) == 1 || IsDeadlineExceeded;

    /// <summary>
    /// Cancels when the invocation deadline passes. Passed to SDK-owned service
    /// calls (state hydration). <see cref="CancellationToken.None"/> when there is
    /// no deadline, so on-demand Lambda calls are unchanged. Do not wait on this
    /// token to learn the deadline passed: its callbacks belong to the client and
    /// may be delayed. Use <see cref="DeadlineTask"/> or <see cref="IsDeadlineExceeded"/>.
    /// </summary>
    public CancellationToken DeadlineToken { get; }

    /// <summary>
    /// Completes when the invocation deadline passes, before any foreign callback
    /// runs. Never completes when there is no deadline.
    /// </summary>
    public Task DeadlineTask => _deadlineTcs.Task;

    /// <summary>
    /// Completes once every callback registered on <see cref="Token"/> has run.
    /// Already complete before the token is cancelled. Exposed for tests and
    /// for disposal ordering.
    /// </summary>
    internal Task CancellationDelivered => Volatile.Read(ref _cancelDelivery) ?? Task.CompletedTask;

    /// <param name="terminationManager">Suspension signal for this invocation.</param>
    /// <param name="deadline">
    /// Time until the invocation deadline, or <c>null</c> for no deadline. Only
    /// set for Lambda Managed Instances; see <see cref="InvocationDeadline"/>.
    /// </param>
    /// <param name="anchorTimestamp">
    /// The <see cref="Stopwatch.GetTimestamp"/> at which <paramref name="deadline"/>
    /// was measured. Time elapsed since then is subtracted, so the deadline does
    /// not drift by however long it took to get here. Defaults to now.
    /// </param>
    /// <param name="armTimer">
    /// Test seam. <c>false</c> leaves the deadline to fire only through
    /// <see cref="TriggerDeadline"/> or the clock check in
    /// <see cref="IsDeadlineExceeded"/>, which models a timer the threadpool has
    /// not run yet. Production code always arms the timer.
    /// </param>
    public WorkflowCancellation(TerminationManager terminationManager, TimeSpan? deadline = null, long? anchorTimestamp = null,
        bool armTimer = true)
    {
        _token = _cts.Token;

        terminationManager.TerminationTask.ContinueWith(
            static (_, state) => ((WorkflowCancellation)state!).Cancel(WorkflowCancellationReason.Terminated),
            this,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        if (deadline is { } remaining)
        {
            _deadlineCts = new CancellationTokenSource();
            DeadlineToken = _deadlineCts.Token;

            var anchor = anchorTimestamp ?? Stopwatch.GetTimestamp();
            _deadlineTimestamp = anchor + (long)(remaining.TotalSeconds * Stopwatch.Frequency);
            var untilDeadline = remaining - Stopwatch.GetElapsedTime(anchor);

            // A deadline already reached (including one that ran out between
            // reading RemainingTime and getting here) fires at once.
            if (!armTimer)
            {
                // Test seam: nothing fires the deadline on its own.
            }
            else if (untilDeadline > TimeSpan.Zero)
                _deadlineTimer = new Timer(static state => ((WorkflowCancellation)state!).TriggerDeadline(), this, untilDeadline, Timeout.InfiniteTimeSpan);
            else
                TriggerDeadline();
        }
    }

    /// <summary>
    /// Marks the invocation as torn down and cancels the token. Returns without
    /// running user cancellation callbacks: they run on the threadpool, so one
    /// that blocks cannot stall the wrapper before its bounded drain.
    /// </summary>
    public void BeginTeardown()
    {
        Volatile.Write(ref _tornDown, 1);
        Cancel(WorkflowCancellationReason.Teardown);
    }

    /// <summary>
    /// Throws <see cref="OperationCanceledException"/> when the invocation is
    /// torn down, so abandoned user code cannot start new durable operations.
    /// </summary>
    public void ThrowIfTornDown()
    {
        if (IsTornDown)
            throw new OperationCanceledException(
                "The durable invocation has ended; no new durable operations can be started.", _token);
    }

    /// <summary>
    /// Throws <see cref="OperationCanceledException"/> when the invocation
    /// deadline has passed, so a timed-out invocation records no outcome.
    /// </summary>
    public void ThrowIfDeadlineExceeded(string message)
    {
        if (IsDeadlineExceeded)
            throw new OperationCanceledException(message, _token);
    }

    /// <summary>
    /// Fires the deadline now: by the timer, by an observer that saw the clock
    /// pass it, or by a test. Idempotent. Runs no foreign code on the caller's
    /// thread, so it is safe to call from anywhere.
    /// </summary>
    internal void TriggerDeadline()
    {
        if (_deadlineCts is null) return;
        if (Interlocked.Exchange(ref _deadlineFired, 1) != 0) return;

        // The SDK's own state first: record why and close admission.
        Interlocked.CompareExchange(ref _reason, (int)WorkflowCancellationReason.DeadlineExceeded, 0);
        Volatile.Write(ref _tornDown, 1);

        // Then start cancelling the workflow token and the client's deadline
        // token. Both only queue their callbacks, so no foreign code runs here.
        // This must happen BEFORE DeadlineTask is signalled: a waiter on
        // DeadlineTask (the hydration wait) can end the invocation and reach
        // Dispose at once, and Dispose must find the deadline cancel already
        // under way, not dispose a source whose cancel has not started.
        Cancel(WorkflowCancellationReason.DeadlineExceeded);
        StartCancelOnce(ref _deadlineDelivery, _deadlineCts,
            "A cancellation callback registered on the durable invocation deadline threw.");

        // Last, signal the handler. _deadlineTcs runs its continuations
        // asynchronously, so this never runs the handler on the calling thread.
        _deadlineTcs.TrySetResult();
        AfterDeadlineSignalledForTest?.Invoke();
    }

    /// <summary>
    /// Test seam: runs right after <see cref="DeadlineTask"/> is signalled, on the
    /// thread firing the deadline. Lets a test act (for example dispose) at the
    /// earliest point a waiter on <see cref="DeadlineTask"/> could.
    /// </summary>
    internal Action? AfterDeadlineSignalledForTest { get; set; }

    private void Cancel(WorkflowCancellationReason reason)
    {
        Interlocked.CompareExchange(ref _reason, (int)reason, 0);
        StartCancelOnce(ref _cancelDelivery, _cts,
            "A cancellation callback registered by the durable workflow threw while the workflow was being cancelled.");
    }

    /// <summary>
    /// Starts cancelling <paramref name="source"/> exactly once, and records the
    /// delivery so <see cref="Dispose"/> can wait for it.
    /// </summary>
    /// <remarks>
    /// The lock makes every caller return only after the source reports
    /// <c>IsCancellationRequested</c>: a caller that loses the race waits for the
    /// winner to start cancelling instead of returning early. It is held only
    /// while <see cref="CancellationTokenSource.CancelAsync"/> flips the state and
    /// queues the callbacks, so no foreign code ever runs under it.
    /// </remarks>
    private void StartCancelOnce(ref Task? deliverySlot, CancellationTokenSource source, string callbackFailureMessage)
    {
        lock (_cancelLock)
        {
            // Once Dispose has taken the delivery handles, the sources may be
            // disposed; a cancel that had not started by then never starts.
            if (deliverySlot != null || _sourcesReleased) return;
            Volatile.Write(ref deliverySlot, StartCancel(source, callbackFailureMessage));
        }
    }

    /// <summary>
    /// Cancels <paramref name="source"/> without running its callbacks on this
    /// thread. The returned task completes when every callback has run; callback
    /// exceptions are logged, never rethrown.
    /// </summary>
    private static Task StartCancel(CancellationTokenSource source, string callbackFailureMessage)
    {
        Task delivery;
        try
        {
            delivery = source.CancelAsync();
        }
        catch (ObjectDisposedException)
        {
            // Only reachable after disposal, which happens once delivery finished,
            // so the source was already cancelled.
            return Task.CompletedTask;
        }

        return delivery.ContinueWith(
            static (t, message) =>
            {
                if (t.Exception is { } ex)
                    LambdaLogger.Log("Warning", ex.Flatten(), (string)message!);
            },
            callbackFailureMessage,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        BeginTeardown();
        _deadlineTimer?.Dispose();

        // Dispose each source only after its callbacks have all run. Disposing
        // earlier could skip callbacks that have not run yet, so linked tokens
        // (and the user code behind them) would never be cancelled. A callback
        // that never returns just leaves its source undisposed. Taking the
        // delivery handles and closing out new cancels happen together under the
        // lock: a cancel is either already under way (and is waited for) or never
        // starts, so a source is never disposed while its cancel is starting.
        Task workflowDelivery;
        Task? deadlineDelivery;
        lock (_cancelLock)
        {
            _sourcesReleased = true;
            workflowDelivery = _cancelDelivery ?? Task.CompletedTask;
            deadlineDelivery = _deadlineDelivery;
        }

        DisposeAfter(_cts, workflowDelivery);
        if (_deadlineCts != null)
        {
            // A deadline that never fired was never cancelled: dispose it now. A
            // timer callback still in flight finds _sourcesReleased set and does
            // not touch the source.
            DisposeAfter(_deadlineCts, deadlineDelivery ?? Task.CompletedTask);
        }
    }

    private static void DisposeAfter(CancellationTokenSource source, Task delivery)
    {
        if (delivery.IsCompleted)
            source.Dispose();
        else
            delivery.ContinueWith(
                static (_, s) => ((CancellationTokenSource)s!).Dispose(),
                source,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
    }
}

/// <summary>
/// Decides whether an invocation gets a deadline that cancels durable work.
/// </summary>
/// <remarks>
/// Only Lambda Managed Instances get a deadline. There, several invocations
/// share one process and Lambda does not stop code when an invocation times out,
/// so the SDK has to stop its own work. In on-demand Lambda the execution
/// environment is stopped at the timeout, so the SDK keeps its existing
/// behavior and never cancels on a deadline.
/// </remarks>
internal static class InvocationDeadline
{
    /// <summary>
    /// Environment variable Lambda sets for functions running on Lambda Managed
    /// Instances (multi-concurrency mode).
    /// </summary>
    internal const string MaxConcurrencyEnvironmentVariable = "AWS_LAMBDA_MAX_CONCURRENCY";

    /// <summary>True when the function runs on Lambda Managed Instances.</summary>
    public static bool IsMultiConcurrency() =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(MaxConcurrencyEnvironmentVariable));

    /// <summary>
    /// Returns the time until the deadline, <see cref="TimeSpan.Zero"/> when the
    /// deadline has already passed, or <c>null</c> for no deadline.
    /// </summary>
    /// <param name="remainingTime">The invocation's <c>ILambdaContext.RemainingTime</c>.</param>
    /// <param name="isMultiConcurrency">Whether the function runs on Lambda Managed Instances.</param>
    /// <remarks>
    /// On Lambda Managed Instances a zero or negative remaining time means the
    /// invocation was already out of time when it started, so it times out
    /// immediately. Tests that exercise this mode must set
    /// <c>TestLambdaContext.RemainingTime</c>.
    /// </remarks>
    public static TimeSpan? Resolve(TimeSpan remainingTime, bool isMultiConcurrency)
    {
        if (!isMultiConcurrency) return null;
        if (remainingTime <= TimeSpan.Zero) return TimeSpan.Zero;

        // Timer rejects delays above int.MaxValue milliseconds. Lambda timeouts
        // are far below that; treat anything larger as no deadline.
        if (remainingTime.TotalMilliseconds >= int.MaxValue) return null;
        return remainingTime;
    }
}
