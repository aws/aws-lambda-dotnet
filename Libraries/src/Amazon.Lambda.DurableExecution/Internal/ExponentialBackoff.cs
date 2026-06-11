namespace Amazon.Lambda.DurableExecution.Internal;

/// <summary>
/// Shared exponential-backoff math for both
/// <see cref="ExponentialRetryStrategy"/> (retry-on-exception) and
/// <c>ExponentialWaitStrategy&lt;TState&gt;</c> (wait-for-condition polling).
/// Computes <c>min(initialDelay * backoff^(attempt-1), maxDelay)</c>, applies
/// the requested jitter, then ceilings to whole seconds with a 1-second floor
/// (the service timer's smallest unit).
/// </summary>
internal static class ExponentialBackoff
{
    [ThreadStatic]
    private static Random? t_random;
    private static Random Random => t_random ??= new Random();

    /// <summary>
    /// Computes the delay for the given <paramref name="attemptNumber"/> (1-based)
    /// using exponential backoff with the requested jitter strategy. Returned
    /// delay is always at least 1 second (service timer floor).
    /// </summary>
    public static TimeSpan CalculateDelay(
        int attemptNumber,
        TimeSpan initialDelay,
        TimeSpan maxDelay,
        double backoffRate,
        JitterStrategy jitter)
    {
        var baseDelay = initialDelay.TotalSeconds * Math.Pow(backoffRate, attemptNumber - 1);
        var cappedDelay = Math.Min(baseDelay, maxDelay.TotalSeconds);

        var finalDelay = jitter switch
        {
            JitterStrategy.Full => Random.NextDouble() * cappedDelay,
            JitterStrategy.Half => cappedDelay * (0.5 + 0.5 * Random.NextDouble()),
            _ => cappedDelay
        };

        return TimeSpan.FromSeconds(Math.Max(1, Math.Ceiling(finalDelay)));
    }
}
