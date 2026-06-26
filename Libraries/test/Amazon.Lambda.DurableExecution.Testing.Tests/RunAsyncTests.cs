// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.DurableExecution.Testing;
using Xunit;

namespace Amazon.Lambda.DurableExecution.Testing.Tests;

public class RunAsyncTests
{
    [Fact]
    public async Task RunAsync_SingleStep_Succeeds()
    {
        await using var runner = new DurableTestRunner<string, string>(
            handler: async (input, ctx) =>
            {
                var result = await ctx.StepAsync(
                    async (_, _) => $"Hello, {input}!",
                    name: "greet");
                return result;
            });

        var result = await runner.RunAsync("World");

        result.EnsureSucceeded();
        Assert.Equal("Hello, World!", result.Result);
        Assert.True(result.InvocationCount >= 1);
    }

    [Fact]
    public async Task RunAsync_MultipleSteps_AllInspectable()
    {
        await using var runner = new DurableTestRunner<int, int>(
            handler: async (input, ctx) =>
            {
                var a = await ctx.StepAsync(
                    async (_, _) => input * 2,
                    name: "double");
                var b = await ctx.StepAsync(
                    async (_, _) => a + 10,
                    name: "add_ten");
                return b;
            });

        var result = await runner.RunAsync(5);

        result.EnsureSucceeded();
        Assert.Equal(20, result.Result);

        var doubleStep = result.GetStep("double");
        Assert.Equal(OperationKind.Step, doubleStep.Kind);
        Assert.Equal(OperationStatus.Succeeded, doubleStep.Status);
        Assert.Equal(10, doubleStep.GetResult<int>());

        var addStep = result.GetStep("add_ten");
        Assert.Equal(OperationStatus.Succeeded, addStep.Status);
        Assert.Equal(20, addStep.GetResult<int>());
    }

    [Fact]
    public async Task RunAsync_WorkflowThrows_ReturnsFailed()
    {
        await using var runner = new DurableTestRunner<string, string>(
            handler: async (input, ctx) =>
            {
                await ctx.StepAsync(async (_, _) =>
                {
                    throw new InvalidOperationException("something broke");
#pragma warning disable CS0162
                    return "";
#pragma warning restore CS0162
                }, name: "boom");
                return "unreachable";
            });

        var result = await runner.RunAsync("test");

        Assert.Equal(InvocationStatus.Failed, result.Status);
        Assert.NotNull(result.Error);
        Assert.Equal("System.InvalidOperationException", result.Error!.ErrorType);
    }

    [Fact]
    public async Task RunAsync_EnsureSucceeded_ThrowsOnFailure()
    {
        await using var runner = new DurableTestRunner<string, string>(
            handler: (input, ctx) =>
            {
                throw new ArgumentException("bad input");
            });

        var result = await runner.RunAsync("test");
        var ex = Assert.Throws<TestExecutionFailedException>(() => result.EnsureSucceeded());
        Assert.Equal(InvocationStatus.Failed, ex.FinalStatus);
    }

    [Fact]
    public async Task RunAsync_WithWait_SkipsTime()
    {
        await using var runner = new DurableTestRunner<string, string>(
            handler: async (input, ctx) =>
            {
                await ctx.StepAsync(async (_, _) => "done", name: "before");
                await ctx.WaitAsync(TimeSpan.FromDays(30), name: "long_wait");
                var after = await ctx.StepAsync(async (_, _) => "completed", name: "after");
                return after;
            },
            options: new TestRunnerOptions { SkipTime = true });

        var result = await runner.RunAsync("go");

        result.EnsureSucceeded();
        Assert.Equal("completed", result.Result);

        var wait = result.FindStep("long_wait");
        Assert.NotNull(wait);
        Assert.Equal(OperationKind.Wait, wait!.Kind);
        Assert.Equal(OperationStatus.Succeeded, wait.Status);
    }

    [Fact]
    public async Task RunAsync_MaxInvocationsExceeded_Throws()
    {
        await using var runner = new DurableTestRunner<string, string>(
            handler: async (input, ctx) =>
            {
                await ctx.WaitAsync(TimeSpan.FromHours(1), name: "infinite");
                return "done";
            },
            options: new TestRunnerOptions { SkipTime = false, MaxInvocations = 3, DefaultTimeout = TimeSpan.FromSeconds(5) });

        await Assert.ThrowsAsync<TestExecutionLimitException>(() => runner.RunAsync("x"));
    }

    [Fact]
    public async Task RunAsync_Timeout_ThrowsOperationCanceled()
    {
        await using var runner = new DurableTestRunner<string, string>(
            handler: async (input, ctx) =>
            {
                await ctx.WaitAsync(TimeSpan.FromHours(1), name: "forever");
                return "done";
            },
            options: new TestRunnerOptions { SkipTime = false, MaxInvocations = 1000 });

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => runner.RunAsync("x", timeout: TimeSpan.FromMilliseconds(100)));
    }

    [Fact]
    public async Task RunAsync_NullResult_ReturnsDefault()
    {
        await using var runner = new DurableTestRunner<string, string?>(
            handler: async (input, ctx) =>
            {
                await ctx.StepAsync(async (_, _) => "done", name: "noop");
                return null;
            });

        var result = await runner.RunAsync("test");
        result.EnsureSucceeded();
        Assert.Null(result.Result);
    }

    [Fact]
    public async Task RunAsync_CustomArn_UsedInResult()
    {
        const string customArn = "arn:aws:lambda:eu-west-1:999:execution:my-fn:my-exec";
        await using var runner = new DurableTestRunner<string, string>(
            handler: async (input, ctx) => "ok",
            options: new TestRunnerOptions { DurableExecutionArn = customArn });

        var result = await runner.RunAsync("test");
        result.EnsureSucceeded();
        Assert.Equal(customArn, result.DurableExecutionArn);
    }
}
