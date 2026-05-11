using Amazon.Lambda.DurableExecution.Internal;

namespace Amazon.Lambda.DurableExecution;

/// <summary>
/// The result of running a durable execution handler.
/// </summary>
internal sealed class HandlerResult<TResult>
{
    public required InvocationStatus Status { get; init; }
    public TResult? Result { get; init; }
    public string? Message { get; init; }
    public Exception? Exception { get; init; }
}

/// <summary>
/// Core orchestration engine for durable execution. Races user code against
/// a termination signal using Task.WhenAny. When user code completes, returns
/// SUCCEEDED/FAILED. When termination wins (wait, callback, invoke), returns PENDING.
/// </summary>
internal static class DurableExecutionHandler
{
    /// <summary>
    /// Runs the user's workflow function within the durable execution engine.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Suspension flow — example: <c>await ctx.WaitAsync(TimeSpan.FromSeconds(5))</c>:
    /// </para>
    /// <code>
    ///   user code            DurableContext       TerminationMgr      RunAsync
    ///   ─────────            ──────────────       ──────────────      ────────
    ///   WaitAsync(5s) ─────► queue WAIT START
    ///                        checkpoint
    ///                        Terminate() ──────► TerminationTask
    ///                                             completes
    ///                ◄────── new TCS().Task
    ///                        (never completes)
    ///   await blocks
    ///   forever                                                       WhenAny:
    ///                                                              ── termination wins
    ///                                                              ── userTask abandoned
    ///                                                              ── return Pending
    /// </code>
    /// <para>
    /// Key insight: <c>WaitAsync</c> never returns a completed Task — it hands back
    /// a TaskCompletionSource that is never resolved. The user's <c>await</c> blocks
    /// indefinitely. The escape signal is <c>terminationManager.Terminate()</c>,
    /// which <c>Task.WhenAny</c> picks up. We return Pending; the dangling user
    /// Task is GC'd. The service flushes checkpoints, fires the wait timer, then
    /// re-invokes Lambda — on replay, <c>WaitAsync</c> sees the matching SUCCEED
    /// checkpoint and returns <c>Task.CompletedTask</c> normally.
    /// </para>
    /// <para>
    /// The same pattern applies to retries (<c>RetryScheduled</c>), callbacks
    /// (<c>CallbackPending</c>), and chained invokes (<c>InvokePending</c>).
    /// </para>
    /// </remarks>
    /// <typeparam name="TResult">The workflow return type.</typeparam>
    /// <param name="executionState">Hydrated execution state from prior invocations.</param>
    /// <param name="terminationManager">Manages the suspension signal.</param>
    /// <param name="userHandler">The user's workflow function receiving a DurableContext.</param>
    /// <returns>The handler result indicating SUCCEEDED, FAILED, or PENDING.</returns>
    internal static async Task<HandlerResult<TResult>> RunAsync<TResult>(
        ExecutionState executionState,
        TerminationManager terminationManager,
        Func<Task<TResult>> userHandler)
    {
        // Run user code on a threadpool thread so it executes independently of
        // the termination signal. When TerminationManager fires (e.g., WaitAsync),
        // we need the WhenAny race below to resolve immediately without waiting
        // for the user task to reach an await point.
        var userTask = Task.Run(userHandler);

        // Race: user code completing vs. termination signal (wait/callback/retry).
        // If termination wins, we return PENDING and the abandoned userTask is never awaited.
        var winner = await Task.WhenAny(userTask, terminationManager.TerminationTask);

        if (winner == terminationManager.TerminationTask)
        {
            var terminationResult = await terminationManager.TerminationTask;

            if (terminationResult.Exception != null)
            {
                return new HandlerResult<TResult>
                {
                    Status = InvocationStatus.Failed,
                    Message = terminationResult.Exception.Message,
                    Exception = terminationResult.Exception
                };
            }

            return new HandlerResult<TResult>
            {
                Status = InvocationStatus.Pending,
                Message = terminationResult.Message
            };
        }

        try
        {
            var result = await userTask;
            return new HandlerResult<TResult>
            {
                Status = InvocationStatus.Succeeded,
                Result = result
            };
        }
        catch (Exception ex)
        {
            return new HandlerResult<TResult>
            {
                Status = InvocationStatus.Failed,
                Message = ex.Message,
                Exception = ex
            };
        }
    }
}
