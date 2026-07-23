// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.DurableExecution.Testing;
using Amazon.Lambda.Model;
using Xunit;

namespace Amazon.Lambda.DurableExecution.Testing.Tests;

/// <summary>
/// Covers the boundary adapter that flattens the AWSSDK <c>OperationUpdate</c> into the neutral
/// <c>OperationUpdateInput</c> the shared kernel consumes. The state-machine behavior itself is
/// tested in the LocalEmulation kernel test project; here we only assert the field-by-field mapping
/// (ConstantClass unwrapping, nested option flattening, error conversion).
/// </summary>
public class SdkOperationUpdateMapperTests
{
    [Fact]
    public void ToInput_UnwrapsConstantClassMembers()
    {
        var update = new OperationUpdate
        {
            Id = "op-1",
            ParentId = "parent-1",
            Name = "validate",
            Type = OperationTypes.Step,
            SubType = OperationSubTypes.WaitForCondition,
            Action = OperationAction.RETRY,
            Payload = """{"x":1}"""
        };

        var input = SdkOperationUpdateMapper.ToInput(update);

        Assert.Equal("op-1", input.Id);
        Assert.Equal("parent-1", input.ParentId);
        Assert.Equal("validate", input.Name);
        Assert.Equal(OperationTypes.Step, input.Type);
        Assert.Equal(OperationSubTypes.WaitForCondition, input.SubType);
        Assert.Equal("RETRY", input.Action);
        Assert.Equal("""{"x":1}""", input.Payload);
    }

    [Fact]
    public void ToInput_FlattensNestedOptions()
    {
        var update = new OperationUpdate
        {
            Id = "op-1",
            Type = OperationTypes.Wait,
            Action = OperationAction.START,
            StepOptions = new StepOptions { NextAttemptDelaySeconds = 7 },
            WaitOptions = new WaitOptions { WaitSeconds = 300 },
            ChainedInvokeOptions = new ChainedInvokeOptions { FunctionName = "payment-fn" }
        };

        var input = SdkOperationUpdateMapper.ToInput(update);

        Assert.Equal(7, input.NextAttemptDelaySeconds);
        Assert.Equal(300, input.WaitSeconds);
        Assert.Equal("payment-fn", input.ChainedInvokeFunctionName);
    }

    [Fact]
    public void ToInput_MapsError()
    {
        var update = new OperationUpdate
        {
            Id = "op-1",
            Type = OperationTypes.Step,
            Action = OperationAction.FAIL,
            Error = new Amazon.Lambda.Model.ErrorObject
            {
                ErrorType = "TestEx",
                ErrorMessage = "boom",
                ErrorData = "detail",
                StackTrace = new List<string> { "frame-1", "frame-2" }
            }
        };

        var input = SdkOperationUpdateMapper.ToInput(update);

        Assert.NotNull(input.Error);
        Assert.Equal("TestEx", input.Error!.ErrorType);
        Assert.Equal("boom", input.Error.ErrorMessage);
        Assert.Equal("detail", input.Error.ErrorData);
        Assert.Equal(new[] { "frame-1", "frame-2" }, input.Error.StackTrace);
    }

    [Fact]
    public void ToInput_NullOptionalsMapToNull()
    {
        var update = new OperationUpdate { Id = "op-1", Type = OperationTypes.Step, Action = OperationAction.START };

        var input = SdkOperationUpdateMapper.ToInput(update);

        Assert.Null(input.Error);
        Assert.Null(input.NextAttemptDelaySeconds);
        Assert.Null(input.WaitSeconds);
        Assert.Null(input.ChainedInvokeFunctionName);
    }
}
