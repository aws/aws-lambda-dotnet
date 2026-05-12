using Amazon.Lambda.DurableExecution;
using Xunit;

namespace Amazon.Lambda.DurableExecution.Tests;

public class RetryStrategyTests
{
    [Fact]
    public void ExponentialDefault_RetriesUpToMaxAttempts()
    {
        var strategy = RetryStrategy.Default;

        // Attempts 1-5 should retry (maxAttempts=6 means 6 total attempts)
        for (int i = 1; i < 6; i++)
        {
            var decision = strategy.ShouldRetry(new InvalidOperationException("fail"), i);
            Assert.True(decision.ShouldRetry);
            Assert.True(decision.Delay >= TimeSpan.FromSeconds(1));
        }

        // Attempt 6 should not retry (exhausted)
        var lastDecision = strategy.ShouldRetry(new InvalidOperationException("fail"), 6);
        Assert.False(lastDecision.ShouldRetry);
    }

    [Fact]
    public void None_NeverRetries()
    {
        var strategy = RetryStrategy.None;

        var decision = strategy.ShouldRetry(new Exception("fail"), 1);
        Assert.False(decision.ShouldRetry);
    }

    [Fact]
    public void Transient_RetriesUpTo3Attempts()
    {
        var strategy = RetryStrategy.Transient;

        Assert.True(strategy.ShouldRetry(new Exception("fail"), 1).ShouldRetry);
        Assert.True(strategy.ShouldRetry(new Exception("fail"), 2).ShouldRetry);
        Assert.False(strategy.ShouldRetry(new Exception("fail"), 3).ShouldRetry);
    }

    [Fact]
    public void Exponential_DelayIncreases()
    {
        var strategy = RetryStrategy.Exponential(
            maxAttempts: 5,
            initialDelay: TimeSpan.FromSeconds(2),
            maxDelay: TimeSpan.FromSeconds(120),
            backoffRate: 2.0,
            jitter: JitterStrategy.None);

        var d1 = strategy.ShouldRetry(new Exception(), 1).Delay;
        var d2 = strategy.ShouldRetry(new Exception(), 2).Delay;
        var d3 = strategy.ShouldRetry(new Exception(), 3).Delay;

        // With no jitter: 2s, 4s, 8s (ceiling to whole seconds)
        Assert.Equal(TimeSpan.FromSeconds(2), d1);
        Assert.Equal(TimeSpan.FromSeconds(4), d2);
        Assert.Equal(TimeSpan.FromSeconds(8), d3);
    }

    [Fact]
    public void Exponential_DelayCapsAtMax()
    {
        var strategy = RetryStrategy.Exponential(
            maxAttempts: 10,
            initialDelay: TimeSpan.FromSeconds(10),
            maxDelay: TimeSpan.FromSeconds(30),
            backoffRate: 3.0,
            jitter: JitterStrategy.None);

        // Attempt 3: 10 * 3^2 = 90, capped to 30
        var decision = strategy.ShouldRetry(new Exception(), 3);
        Assert.Equal(TimeSpan.FromSeconds(30), decision.Delay);
    }

    [Fact]
    public void Exponential_FullJitter_BoundedByDelay()
    {
        var strategy = RetryStrategy.Exponential(
            maxAttempts: 5,
            initialDelay: TimeSpan.FromSeconds(10),
            maxDelay: TimeSpan.FromSeconds(100),
            backoffRate: 2.0,
            jitter: JitterStrategy.Full);

        // Run multiple times to check bounds
        for (int i = 0; i < 50; i++)
        {
            var decision = strategy.ShouldRetry(new Exception(), 1);
            Assert.True(decision.Delay >= TimeSpan.FromSeconds(1));
            Assert.True(decision.Delay <= TimeSpan.FromSeconds(10));
        }
    }

    [Fact]
    public void Exponential_HalfJitter_BoundedBetween50And100Percent()
    {
        var strategy = RetryStrategy.Exponential(
            maxAttempts: 5,
            initialDelay: TimeSpan.FromSeconds(10),
            maxDelay: TimeSpan.FromSeconds(100),
            backoffRate: 2.0,
            jitter: JitterStrategy.Half);

        for (int i = 0; i < 50; i++)
        {
            var decision = strategy.ShouldRetry(new Exception(), 1);
            Assert.True(decision.Delay >= TimeSpan.FromSeconds(5));
            Assert.True(decision.Delay <= TimeSpan.FromSeconds(10));
        }
    }

    [Fact]
    public void Exponential_RetryableExceptions_FiltersCorrectly()
    {
        var strategy = RetryStrategy.Exponential(
            maxAttempts: 3,
            retryableExceptions: new[] { typeof(TimeoutException), typeof(HttpRequestException) });

        Assert.True(strategy.ShouldRetry(new TimeoutException(), 1).ShouldRetry);
        Assert.True(strategy.ShouldRetry(new HttpRequestException(), 1).ShouldRetry);
        Assert.False(strategy.ShouldRetry(new InvalidOperationException(), 1).ShouldRetry);
    }

    [Fact]
    public void Exponential_RetryableExceptions_MatchesDerivedTypes()
    {
        var strategy = RetryStrategy.Exponential(
            maxAttempts: 3,
            retryableExceptions: new[] { typeof(IOException) });

        Assert.True(strategy.ShouldRetry(new FileNotFoundException(), 1).ShouldRetry);
    }

    [Fact]
    public void Exponential_MessagePatterns_FiltersCorrectly()
    {
        var strategy = RetryStrategy.Exponential(
            maxAttempts: 3,
            retryableMessagePatterns: new[] { "timeout", "throttl", "5\\d{2}" });

        Assert.True(strategy.ShouldRetry(new Exception("connection timeout"), 1).ShouldRetry);
        Assert.True(strategy.ShouldRetry(new Exception("request throttled"), 1).ShouldRetry);
        Assert.True(strategy.ShouldRetry(new Exception("HTTP 503"), 1).ShouldRetry);
        Assert.False(strategy.ShouldRetry(new Exception("not found"), 1).ShouldRetry);
    }

    [Fact]
    public void Exponential_BothFilters_EitherMatches()
    {
        var strategy = RetryStrategy.Exponential(
            maxAttempts: 3,
            retryableExceptions: new[] { typeof(TimeoutException) },
            retryableMessagePatterns: new[] { "throttl" });

        // Matches exception type
        Assert.True(strategy.ShouldRetry(new TimeoutException("any message"), 1).ShouldRetry);
        // Matches message pattern
        Assert.True(strategy.ShouldRetry(new Exception("throttled"), 1).ShouldRetry);
        // Matches neither
        Assert.False(strategy.ShouldRetry(new InvalidOperationException("bad state"), 1).ShouldRetry);
    }

    [Fact]
    public void Exponential_NoFilters_RetriesAllExceptions()
    {
        var strategy = RetryStrategy.Exponential(maxAttempts: 3);

        Assert.True(strategy.ShouldRetry(new Exception("anything"), 1).ShouldRetry);
        Assert.True(strategy.ShouldRetry(new InvalidOperationException(), 1).ShouldRetry);
        Assert.True(strategy.ShouldRetry(new OutOfMemoryException(), 1).ShouldRetry);
    }

    [Fact]
    public void Exponential_MinimumDelayIsOneSecond()
    {
        var strategy = RetryStrategy.Exponential(
            maxAttempts: 3,
            initialDelay: TimeSpan.FromMilliseconds(100),
            jitter: JitterStrategy.None);

        var decision = strategy.ShouldRetry(new Exception(), 1);
        Assert.True(decision.Delay >= TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void FromDelegate_UsesProvidedFunction()
    {
        var strategy = RetryStrategy.FromDelegate((ex, attempt) =>
            attempt < 2 && ex is TimeoutException
                ? RetryDecision.RetryAfter(TimeSpan.FromSeconds(5))
                : RetryDecision.DoNotRetry());

        Assert.True(strategy.ShouldRetry(new TimeoutException(), 1).ShouldRetry);
        Assert.False(strategy.ShouldRetry(new TimeoutException(), 2).ShouldRetry);
        Assert.False(strategy.ShouldRetry(new Exception(), 1).ShouldRetry);
    }
}
