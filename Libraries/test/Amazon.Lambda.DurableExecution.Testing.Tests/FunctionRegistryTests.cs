// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution.Testing;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.Lambda.TestUtilities;
using Xunit;

namespace Amazon.Lambda.DurableExecution.Testing.Tests;

public class FunctionRegistryTests
{
    private static readonly DefaultLambdaJsonSerializer Serializer = new();
    private static readonly TestLambdaContext LambdaContext = new();

    [Fact]
    public async Task InvokeAsync_RegisteredByShortName_InvokedByShortName()
    {
        var registry = new FunctionRegistry();
        registry.RegisterPlain<PaymentRequest, PaymentResult>(
            "process-payment", ProcessPayment);

        var (result, error) = await registry.InvokeAsync(
            "process-payment", """{"Amount":100}""", Serializer, LambdaContext);

        Assert.Null(error);
        Assert.Contains("100", result);
    }

    [Fact]
    public async Task InvokeAsync_RegisteredByShortName_InvokedByFullArn()
    {
        var registry = new FunctionRegistry();
        registry.RegisterPlain<PaymentRequest, PaymentResult>(
            "process-payment", ProcessPayment);

        var (result, error) = await registry.InvokeAsync(
            "arn:aws:lambda:us-east-1:123456789012:function:process-payment",
            """{"Amount":50}""", Serializer, LambdaContext);

        Assert.Null(error);
        Assert.Contains("50", result);
    }

    [Fact]
    public async Task InvokeAsync_RegisteredByFullArn_InvokedByShortName()
    {
        var registry = new FunctionRegistry();
        registry.RegisterPlain<PaymentRequest, PaymentResult>(
            "arn:aws:lambda:us-east-1:123456789012:function:process-payment",
            ProcessPayment);

        var (result, error) = await registry.InvokeAsync(
            "process-payment", """{"Amount":75}""", Serializer, LambdaContext);

        Assert.Null(error);
        Assert.Contains("75", result);
    }

    [Fact]
    public async Task InvokeAsync_ExactMatchWins_OverArnExtraction()
    {
        var registry = new FunctionRegistry();
        registry.RegisterPlain<PaymentRequest, PaymentResult>(
            "process-payment", (req, _) => Task.FromResult(new PaymentResult { Status = "short" }));
        registry.RegisterPlain<PaymentRequest, PaymentResult>(
            "arn:aws:lambda:us-east-1:123456789012:function:process-payment",
            (req, _) => Task.FromResult(new PaymentResult { Status = "arn" }));

        var (result, _) = await registry.InvokeAsync(
            "arn:aws:lambda:us-east-1:123456789012:function:process-payment",
            """{"Amount":1}""", Serializer, LambdaContext);

        Assert.Contains("arn", result);
    }

    [Fact]
    public async Task InvokeAsync_UnregisteredFunction_Throws()
    {
        var registry = new FunctionRegistry();

        await Assert.ThrowsAsync<UnregisteredSiblingFunctionException>(() =>
            registry.InvokeAsync("unknown-fn", "{}", Serializer, LambdaContext));
    }

    [Fact]
    public async Task InvokeAsync_HandlerThatThrows_ReturnsError()
    {
        var registry = new FunctionRegistry();
        registry.RegisterPlain<PaymentRequest, PaymentResult>(
            "failing-fn", (_, _) => throw new InvalidOperationException("payment failed"));

        var (result, error) = await registry.InvokeAsync(
            "failing-fn", """{"Amount":1}""", Serializer, LambdaContext);

        Assert.Null(result);
        Assert.NotNull(error);
        Assert.Equal("System.InvalidOperationException", error!.ErrorType);
        Assert.Equal("payment failed", error.ErrorMessage);
    }

    [Fact]
    public void IsRegistered_ReturnsTrueForRegistered()
    {
        var registry = new FunctionRegistry();
        registry.RegisterPlain<PaymentRequest, PaymentResult>("my-fn", ProcessPayment);

        Assert.True(registry.IsRegistered("my-fn"));
        Assert.True(registry.IsRegistered("arn:aws:lambda:us-east-1:123:function:my-fn"));
    }

    [Fact]
    public void IsRegistered_ReturnsFalseForUnregistered()
    {
        var registry = new FunctionRegistry();
        Assert.False(registry.IsRegistered("unknown"));
    }

    [Fact]
    public async Task InvokeAsync_WithQualifiedArn_ExtractsName()
    {
        var registry = new FunctionRegistry();
        registry.RegisterPlain<PaymentRequest, PaymentResult>("calc", ProcessPayment);

        var (result, error) = await registry.InvokeAsync(
            "arn:aws:lambda:us-east-1:123:function:calc:$LATEST",
            """{"Amount":10}""", Serializer, LambdaContext);

        Assert.Null(error);
        Assert.NotNull(result);
    }

    private static Task<PaymentResult> ProcessPayment(PaymentRequest req, ILambdaContext ctx)
    {
        return Task.FromResult(new PaymentResult { Status = $"approved-{req.Amount}" });
    }

    public sealed class PaymentRequest { public int Amount { get; set; } }
    public sealed class PaymentResult { public string? Status { get; set; } }
}
