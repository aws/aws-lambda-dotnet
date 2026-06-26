// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.DurableExecution.Testing;
using Xunit;

namespace Amazon.Lambda.DurableExecution.Testing.Tests;

/// <summary>
/// End-to-end tests that drive a workflow's <c>ctx.InvokeAsync</c> through a
/// registered sibling function. These exercise the registry-to-checkpoint wiring
/// (regression coverage for the registry never being connected to execution).
/// </summary>
public class SiblingInvokeIntegrationTests
{
    public sealed class PaymentRequest { public int Amount { get; set; } }
    public sealed class PaymentResult { public string? Status { get; set; } }

    [Fact]
    public async Task RunAsync_InvokesRegisteredPlainSibling_ThroughWorkflow()
    {
        await using var runner = new DurableTestRunner<int, string>(
            handler: async (input, ctx) =>
            {
                var payment = await ctx.InvokeAsync<PaymentRequest, PaymentResult>(
                    "process-payment",
                    new PaymentRequest { Amount = input },
                    name: "charge");
                return $"charged: {payment.Status}";
            });

        runner.RegisterFunction<PaymentRequest, PaymentResult>(
            "process-payment",
            (req, _) => Task.FromResult(new PaymentResult { Status = $"approved-{req.Amount}" }));

        var result = await runner.RunAsync(100);

        result.EnsureSucceeded();
        Assert.Equal("charged: approved-100", result.Result);

        // The chained invoke is recorded and inspectable as a step.
        var invoke = result.GetStep("charge");
        Assert.Equal(OperationKind.ChainedInvoke, invoke.Kind);
        Assert.Equal(OperationStatus.Succeeded, invoke.Status);
        Assert.Equal("approved-100", invoke.GetResult<PaymentResult>()!.Status);
    }

    [Fact]
    public async Task RunAsync_SiblingByFullArn_ResolvesByShortName()
    {
        await using var runner = new DurableTestRunner<int, string>(
            handler: async (input, ctx) =>
            {
                var r = await ctx.InvokeAsync<PaymentRequest, PaymentResult>(
                    "arn:aws:lambda:us-east-1:123456789012:function:process-payment:$LATEST",
                    new PaymentRequest { Amount = input },
                    name: "charge");
                return r.Status!;
            });

        runner.RegisterFunction<PaymentRequest, PaymentResult>(
            "process-payment",
            (req, _) => Task.FromResult(new PaymentResult { Status = $"ok-{req.Amount}" }));

        var result = await runner.RunAsync(5);
        result.EnsureSucceeded();
        Assert.Equal("ok-5", result.Result);
    }

    [Fact]
    public async Task RunAsync_UnregisteredSibling_ThrowsUnregisteredSiblingFunctionException()
    {
        await using var runner = new DurableTestRunner<int, string>(
            handler: async (input, ctx) =>
            {
                var r = await ctx.InvokeAsync<PaymentRequest, PaymentResult>(
                    "not-registered", new PaymentRequest { Amount = input }, name: "charge");
                return r.Status!;
            },
            options: new TestRunnerOptions { MaxInvocations = 10 });

        // Must surface the actionable exception, NOT degrade to a TestExecutionLimitException.
        await Assert.ThrowsAsync<UnregisteredSiblingFunctionException>(() => runner.RunAsync(1));
    }

    [Fact]
    public async Task RunAsync_SiblingThatThrows_SurfacesAsInvokeFailure()
    {
        await using var runner = new DurableTestRunner<int, string>(
            handler: async (input, ctx) =>
            {
                try
                {
                    var r = await ctx.InvokeAsync<PaymentRequest, PaymentResult>(
                        "failing", new PaymentRequest { Amount = input }, name: "charge");
                    return r.Status!;
                }
                catch (InvokeException ex)
                {
                    return $"failed: {ex.ErrorType}";
                }
            });

        runner.RegisterFunction<PaymentRequest, PaymentResult>(
            "failing",
            (_, _) => throw new InvalidOperationException("downstream boom"));

        var result = await runner.RunAsync(1);
        result.EnsureSucceeded();
        Assert.Equal("failed: System.InvalidOperationException", result.Result);

        // The chained-invoke step records the failure for inspection.
        var invoke = result.GetStep("charge");
        Assert.Equal(OperationStatus.Failed, invoke.Status);
        Assert.Equal("System.InvalidOperationException", invoke.GetError()!.ErrorType);
    }

    [Fact]
    public async Task RunAsync_InvokesRegisteredDurableSibling_RunsInNestedRunner()
    {
        await using var runner = new DurableTestRunner<int, string>(
            handler: async (input, ctx) =>
            {
                var r = await ctx.InvokeAsync<PaymentRequest, PaymentResult>(
                    "durable-child", new PaymentRequest { Amount = input }, name: "child");
                return $"parent saw: {r.Status}";
            });

        // The durable sibling is itself a workflow with its own steps.
        runner.RegisterDurableFunction<PaymentRequest, PaymentResult>(
            "durable-child",
            async (req, childCtx) =>
            {
                var doubled = await childCtx.StepAsync(
                    async (_, _) => req.Amount * 2, name: "double");
                return new PaymentResult { Status = $"child-{doubled}" };
            });

        var result = await runner.RunAsync(21);
        result.EnsureSucceeded();
        Assert.Equal("parent saw: child-42", result.Result);
    }
}
