namespace Amazon.Lambda.DurableExecution;

/// <summary>
/// Decides, per polling iteration, whether a <c>WaitForConditionAsync</c>
/// operation should keep polling and how long to wait before the next attempt.
/// </summary>
/// <remarks>
/// Distinct from <see cref="IRetryStrategy"/>: that interface decides
/// retry-on-exception (input is the thrown <see cref="Exception"/>); this one
/// decides poll-until-condition (input is the latest <typeparamref name="TState"/>
/// observed by the check function). Implementations are typically obtained
/// via the <see cref="WaitStrategy"/> factory; users who need richer logic
/// (e.g. wall-clock-time budgets, conditional jitter) can implement this
/// interface directly.
/// </remarks>
/// <typeparam name="TState">The state type produced by the check function.</typeparam>
public interface IWaitStrategy<TState>
{
    /// <summary>
    /// Evaluates the latest <paramref name="state"/> from the check function
    /// and the 1-based <paramref name="attemptNumber"/> just executed, and
    /// returns either <see cref="WaitDecision.Stop"/> (terminate) or
    /// <see cref="WaitDecision.ContinueAfter(TimeSpan)"/> (poll again after
    /// the given delay).
    /// </summary>
    WaitDecision Decide(TState state, int attemptNumber);
}
