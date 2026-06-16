// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.DurableExecution.Testing;
using Amazon.Lambda.Serialization.SystemTextJson;
using Xunit;

namespace Amazon.Lambda.DurableExecution.Testing.Tests;

public class TestStepTests
{
    private static readonly DefaultLambdaJsonSerializer Serializer = new();

    [Theory]
    [InlineData(OperationTypes.Step, OperationKind.Step)]
    [InlineData(OperationTypes.Wait, OperationKind.Wait)]
    [InlineData(OperationTypes.Callback, OperationKind.Callback)]
    [InlineData(OperationTypes.ChainedInvoke, OperationKind.ChainedInvoke)]
    [InlineData(OperationTypes.Context, OperationKind.Context)]
    [InlineData(OperationTypes.Execution, OperationKind.Execution)]
    public void Kind_MapsFromOperationType(string operationType, OperationKind expected)
    {
        var op = new Operation { Id = "op-1", Type = operationType };
        var step = new TestStep(op, Serializer);
        Assert.Equal(expected, step.Kind);
    }

    [Fact]
    public void Properties_ExposedFromOperation()
    {
        var op = new Operation
        {
            Id = "op-123",
            Name = "validate_order",
            ParentId = "op-parent",
            Type = OperationTypes.Step,
            SubType = OperationSubTypes.Step,
            Status = OperationStatuses.Succeeded,
            StartTimestamp = 1700000000000,
            EndTimestamp = 1700000001000,
            StepDetails = new StepDetails { Attempt = 2 }
        };

        var step = new TestStep(op, Serializer);

        Assert.Equal("op-123", step.Id);
        Assert.Equal("validate_order", step.Name);
        Assert.Equal("op-parent", step.ParentId);
        Assert.Equal(OperationKind.Step, step.Kind);
        Assert.Equal(OperationSubTypes.Step, step.SubKind);
        Assert.Equal(OperationStatus.Succeeded, step.Status);
        Assert.Equal(2, step.Attempt);
        Assert.NotNull(step.StartedAt);
        Assert.NotNull(step.EndedAt);
        Assert.NotNull(step.Duration);
        Assert.Equal(TimeSpan.FromSeconds(1), step.Duration);
    }

    [Fact]
    public void Attempt_ReturnsZero_ForNonStepKind()
    {
        var op = new Operation { Id = "op-1", Type = OperationTypes.Wait };
        var step = new TestStep(op, Serializer);
        Assert.Equal(0, step.Attempt);
    }

    [Fact]
    public void Status_DefaultsToPending_WhenNull()
    {
        var op = new Operation { Id = "op-1", Type = OperationTypes.Step, Status = null };
        var step = new TestStep(op, Serializer);
        Assert.Equal(OperationStatus.Pending, step.Status);
    }

    [Fact]
    public void Timestamps_Null_WhenNotSet()
    {
        var op = new Operation { Id = "op-1", Type = OperationTypes.Step };
        var step = new TestStep(op, Serializer);
        Assert.Null(step.StartedAt);
        Assert.Null(step.EndedAt);
        Assert.Null(step.Duration);
    }

    [Fact]
    public void GetResult_DeserializesStepResult()
    {
        var op = new Operation
        {
            Id = "op-1",
            Type = OperationTypes.Step,
            StepDetails = new StepDetails { Result = """{"Value":42}""" }
        };

        var step = new TestStep(op, Serializer);
        var result = step.GetResult<TestPayload>();

        Assert.NotNull(result);
        Assert.Equal(42, result!.Value);
    }

    [Fact]
    public void GetResult_DeserializesChainedInvokeResult()
    {
        var op = new Operation
        {
            Id = "op-1",
            Type = OperationTypes.ChainedInvoke,
            ChainedInvokeDetails = new ChainedInvokeDetails { Result = """{"Value":99}""" }
        };

        var step = new TestStep(op, Serializer);
        var result = step.GetResult<TestPayload>();

        Assert.NotNull(result);
        Assert.Equal(99, result!.Value);
    }

    [Fact]
    public void GetResult_DeserializesContextResult()
    {
        var op = new Operation
        {
            Id = "op-1",
            Type = OperationTypes.Context,
            ContextDetails = new ContextDetails { Result = """{"Value":7}""" }
        };

        var step = new TestStep(op, Serializer);
        var result = step.GetResult<TestPayload>();

        Assert.NotNull(result);
        Assert.Equal(7, result!.Value);
    }

    [Fact]
    public void GetResult_DeserializesCallbackResult()
    {
        var op = new Operation
        {
            Id = "op-1",
            Type = OperationTypes.Callback,
            CallbackDetails = new CallbackDetails { Result = """{"Value":3}""" }
        };

        var step = new TestStep(op, Serializer);
        var result = step.GetResult<TestPayload>();

        Assert.NotNull(result);
        Assert.Equal(3, result!.Value);
    }

    [Fact]
    public void GetResult_ReturnsDefault_WhenNoResult()
    {
        var op = new Operation { Id = "op-1", Type = OperationTypes.Step, StepDetails = new StepDetails() };
        var step = new TestStep(op, Serializer);
        Assert.Null(step.GetResult<TestPayload>());
    }

    [Fact]
    public void GetResult_ReturnsDefault_ForWaitKind()
    {
        var op = new Operation { Id = "op-1", Type = OperationTypes.Wait };
        var step = new TestStep(op, Serializer);
        Assert.Null(step.GetResult<TestPayload>());
    }

    [Fact]
    public void GetError_ReturnsStepError()
    {
        var error = new ErrorObject { ErrorType = "TestEx", ErrorMessage = "boom" };
        var op = new Operation
        {
            Id = "op-1",
            Type = OperationTypes.Step,
            StepDetails = new StepDetails { Error = error }
        };

        var step = new TestStep(op, Serializer);
        var result = step.GetError();

        Assert.NotNull(result);
        Assert.Equal("TestEx", result!.ErrorType);
        Assert.Equal("boom", result.ErrorMessage);
    }

    [Fact]
    public void GetError_ReturnsNull_WhenNoError()
    {
        var op = new Operation { Id = "op-1", Type = OperationTypes.Step, StepDetails = new StepDetails() };
        var step = new TestStep(op, Serializer);
        Assert.Null(step.GetError());
    }

    [Fact]
    public void GetWaitEndsAt_ReturnsTimestamp()
    {
        var op = new Operation
        {
            Id = "op-1",
            Type = OperationTypes.Wait,
            WaitDetails = new WaitDetails { ScheduledEndTimestamp = 1700000000000 }
        };

        var step = new TestStep(op, Serializer);
        var result = step.GetWaitEndsAt();

        Assert.NotNull(result);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1700000000000), result);
    }

    [Fact]
    public void GetWaitEndsAt_ReturnsNull_WhenNoWaitDetails()
    {
        var op = new Operation { Id = "op-1", Type = OperationTypes.Step };
        var step = new TestStep(op, Serializer);
        Assert.Null(step.GetWaitEndsAt());
    }

    [Fact]
    public void GetCallbackId_ReturnsId()
    {
        var op = new Operation
        {
            Id = "op-1",
            Type = OperationTypes.Callback,
            CallbackDetails = new CallbackDetails { CallbackId = "cb-abc" }
        };

        var step = new TestStep(op, Serializer);
        Assert.Equal("cb-abc", step.GetCallbackId());
    }

    [Fact]
    public void GetCallbackId_ReturnsNull_WhenNoCallbackDetails()
    {
        var op = new Operation { Id = "op-1", Type = OperationTypes.Step };
        var step = new TestStep(op, Serializer);
        Assert.Null(step.GetCallbackId());
    }

    [Fact]
    public void GetChainedInvokeFunctionName_ReturnsName()
    {
        var op = new Operation
        {
            Id = "op-1",
            Name = "process-payment",
            Type = OperationTypes.ChainedInvoke,
            ChainedInvokeDetails = new ChainedInvokeDetails()
        };

        var step = new TestStep(op, Serializer);
        Assert.Equal("process-payment", step.GetChainedInvokeFunctionName());
    }

    [Fact]
    public void GetChainedInvokeFunctionName_ReturnsNull_ForNonInvokeKind()
    {
        var op = new Operation { Id = "op-1", Name = "some-step", Type = OperationTypes.Step };
        var step = new TestStep(op, Serializer);
        Assert.Null(step.GetChainedInvokeFunctionName());
    }

    [Fact]
    public void Children_EmptyByDefault()
    {
        var op = new Operation { Id = "op-1", Type = OperationTypes.Context };
        var step = new TestStep(op, Serializer);
        Assert.Empty(step.Children);
    }

    private sealed class TestPayload
    {
        public int Value { get; set; }
    }
}
