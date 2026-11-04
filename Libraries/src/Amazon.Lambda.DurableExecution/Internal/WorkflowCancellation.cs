// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

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
/// Lifetime rules that matter when several invocations share one process
/// (Lambda Managed Instances):
/// </para>
/// <list type="bullet">
///   <item><see cref="BeginTeardown"/> and <see cref="Dispose"/> cancel the token
///       synchronously before releasing anything. Termination delivers its cancel
///       through a threadpool continuation, which could otherwise run after
///       disposal and be lost.</item>
///   <item><see cref="Token"/> is captured at construction, so abandoned user code
///       that reads it after disposal gets a cancelled token instead of an
///       <see cref="ObjectDisposedException"/>.</item>
/// </list>
/// </remarks>
internal sealed class WorkflowCancellation : IDisposable
{
    private readonly CancellationTokenSource _cts = new();
    private readonly CancellationToken _token;
    private readonly CancellationTokenSource? _deadlineCts;
    private readonly CancellationTokenRegistration _deadlineRegistration;
    private readonly TaskCompletionSource _deadlineTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _reason;
    private int _tornDown;
    private int _disposed;

    public CancellationToken Token => _token;

    /// <summary>
    /// The reason the token was cancelled, or <see cref="WorkflowCancellationReason.None"/>.
    /// </summary>
    public WorkflowCancellationReason Reason => (WorkflowCancellationReason)Volatile.Read(ref _reason);

    /// <summary>
    /// True once the invocation deadline passed. Read from the deadline token
    /// itself, so it is accurate even when a suspension or teardown cancelled the
    /// workflow first, and becomes true before any cancellation callback runs.
    /// </summary>
    public bool IsDeadlineExceeded => DeadlineToken.IsCancellationRequested;

    /// <summary>
    /// True once the invocation is being torn down (the wrapper finished or the
    /// deadline passed). New durable operations are rejected from this point.
    /// </summary>
    public bool IsTornDown => Volatile.Read(ref _tornDown) == 1;

    /// <summary>
    /// Cancels only when the invocation deadline passes. Passed to SDK-owned
    /// service calls (state hydration). <see cref="CancellationToken.None"/> when
    /// there is no deadline, so on-demand Lambda calls are unchanged.
    /// </summary>
    public CancellationToken DeadlineToken { get; }

    /// <summary>
    /// Completes when the invocation deadline passes. Never completes when there
    /// is no deadline.
    /// </summary>
    public Task DeadlineTask => _deadlineTcs.Task;

    /// <param name="terminationManager">Suspension signal for this invocation.</param>
    /// <param name="deadline">
    /// Time until the invocation deadline, or <c>null</c> for no deadline. Only
    /// set for Lambda Managed Instances; see <see cref="InvocationDeadline"/>.
    /// </param>
    public WorkflowCancellation(TerminationManager terminationManager, TimeSpan? deadline = null)
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
            // A zero deadline means the invocation was already out of time when
            // it started: cancel at once rather than through the timer.
            _deadlineCts = remaining > TimeSpan.Zero
                ? new CancellationTokenSource(remaining)
                : new CancellationTokenSource();
            if (remaining <= TimeSpan.Zero) _deadlineCts.Cancel();
            DeadlineToken = _deadlineCts.Token;
            _deadlineRegistration = _deadlineCts.Token.Register(
                static state => ((WorkflowCancellation)state!).OnDeadline(),
                this);
        }
    }

    /// <summary>
    /// Marks the invocation as torn down and cancels the token synchronously,
    /// so the cancel is delivered before the caller releases any resources.
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

    private void OnDeadline()
    {
        // Stop admitting new operations and signal the deadline BEFORE cancelling
        // the token. Cancel runs user cancellation callbacks synchronously; a
        // callback that blocks must not stop DurableExecutionHandler from seeing
        // the deadline. _deadlineTcs runs its continuations asynchronously, so
        // signalling it never runs the handler on this timer thread.
        Interlocked.CompareExchange(ref _reason, (int)WorkflowCancellationReason.DeadlineExceeded, 0);
        Volatile.Write(ref _tornDown, 1);
        _deadlineTcs.TrySetResult();
        Cancel(WorkflowCancellationReason.DeadlineExceeded);
    }

    private void Cancel(WorkflowCancellationReason reason)
    {
        Interlocked.CompareExchange(ref _reason, (int)reason, 0);
        try { _cts.Cancel(); }
        catch (ObjectDisposedException)
        {
            // Only reachable from the late termination continuation or deadline
            // timer. Dispose cancels before disposing, so the token is already
            // cancelled.
        }
        catch (AggregateException ex)
        {
            // A user cancellation callback threw. Cancel still ran every other
            // callback before throwing, so cancellation is fully delivered. Never
            // let the exception escape: on the deadline timer it would crash the
            // process (and every invocation sharing it), and during teardown it
            // would replace the invocation's real outcome.
            LambdaLogger.Log(
                "Warning", ex,
                "A cancellation callback registered by the durable workflow threw while the workflow was being cancelled.");
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        BeginTeardown();

        // Unregister rather than Dispose: Dispose waits for a deadline callback
        // that is still running, and a user cancellation callback that blocks
        // would then hang the invocation here.
        _deadlineRegistration.Unregister();
        _deadlineCts?.Dispose();
        _cts.Dispose();
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

        // CancellationTokenSource rejects delays above int.MaxValue milliseconds.
        // Lambda timeouts are far below that; treat anything larger as no deadline.
        if (remainingTime.TotalMilliseconds >= int.MaxValue) return null;
        return remainingTime;
    }
}
