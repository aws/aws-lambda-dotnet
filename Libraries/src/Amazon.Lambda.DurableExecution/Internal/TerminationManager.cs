namespace Amazon.Lambda.DurableExecution.Internal;

/// <summary>
/// The reason the execution was terminated.
/// </summary>
internal enum TerminationReason
{
    WaitScheduled,
    RetryScheduled,
    CallbackPending,
    InvokePending,
    CheckpointFailed
}

/// <summary>
/// The result of a termination signal.
/// </summary>
internal sealed class TerminationResult
{
    public required TerminationReason Reason { get; init; }
    public string? Message { get; init; }
    public Exception? Exception { get; init; }
}

/// <summary>
/// Manages the suspension signal for durable execution.
/// Uses a TaskCompletionSource that resolves when the function should suspend.
/// Only the first Terminate() call wins; subsequent calls are ignored.
/// </summary>
internal sealed class TerminationManager
{
    private readonly TaskCompletionSource<TerminationResult> _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _terminated;

    /// <summary>
    /// A Task that resolves when Terminate() is called. Used in Task.WhenAny
    /// to race against user code.
    /// </summary>
    public Task<TerminationResult> TerminationTask => _tcs.Task;

    /// <summary>
    /// Whether Terminate() has been called.
    /// </summary>
    public bool IsTerminated => Volatile.Read(ref _terminated) == 1;

    /// <summary>
    /// Signals that the execution should suspend. Thread-safe; only the first
    /// call has effect.
    /// </summary>
    /// <returns>true if this call triggered termination, false if already terminated.</returns>
    public bool Terminate(TerminationReason reason, string? message = null, Exception? exception = null)
    {
        if (Interlocked.CompareExchange(ref _terminated, 1, 0) != 0)
            return false;

        _tcs.TrySetResult(new TerminationResult
        {
            Reason = reason,
            Message = message,
            Exception = exception
        });

        return true;
    }

    /// <summary>
    /// Trips the termination signal and returns a Task that never completes.
    /// This is the standard suspension idiom: the caller awaits the returned
    /// Task, and <see cref="DurableExecutionHandler"/>'s <c>Task.WhenAny</c>
    /// race picks up <see cref="TerminationTask"/> instead, returning Pending
    /// to the service. The returned Task is abandoned and GC'd.
    /// </summary>
    public Task<T> SuspendAndAwait<T>(TerminationReason reason, string? message = null, Exception? exception = null)
    {
        Terminate(reason, message, exception);
        return new TaskCompletionSource<T>().Task;
    }
}
