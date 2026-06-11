using Amazon.Lambda.DurableExecution.Internal;

namespace Amazon.Lambda.DurableExecution;

/// <summary>
/// Factory methods for built-in <see cref="IWaitStrategy{TState}"/>
/// implementations used with
/// <c>IDurableContext.WaitForConditionAsync</c>.
/// </summary>
/// <remarks>
/// Each factory accepts an optional <c>isDone</c> predicate so users can
/// terminate polling declaratively when the latest state satisfies a
/// condition (e.g. <c>state =&gt; state.IsReady</c>) without implementing
/// <see cref="IWaitStrategy{TState}"/> themselves. If <c>isDone</c> is
/// <c>null</c>, the strategy polls until <c>maxAttempts</c> is exhausted —
/// at which point a <see cref="WaitForConditionException"/> is thrown by
/// the operation. Defaults are intentionally tuned for polling, not for
/// retry-on-exception: 60 attempts / 5s initial / 300s max / 1.5x backoff /
/// Full jitter (matches Python+JS+Java reference SDKs).
/// </remarks>
public static class WaitStrategy
{
    /// <summary>
    /// Exponential-backoff wait strategy. Defaults: 60 attempts, 5s initial
    /// delay, 5min (300s) max delay, 1.5x backoff, Full jitter — matching
    /// the Python, JS, and Java reference SDKs.
    /// </summary>
    /// <param name="maxAttempts">Maximum polling attempts before the operation throws <see cref="WaitForConditionException"/>.</param>
    /// <param name="initialDelay">Delay before the second attempt; subsequent delays multiply by <paramref name="backoffRate"/> up to <paramref name="maxDelay"/>.</param>
    /// <param name="maxDelay">Cap on the per-attempt delay.</param>
    /// <param name="backoffRate">Multiplier applied per attempt.</param>
    /// <param name="jitter">Jitter strategy applied to each delay.</param>
    /// <param name="isDone">Optional predicate evaluated against the latest state; when it returns <c>true</c>, polling stops and the state is returned.</param>
    public static IWaitStrategy<TState> Exponential<TState>(
        int maxAttempts = 60,
        TimeSpan? initialDelay = null,
        TimeSpan? maxDelay = null,
        double backoffRate = 1.5,
        JitterStrategy jitter = JitterStrategy.Full,
        Func<TState, bool>? isDone = null)
    {
        return new ExponentialWaitStrategy<TState>(
            maxAttempts,
            initialDelay ?? TimeSpan.FromSeconds(5),
            maxDelay ?? TimeSpan.FromSeconds(300),
            backoffRate,
            jitter,
            isDone);
    }

    /// <summary>
    /// Linear-growth wait strategy. The delay starts at
    /// <paramref name="initialDelay"/> and grows by
    /// <paramref name="increment"/> each attempt, up to
    /// <paramref name="maxDelay"/>.
    /// </summary>
    /// <param name="maxAttempts">Maximum polling attempts before the operation throws <see cref="WaitForConditionException"/>.</param>
    /// <param name="initialDelay">Delay before the second attempt.</param>
    /// <param name="increment">Amount added to the delay on each subsequent attempt.</param>
    /// <param name="maxDelay">Cap on the per-attempt delay; <c>null</c> means no cap.</param>
    /// <param name="isDone">Optional predicate evaluated against the latest state; when it returns <c>true</c>, polling stops and the state is returned.</param>
    public static IWaitStrategy<TState> Linear<TState>(
        int maxAttempts = 60,
        TimeSpan? initialDelay = null,
        TimeSpan? increment = null,
        TimeSpan? maxDelay = null,
        Func<TState, bool>? isDone = null)
    {
        return new LinearWaitStrategy<TState>(
            maxAttempts,
            initialDelay ?? TimeSpan.FromSeconds(5),
            increment ?? TimeSpan.FromSeconds(5),
            maxDelay,
            isDone);
    }

    /// <summary>
    /// Fixed-delay wait strategy. Every poll waits the same
    /// <paramref name="delay"/>.
    /// </summary>
    /// <param name="delay">Fixed delay between polls.</param>
    /// <param name="maxAttempts">Maximum polling attempts before the operation throws <see cref="WaitForConditionException"/>.</param>
    /// <param name="isDone">Optional predicate evaluated against the latest state; when it returns <c>true</c>, polling stops and the state is returned.</param>
    public static IWaitStrategy<TState> Fixed<TState>(
        TimeSpan delay,
        int maxAttempts = 60,
        Func<TState, bool>? isDone = null)
    {
        return new FixedWaitStrategy<TState>(maxAttempts, delay, isDone);
    }

    /// <summary>
    /// Wraps an arbitrary delegate as an <see cref="IWaitStrategy{TState}"/>.
    /// </summary>
    public static IWaitStrategy<TState> FromDelegate<TState>(Func<TState, int, WaitDecision> strategy)
        => new DelegateWaitStrategy<TState>(strategy);
}

internal sealed class ExponentialWaitStrategy<TState> : IWaitStrategy<TState>
{
    private readonly int _maxAttempts;
    private readonly TimeSpan _initialDelay;
    private readonly TimeSpan _maxDelay;
    private readonly double _backoffRate;
    private readonly JitterStrategy _jitter;
    private readonly Func<TState, bool>? _isDone;

    public ExponentialWaitStrategy(
        int maxAttempts,
        TimeSpan initialDelay,
        TimeSpan maxDelay,
        double backoffRate,
        JitterStrategy jitter,
        Func<TState, bool>? isDone)
    {
        _maxAttempts = maxAttempts;
        _initialDelay = initialDelay;
        _maxDelay = maxDelay;
        _backoffRate = backoffRate;
        _jitter = jitter;
        _isDone = isDone;
    }

    public WaitDecision Decide(TState state, int attemptNumber)
    {
        // Predicate satisfied → stop normally (operation SUCCEEDs).
        if (_isDone != null && _isDone(state)) return WaitDecision.Stop();

        // Attempts saturated → throw WaitForConditionException directly.
        // Matches the JS reference SDK (wait-strategy-config.ts:54-57); lets
        // the operation distinguish "condition met" (Stop) from "gave up"
        // (exception) without a discriminator on WaitDecision. The operation
        // catches, populates LastState (which the strategy doesn't have
        // access to), checkpoints FAIL, and rethrows.
        if (attemptNumber >= _maxAttempts)
            throw new WaitForConditionException(
                $"WaitForCondition exceeded maximum attempts ({_maxAttempts}).")
            {
                AttemptsExhausted = attemptNumber
            };

        var delay = ExponentialBackoff.CalculateDelay(
            attemptNumber, _initialDelay, _maxDelay, _backoffRate, _jitter);
        return WaitDecision.ContinueAfter(delay);
    }
}

internal sealed class LinearWaitStrategy<TState> : IWaitStrategy<TState>
{
    private readonly int _maxAttempts;
    private readonly TimeSpan _initialDelay;
    private readonly TimeSpan _increment;
    private readonly TimeSpan? _maxDelay;
    private readonly Func<TState, bool>? _isDone;

    public LinearWaitStrategy(
        int maxAttempts,
        TimeSpan initialDelay,
        TimeSpan increment,
        TimeSpan? maxDelay,
        Func<TState, bool>? isDone)
    {
        _maxAttempts = maxAttempts;
        _initialDelay = initialDelay;
        _increment = increment;
        _maxDelay = maxDelay;
        _isDone = isDone;
    }

    public WaitDecision Decide(TState state, int attemptNumber)
    {
        if (_isDone != null && _isDone(state)) return WaitDecision.Stop();
        if (attemptNumber >= _maxAttempts)
            throw new WaitForConditionException(
                $"WaitForCondition exceeded maximum attempts ({_maxAttempts}).")
            {
                AttemptsExhausted = attemptNumber
            };

        var rawSeconds = _initialDelay.TotalSeconds + _increment.TotalSeconds * (attemptNumber - 1);
        if (_maxDelay is { } cap) rawSeconds = Math.Min(rawSeconds, cap.TotalSeconds);

        // Floor at 1 second to match the service timer granularity.
        var seconds = Math.Max(1, Math.Ceiling(rawSeconds));
        return WaitDecision.ContinueAfter(TimeSpan.FromSeconds(seconds));
    }
}

internal sealed class FixedWaitStrategy<TState> : IWaitStrategy<TState>
{
    private readonly int _maxAttempts;
    private readonly TimeSpan _delay;
    private readonly Func<TState, bool>? _isDone;

    public FixedWaitStrategy(int maxAttempts, TimeSpan delay, Func<TState, bool>? isDone)
    {
        _maxAttempts = maxAttempts;
        _delay = delay;
        _isDone = isDone;
    }

    public WaitDecision Decide(TState state, int attemptNumber)
    {
        if (_isDone != null && _isDone(state)) return WaitDecision.Stop();
        if (attemptNumber >= _maxAttempts)
            throw new WaitForConditionException(
                $"WaitForCondition exceeded maximum attempts ({_maxAttempts}).")
            {
                AttemptsExhausted = attemptNumber
            };

        var seconds = Math.Max(1, Math.Ceiling(_delay.TotalSeconds));
        return WaitDecision.ContinueAfter(TimeSpan.FromSeconds(seconds));
    }
}

internal sealed class DelegateWaitStrategy<TState> : IWaitStrategy<TState>
{
    private readonly Func<TState, int, WaitDecision> _strategy;
    public DelegateWaitStrategy(Func<TState, int, WaitDecision> strategy) => _strategy = strategy;
    public WaitDecision Decide(TState state, int attemptNumber) => _strategy(state, attemptNumber);
}
