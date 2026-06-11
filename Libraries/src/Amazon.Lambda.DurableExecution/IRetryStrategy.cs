// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

namespace Amazon.Lambda.DurableExecution;

/// <summary>
/// Determines whether a failed step should be retried and with what delay.
/// </summary>
public interface IRetryStrategy
{
    /// <summary>
    /// Evaluates whether the given exception warrants a retry.
    /// </summary>
    /// <param name="exception">The exception that caused the step to fail.</param>
    /// <param name="attemptNumber">The 1-based attempt number that just failed.</param>
    /// <returns>A decision indicating whether to retry and the delay before the next attempt.</returns>
    RetryDecision ShouldRetry(Exception exception, int attemptNumber);
}

/// <summary>
/// The outcome of a retry evaluation.
/// </summary>
public readonly struct RetryDecision
{
    /// <summary>Whether the step should be retried.</summary>
    public bool ShouldRetry { get; }

    /// <summary>The delay before the next retry attempt.</summary>
    public TimeSpan Delay { get; }

    private RetryDecision(bool shouldRetry, TimeSpan delay)
    {
        ShouldRetry = shouldRetry;
        Delay = delay;
    }

    /// <summary>Indicates the step should not be retried.</summary>
    public static RetryDecision DoNotRetry() => new(false, TimeSpan.Zero);

    /// <summary>Indicates the step should be retried after the specified delay.</summary>
    public static RetryDecision RetryAfter(TimeSpan delay) => new(true, delay);
}
