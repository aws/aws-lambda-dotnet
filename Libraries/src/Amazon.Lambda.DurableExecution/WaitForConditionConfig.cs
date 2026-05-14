namespace Amazon.Lambda.DurableExecution;

/// <summary>
/// Configuration for a <c>WaitForConditionAsync</c> polling operation.
/// </summary>
/// <remarks>
/// Both properties are required: the strategy decides "continue or stop"
/// (per-call) and the initial state seeds the very first check invocation.
/// On replay, the latest checkpointed state is restored from the previous
/// RETRY checkpoint and used in place of <see cref="InitialState"/>; this
/// is what makes the polling loop survive Lambda re-invocations
/// deterministically.
/// </remarks>
/// <typeparam name="TState">The state type produced by the check function.</typeparam>
public sealed class WaitForConditionConfig<TState>
{
    /// <summary>
    /// Initial state passed to the very first invocation of the check
    /// function. Subsequent invocations receive the state returned by the
    /// previous call.
    /// </summary>
    public required TState InitialState { get; set; }

    /// <summary>
    /// Strategy that decides, after each check invocation, whether to keep
    /// polling and how long to wait before the next attempt.
    /// </summary>
    public required IWaitStrategy<TState> WaitStrategy { get; set; }
}
