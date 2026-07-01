// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.DurableExecution.Testing;
using Xunit;

namespace Amazon.Lambda.DurableExecution.Testing.Tests;

public class CallbackTests
{
    [Fact]
    public async Task CallbackWorkflow_FullRoundTrip()
    {
        await using var runner = new DurableTestRunner<string, string>(
            handler: async (input, ctx) =>
            {
                var approval = await ctx.WaitForCallbackAsync<string>(
                    async (callbackId, cbCtx, ct) => { /* submitter: e.g. send to external system */ },
                    name: "approval");
                return $"approved: {approval}";
            });

        var arn = await runner.StartAsync("request-1");
        var callbackId = await runner.WaitForCallbackAsync(arn, name: "approval");

        Assert.NotNull(callbackId);
        Assert.StartsWith("cb-", callbackId);

        await runner.SendCallbackSuccessAsync(callbackId, "yes");
        var result = await runner.WaitForResultAsync(arn);

        result.EnsureSucceeded();
        Assert.Equal("approved: yes", result.Result);
    }

    [Fact]
    public async Task CallbackWorkflow_Failure()
    {
        await using var runner = new DurableTestRunner<string, string>(
            handler: async (input, ctx) =>
            {
                try
                {
                    var res = await ctx.WaitForCallbackAsync<string>(
                        async (callbackId, cbCtx, ct) => { },
                        name: "check");
                    return res;
                }
                catch (CallbackException ex)
                {
                    return $"failed: {ex.ErrorType}";
                }
            });

        var arn = await runner.StartAsync("req");
        var callbackId = await runner.WaitForCallbackAsync(arn, name: "check");

        await runner.SendCallbackFailureAsync(callbackId,
            new ErrorObject { ErrorType = "Rejected", ErrorMessage = "nope" });
        var result = await runner.WaitForResultAsync(arn);

        result.EnsureSucceeded();
        Assert.Equal("failed: Rejected", result.Result);
    }

    [Fact]
    public async Task WaitForCallbackAsync_ThrowsWhenNoPendingCallback()
    {
        await using var runner = new DurableTestRunner<string, string>(
            handler: async (input, ctx) =>
            {
                // No callback in this workflow
                return await ctx.StepAsync(async (_, _) => "done", name: "step1");
            });

        var arn = await runner.StartAsync("x");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => runner.WaitForCallbackAsync(arn, name: "nonexistent"));
    }

    [Fact]
    public async Task SendCallbackSuccess_ThrowsForUnknownCallbackId()
    {
        await using var runner = new DurableTestRunner<string, string>(
            handler: async (input, ctx) => "ok");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => runner.SendCallbackSuccessAsync("cb-unknown", "data"));
    }

    [Fact]
    public async Task WaitForResultAsync_ReturnsCachedResult()
    {
        await using var runner = new DurableTestRunner<string, string>(
            handler: async (input, ctx) =>
            {
                var val = await ctx.WaitForCallbackAsync<string>(
                    async (_, _, _) => { }, name: "cb");
                return val;
            });

        var arn = await runner.StartAsync("input");
        var cbId = await runner.WaitForCallbackAsync(arn, name: "cb");
        await runner.SendCallbackSuccessAsync(cbId, "result1");
        var result1 = await runner.WaitForResultAsync(arn);
        var result2 = await runner.WaitForResultAsync(arn);

        Assert.Same(result1, result2);
    }

    [Fact]
    public async Task SendCallbackHeartbeat_DoesNotThrow()
    {
        await using var runner = new DurableTestRunner<string, string>(
            handler: async (input, ctx) =>
            {
                var val = await ctx.WaitForCallbackAsync<string>(
                    async (_, _, _) => { }, name: "hb");
                return val;
            });

        var arn = await runner.StartAsync("input");
        var cbId = await runner.WaitForCallbackAsync(arn, name: "hb");

        // Heartbeat should not throw
        await runner.SendCallbackHeartbeatAsync(cbId);

        // Workflow still completes normally
        await runner.SendCallbackSuccessAsync(cbId, "alive");
        var result = await runner.WaitForResultAsync(arn);
        result.EnsureSucceeded();
    }

}
