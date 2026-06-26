// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.DurableExecution.Testing;
using Xunit;

namespace Amazon.Lambda.DurableExecution.Testing.Tests;

/// <summary>
/// Behavioral regression tests for the local runner: WaitForCondition driven
/// end-to-end, RunAsync's callback contract, accumulated invocation counts, and
/// the step retry attempt counter.
/// </summary>
public class RunAsyncBehaviorTests
{
    [Fact]
    public async Task RunAsync_WaitForCondition_PollsUntilDone_UnderSkipTime()
    {
        var checkCount = 0;
        await using var runner = new DurableTestRunner<int, int>(
            handler: async (input, ctx) =>
            {
                // Poll until the condition (state >= 3) is met. Without time-skipping
                // collapsing the between-poll backoff this would block for seconds.
                var final = await ctx.WaitForConditionAsync<int>(
                    check: async (state, _, _) =>
                    {
                        checkCount++;
                        await Task.CompletedTask;
                        return state + 1;
                    },
                    config: new WaitForConditionConfig<int>
                    {
                        InitialState = 0,
                        WaitStrategy = WaitStrategy.Fixed<int>(
                            TimeSpan.FromMinutes(5), maxAttempts: 20, isDone: s => s >= 3)
                    },
                    name: "poll");
                return final;
            },
            options: new TestRunnerOptions { SkipTime = true });

        var result = await runner.RunAsync(0, timeout: TimeSpan.FromSeconds(10));

        result.EnsureSucceeded();
        Assert.Equal(3, result.Result);
        Assert.True(checkCount >= 3, $"expected the condition to be polled at least 3 times, got {checkCount}");

        // The WaitForCondition op is wire-encoded as a STEP (SubType WaitForCondition).
        var poll = result.GetStep("poll");
        Assert.Equal(OperationKind.Step, poll.Kind);
        Assert.Equal(OperationSubTypes.WaitForCondition, poll.SubKind);
        Assert.Equal(OperationStatus.Succeeded, poll.Status);
    }

    [Fact]
    public async Task RunAsync_CallbackWorkflow_ThrowsActionableError()
    {
        await using var runner = new DurableTestRunner<string, string>(
            handler: async (input, ctx) =>
            {
                var approval = await ctx.WaitForCallbackAsync<string>(
                    async (_, _, _) => { }, name: "approval");
                return $"approved: {approval}";
            });

        // RunAsync cannot drive a callback workflow; it must fail with a clear
        // message pointing at the two-call pattern, not a MaxInvocations timeout.
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => runner.RunAsync("req"));
        Assert.Contains("callback", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("StartAsync", ex.Message);
    }

    [Fact]
    public async Task CallbackFlow_InvocationCount_AccumulatesAcrossStartAndWaitForResult()
    {
        await using var runner = new DurableTestRunner<string, string>(
            handler: async (input, ctx) =>
            {
                await ctx.StepAsync(async (_, _) => "before", name: "before");
                var approval = await ctx.WaitForCallbackAsync<string>(
                    async (_, _, _) => { }, name: "approval");
                await ctx.StepAsync(async (_, _) => "after", name: "after");
                return $"approved: {approval}";
            });

        var arn = await runner.StartAsync("req");
        var callbackId = await runner.WaitForCallbackAsync(arn, name: "approval");
        await runner.SendCallbackSuccessAsync(callbackId, "yes");
        var result = await runner.WaitForResultAsync(arn);

        result.EnsureSucceeded();
        Assert.Equal("approved: yes", result.Result);

        // The count reflects invocations across BOTH the pre-callback drive (StartAsync)
        // and the post-callback drive (WaitForResultAsync), not just the latter.
        Assert.True(result.InvocationCount >= 2,
            $"expected accumulated invocation count >= 2, got {result.InvocationCount}");
    }

    [Fact]
    public async Task RunAsync_StepRetry_AttemptCounterAdvancesAcrossRetries()
    {
        var attemptsSeen = new List<int>();
        await using var runner = new DurableTestRunner<string, string>(
            handler: async (input, ctx) =>
            {
                return await ctx.StepAsync(
                    async (stepCtx, _) =>
                    {
                        attemptsSeen.Add(stepCtx.AttemptNumber);
                        // Fail the first two attempts, succeed on the third.
                        if (stepCtx.AttemptNumber < 3)
                            throw new InvalidOperationException("transient");
                        await Task.CompletedTask;
                        return "ok";
                    },
                    name: "flaky",
                    config: new StepConfig
                    {
                        RetryStrategy = RetryStrategy.Exponential(
                            maxAttempts: 5, initialDelay: TimeSpan.FromMinutes(1))
                    });
            },
            options: new TestRunnerOptions { SkipTime = true });

        var result = await runner.RunAsync("x", timeout: TimeSpan.FromSeconds(10));

        result.EnsureSucceeded();
        Assert.Equal("ok", result.Result);
        // The attempt number must advance 1 -> 2 -> 3, not stick at 2. Guards both
        // the "never increments" bug and a "double-increments" over-correction.
        Assert.Equal(new[] { 1, 2, 3 }, attemptsSeen);

        var step = result.GetStep("flaky");
        Assert.Equal(3, step.Attempt);
    }
}
