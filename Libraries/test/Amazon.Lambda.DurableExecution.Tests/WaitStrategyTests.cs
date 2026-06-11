using Amazon.Lambda.DurableExecution;
using Xunit;

namespace Amazon.Lambda.DurableExecution.Tests;

public class WaitStrategyTests
{
    [Fact]
    public void Exponential_Defaults_MatchReferenceSDKs()
    {
        // Reference SDKs (Python, JS, Java) all default to:
        //   maxAttempts=60, initialDelay=5s, maxDelay=300s, backoff=1.5x, FullJitter.
        // Verify by exercising the boundary: an attempt one short of 60
        // continues; the 60th throws (matches the JS SDK pattern of
        // signaling max-attempts via exception so the operation can produce
        // a WaitForConditionException carrying the last state).
        var strategy = WaitStrategy.Exponential<string>();

        Assert.True(strategy.Decide("any", 1).ShouldContinue);
        Assert.True(strategy.Decide("any", 59).ShouldContinue);

        var ex = Assert.Throws<WaitForConditionException>(() => strategy.Decide("any", 60));
        Assert.Equal(60, ex.AttemptsExhausted);
    }

    [Fact]
    public void Exponential_NoIsDone_ThrowsAtMaxAttempts()
    {
        var strategy = WaitStrategy.Exponential<int>(maxAttempts: 5);

        Assert.True(strategy.Decide(0, 1).ShouldContinue);
        Assert.True(strategy.Decide(0, 4).ShouldContinue);

        var ex = Assert.Throws<WaitForConditionException>(() => strategy.Decide(0, 5));
        Assert.Equal(5, ex.AttemptsExhausted);
    }

    [Fact]
    public void Exponential_IsDoneTrue_StopsRegardlessOfAttempt()
    {
        var strategy = WaitStrategy.Exponential<int>(
            maxAttempts: 100,
            isDone: state => state >= 10);

        // Predicate is the gate, not the attempt counter.
        Assert.True(strategy.Decide(5, 1).ShouldContinue);
        Assert.False(strategy.Decide(10, 1).ShouldContinue);
        Assert.False(strategy.Decide(15, 1).ShouldContinue);
    }

    [Fact]
    public void Exponential_DelayGrowsAndCapsAtMax()
    {
        var strategy = WaitStrategy.Exponential<int>(
            maxAttempts: 20,
            initialDelay: TimeSpan.FromSeconds(2),
            maxDelay: TimeSpan.FromSeconds(20),
            backoffRate: 2.0,
            jitter: JitterStrategy.None);

        Assert.Equal(TimeSpan.FromSeconds(2), strategy.Decide(0, 1).Delay);
        Assert.Equal(TimeSpan.FromSeconds(4), strategy.Decide(0, 2).Delay);
        Assert.Equal(TimeSpan.FromSeconds(8), strategy.Decide(0, 3).Delay);
        Assert.Equal(TimeSpan.FromSeconds(16), strategy.Decide(0, 4).Delay);
        // 2 * 2^4 = 32, capped at 20.
        Assert.Equal(TimeSpan.FromSeconds(20), strategy.Decide(0, 5).Delay);
    }

    [Fact]
    public void Exponential_FullJitter_StaysWithinBounds()
    {
        var strategy = WaitStrategy.Exponential<int>(
            maxAttempts: 20,
            initialDelay: TimeSpan.FromSeconds(10),
            maxDelay: TimeSpan.FromSeconds(100),
            backoffRate: 2.0,
            jitter: JitterStrategy.Full);

        for (int i = 0; i < 50; i++)
        {
            var d = strategy.Decide(0, 1).Delay;
            // With Full jitter at attempt 1: between 1 (floor) and 10 inclusive.
            Assert.True(d >= TimeSpan.FromSeconds(1));
            Assert.True(d <= TimeSpan.FromSeconds(10));
        }
    }

    [Fact]
    public void Exponential_HalfJitter_StaysWithinBounds()
    {
        // Half-jitter formula: cappedDelay * (0.5 + 0.5 * rand) ⇒ output is in
        // [cappedDelay/2, cappedDelay], then ceilinged to whole seconds with a
        // 1-second floor. At attempt 3 with initialDelay=10s, backoff=2.0:
        // cappedDelay = min(10 * 2^2, 100) = 40s ⇒ output ∈ [20, 40] seconds.
        var strategy = WaitStrategy.Exponential<int>(
            maxAttempts: 20,
            initialDelay: TimeSpan.FromSeconds(10),
            maxDelay: TimeSpan.FromSeconds(100),
            backoffRate: 2.0,
            jitter: JitterStrategy.Half);

        for (int i = 0; i < 50; i++)
        {
            var d = strategy.Decide(0, 3).Delay;
            Assert.True(d >= TimeSpan.FromSeconds(20), $"expected >= 20s, got {d}");
            Assert.True(d <= TimeSpan.FromSeconds(40), $"expected <= 40s, got {d}");
        }
    }

    [Fact]
    public void Linear_DefaultsAreSensible()
    {
        // Default: 5s initial, +5s per attempt, no cap, 60 attempts.
        var strategy = WaitStrategy.Linear<int>();

        Assert.Equal(TimeSpan.FromSeconds(5), strategy.Decide(0, 1).Delay);
        Assert.Equal(TimeSpan.FromSeconds(10), strategy.Decide(0, 2).Delay);
        Assert.Equal(TimeSpan.FromSeconds(15), strategy.Decide(0, 3).Delay);
    }

    [Fact]
    public void Linear_RespectsMaxDelay()
    {
        var strategy = WaitStrategy.Linear<int>(
            maxAttempts: 10,
            initialDelay: TimeSpan.FromSeconds(2),
            increment: TimeSpan.FromSeconds(3),
            maxDelay: TimeSpan.FromSeconds(8));

        Assert.Equal(TimeSpan.FromSeconds(2), strategy.Decide(0, 1).Delay);
        Assert.Equal(TimeSpan.FromSeconds(5), strategy.Decide(0, 2).Delay);
        Assert.Equal(TimeSpan.FromSeconds(8), strategy.Decide(0, 3).Delay);
        // 2+3*3=11, capped to 8.
        Assert.Equal(TimeSpan.FromSeconds(8), strategy.Decide(0, 4).Delay);
    }

    [Fact]
    public void Linear_ThrowsAtMaxAttempts()
    {
        var strategy = WaitStrategy.Linear<int>(maxAttempts: 3);

        Assert.True(strategy.Decide(0, 1).ShouldContinue);
        Assert.True(strategy.Decide(0, 2).ShouldContinue);
        Assert.Throws<WaitForConditionException>(() => strategy.Decide(0, 3));
    }

    [Fact]
    public void Linear_IsDonePredicate_ShortCircuits()
    {
        var strategy = WaitStrategy.Linear<int>(
            maxAttempts: 100,
            isDone: state => state == 42);

        Assert.True(strategy.Decide(1, 1).ShouldContinue);
        Assert.False(strategy.Decide(42, 1).ShouldContinue);
    }

    [Fact]
    public void Fixed_AlwaysReturnsSameDelay()
    {
        var strategy = WaitStrategy.Fixed<int>(TimeSpan.FromSeconds(7), maxAttempts: 5);

        Assert.Equal(TimeSpan.FromSeconds(7), strategy.Decide(0, 1).Delay);
        Assert.Equal(TimeSpan.FromSeconds(7), strategy.Decide(0, 2).Delay);
        Assert.Equal(TimeSpan.FromSeconds(7), strategy.Decide(0, 4).Delay);
    }

    [Fact]
    public void Fixed_ThrowsAtMaxAttempts()
    {
        var strategy = WaitStrategy.Fixed<int>(TimeSpan.FromSeconds(2), maxAttempts: 3);

        Assert.True(strategy.Decide(0, 1).ShouldContinue);
        Assert.True(strategy.Decide(0, 2).ShouldContinue);
        Assert.Throws<WaitForConditionException>(() => strategy.Decide(0, 3));
    }

    [Fact]
    public void Fixed_FloorsDelayAtOneSecond()
    {
        // Service timer granularity is 1 second; sub-second delays would
        // round to 0 if we didn't floor.
        var strategy = WaitStrategy.Fixed<int>(TimeSpan.FromMilliseconds(100), maxAttempts: 3);
        var decision = strategy.Decide(0, 1);
        Assert.True(decision.ShouldContinue);
        Assert.Equal(TimeSpan.FromSeconds(1), decision.Delay);
    }

    [Fact]
    public void Fixed_IsDonePredicate_ShortCircuits()
    {
        var strategy = WaitStrategy.Fixed<bool>(
            TimeSpan.FromSeconds(1),
            maxAttempts: 50,
            isDone: state => state);

        Assert.True(strategy.Decide(false, 1).ShouldContinue);
        Assert.False(strategy.Decide(true, 1).ShouldContinue);
    }

    [Fact]
    public void FromDelegate_UsesProvidedFunction()
    {
        var strategy = WaitStrategy.FromDelegate<int>((state, attempt) =>
            state >= 3 || attempt >= 5
                ? WaitDecision.Stop()
                : WaitDecision.ContinueAfter(TimeSpan.FromSeconds(state + 1)));

        Assert.True(strategy.Decide(0, 1).ShouldContinue);
        Assert.Equal(TimeSpan.FromSeconds(1), strategy.Decide(0, 1).Delay);
        Assert.False(strategy.Decide(3, 1).ShouldContinue);
        Assert.False(strategy.Decide(0, 5).ShouldContinue);
    }

    [Fact]
    public void WaitDecision_StopAndContinueAfter_ProduceExpectedShape()
    {
        var stop = WaitDecision.Stop();
        Assert.False(stop.ShouldContinue);
        Assert.Equal(TimeSpan.Zero, stop.Delay);

        var cont = WaitDecision.ContinueAfter(TimeSpan.FromSeconds(3));
        Assert.True(cont.ShouldContinue);
        Assert.Equal(TimeSpan.FromSeconds(3), cont.Delay);
    }
}
