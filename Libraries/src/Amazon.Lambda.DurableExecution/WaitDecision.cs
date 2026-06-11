namespace Amazon.Lambda.DurableExecution;

/// <summary>
/// Decision returned by an <see cref="IWaitStrategy{TState}"/> on each polling
/// iteration: either stop polling (the condition has been met or attempts
/// have been exhausted) or continue after the given delay.
/// </summary>
public readonly record struct WaitDecision
{
    /// <summary>
    /// True when the strategy wants the operation to keep polling; false when
    /// the operation should terminate (condition satisfied or limit reached).
    /// </summary>
    public bool ShouldContinue { get; }

    /// <summary>
    /// Delay before the next poll. Only meaningful when
    /// <see cref="ShouldContinue"/> is <c>true</c>; otherwise
    /// <see cref="TimeSpan.Zero"/>. The wire-level timer floors this at 1
    /// second.
    /// </summary>
    public TimeSpan Delay { get; }

    private WaitDecision(bool shouldContinue, TimeSpan delay)
    {
        ShouldContinue = shouldContinue;
        Delay = delay;
    }

    /// <summary>
    /// Stop polling. The current state is treated as the final result of the
    /// wait-for-condition operation and returned to the caller.
    /// </summary>
    public static WaitDecision Stop() => new(false, TimeSpan.Zero);

    /// <summary>
    /// Continue polling after the given delay. The Lambda is suspended until
    /// the delay elapses, at which point the service re-invokes and the
    /// condition is re-evaluated.
    /// </summary>
    public static WaitDecision ContinueAfter(TimeSpan delay) => new(true, delay);
}
