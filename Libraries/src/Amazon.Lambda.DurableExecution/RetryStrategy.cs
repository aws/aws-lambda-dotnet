// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using Amazon.Lambda.DurableExecution.Internal;

namespace Amazon.Lambda.DurableExecution;

/// <summary>
/// Jitter strategy for exponential backoff to prevent thundering-herd scenarios.
/// </summary>
public enum JitterStrategy
{
    /// <summary>No randomization — delay is exactly the calculated backoff value.</summary>
    None,
    /// <summary>Random delay between 0 and the calculated backoff value (recommended).</summary>
    Full,
    /// <summary>Random delay between 50% and 100% of the calculated backoff value.</summary>
    Half
}

/// <summary>
/// Controls whether a step re-executes if the Lambda is re-invoked mid-attempt.
/// </summary>
public enum StepSemantics
{
    /// <summary>
    /// Default. The step may re-execute if the Lambda is re-invoked during execution.
    /// Use for idempotent operations.
    /// </summary>
    AtLeastOncePerRetry,

    /// <summary>
    /// The step executes at most once per retry attempt. A START checkpoint is written
    /// before execution; on replay with an existing START, the SDK skips re-execution
    /// and proceeds to the retry handler.
    /// </summary>
    AtMostOncePerRetry
}

/// <summary>
/// Factory methods for common retry strategies.
/// </summary>
public static class RetryStrategy
{
    /// <summary>6 attempts, 2x backoff, 5s initial delay, 60s max, Full jitter.</summary>
    public static IRetryStrategy Default { get; } = Exponential(
        maxAttempts: 6,
        initialDelay: TimeSpan.FromSeconds(5),
        maxDelay: TimeSpan.FromSeconds(60),
        backoffRate: 2.0,
        jitter: JitterStrategy.Full);

    /// <summary>3 attempts, 2x backoff, 1s initial delay, 5s max, Half jitter.</summary>
    public static IRetryStrategy Transient { get; } = Exponential(
        maxAttempts: 3,
        initialDelay: TimeSpan.FromSeconds(1),
        maxDelay: TimeSpan.FromSeconds(5),
        backoffRate: 2.0,
        jitter: JitterStrategy.Half);

    /// <summary>No retry — 1 attempt only.</summary>
    public static IRetryStrategy None { get; } = Exponential(maxAttempts: 1);

    /// <summary>
    /// Creates an exponential backoff retry strategy.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown if <paramref name="maxAttempts"/> &lt; 1, <paramref name="backoffRate"/> &lt; 1,
    /// <paramref name="initialDelay"/> is non-positive, <paramref name="maxDelay"/> is non-positive,
    /// or <paramref name="initialDelay"/> &gt; <paramref name="maxDelay"/>.
    /// </exception>
    public static IRetryStrategy Exponential(
        int maxAttempts = 3,
        TimeSpan? initialDelay = null,
        TimeSpan? maxDelay = null,
        double backoffRate = 2.0,
        JitterStrategy jitter = JitterStrategy.Full,
        Type[]? retryableExceptions = null,
        string[]? retryableMessagePatterns = null)
    {
        return new ExponentialRetryStrategy(
            maxAttempts,
            initialDelay ?? TimeSpan.FromSeconds(5),
            maxDelay ?? TimeSpan.FromSeconds(300),
            backoffRate,
            jitter,
            retryableExceptions,
            retryableMessagePatterns);
    }

    /// <summary>
    /// Creates a retry strategy from a delegate.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="strategy"/> is null.</exception>
    public static IRetryStrategy FromDelegate(Func<Exception, int, RetryDecision> strategy)
    {
        if (strategy == null) throw new ArgumentNullException(nameof(strategy));
        return new DelegateRetryStrategy(strategy);
    }
}

internal sealed class ExponentialRetryStrategy : IRetryStrategy
{
    private readonly int _maxAttempts;
    private readonly TimeSpan _initialDelay;
    private readonly TimeSpan _maxDelay;
    private readonly double _backoffRate;
    private readonly JitterStrategy _jitter;
    private readonly Type[]? _retryableExceptions;
    private readonly Regex[]? _retryableMessagePatterns;

    public ExponentialRetryStrategy(
        int maxAttempts,
        TimeSpan initialDelay,
        TimeSpan maxDelay,
        double backoffRate,
        JitterStrategy jitter,
        Type[]? retryableExceptions,
        string[]? retryableMessagePatterns)
    {
        if (maxAttempts < 1)
            throw new ArgumentOutOfRangeException(nameof(maxAttempts), maxAttempts, "must be >= 1");
        if (initialDelay <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(initialDelay), initialDelay, "must be > 0");
        if (maxDelay <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(maxDelay), maxDelay, "must be > 0");
        if (initialDelay > maxDelay)
            throw new ArgumentOutOfRangeException(nameof(initialDelay), initialDelay, $"must be <= maxDelay ({maxDelay})");
        if (backoffRate < 1.0 || double.IsNaN(backoffRate) || double.IsInfinity(backoffRate))
            throw new ArgumentOutOfRangeException(nameof(backoffRate), backoffRate, "must be a finite value >= 1.0");

        _maxAttempts = maxAttempts;
        _initialDelay = initialDelay;
        _maxDelay = maxDelay;
        _backoffRate = backoffRate;
        _jitter = jitter;
        _retryableExceptions = retryableExceptions;
        _retryableMessagePatterns = retryableMessagePatterns?
            .Select(p => new Regex(p))
            .ToArray();
    }

    public RetryDecision ShouldRetry(Exception exception, int attemptNumber)
    {
        if (attemptNumber >= _maxAttempts)
            return RetryDecision.DoNotRetry();

        if (!IsRetryable(exception))
            return RetryDecision.DoNotRetry();

        var delay = CalculateDelay(attemptNumber);
        return RetryDecision.RetryAfter(delay);
    }

    private bool IsRetryable(Exception exception)
    {
        if (_retryableExceptions == null && _retryableMessagePatterns == null)
            return true;

        if (_retryableExceptions != null)
        {
            var exType = exception.GetType();
            if (_retryableExceptions.Any(t => t.IsAssignableFrom(exType)))
                return true;
        }

        if (_retryableMessagePatterns != null)
        {
            var message = exception.Message;
            if (_retryableMessagePatterns.Any(p => p.IsMatch(message)))
                return true;
        }

        return false;
    }

    internal TimeSpan CalculateDelay(int attemptNumber)
        => ExponentialBackoff.CalculateDelay(attemptNumber, _initialDelay, _maxDelay, _backoffRate, _jitter);
}

internal sealed class DelegateRetryStrategy : IRetryStrategy
{
    private readonly Func<Exception, int, RetryDecision> _strategy;

    public DelegateRetryStrategy(Func<Exception, int, RetryDecision> strategy)
    {
        _strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
    }

    public RetryDecision ShouldRetry(Exception exception, int attemptNumber)
        => _strategy(exception, attemptNumber);
}
