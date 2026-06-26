// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.DurableExecution.Testing;
using Amazon.Lambda.Serialization.SystemTextJson;
using Xunit;

namespace Amazon.Lambda.DurableExecution.Testing.Tests;

public class ExceptionTests
{
    private static readonly DefaultLambdaJsonSerializer Serializer = new();

    [Fact]
    public void TestExecutionFailedException_ContainsStatusAndError()
    {
        var error = new ErrorObject { ErrorType = "MyException", ErrorMessage = "it failed" };
        var steps = new[]
        {
            new TestStep(new Operation { Id = "op-1", Type = OperationTypes.Step, Name = "step1" }, Serializer)
        };

        var ex = new TestExecutionFailedException(InvocationStatus.Failed, error, steps);

        Assert.Equal(InvocationStatus.Failed, ex.FinalStatus);
        Assert.Same(error, ex.FailureError);
        Assert.Single(ex.Steps);
        Assert.Contains("MyException", ex.Message);
        Assert.Contains("it failed", ex.Message);
        Assert.Contains("Failed", ex.Message);
    }

    [Fact]
    public void TestExecutionFailedException_NullError_StillFormatsMessage()
    {
        var ex = new TestExecutionFailedException(
            InvocationStatus.Pending, null, Array.Empty<TestStep>());

        Assert.Equal(InvocationStatus.Pending, ex.FinalStatus);
        Assert.Null(ex.FailureError);
        Assert.Contains("Pending", ex.Message);
    }

    [Fact]
    public void TestExecutionLimitException_ContainsDiagnostics()
    {
        var ex = new TestExecutionLimitException(100, 47);

        Assert.Equal(100, ex.MaxInvocations);
        Assert.Equal(47, ex.TotalOperations);
        Assert.Contains("100", ex.Message);
        Assert.Contains("47", ex.Message);
        Assert.Contains("WaitForCallbackAsync", ex.Message);
        Assert.Contains("RegisterFunction", ex.Message);
        Assert.Contains("WaitForConditionAsync", ex.Message);
    }

    [Fact]
    public void UnregisteredSiblingFunctionException_ContainsFunctionName()
    {
        var ex = new UnregisteredSiblingFunctionException("process-payment");

        Assert.Equal("process-payment", ex.FunctionName);
        Assert.Contains("process-payment", ex.Message);
        Assert.Contains("RegisterFunction", ex.Message);
        Assert.Contains("RegisterDurableFunction", ex.Message);
    }

    [Fact]
    public void CloudTestException_BasicConstruction()
    {
        var ex = new CloudTestException("no ARN in response");
        Assert.Equal("no ARN in response", ex.Message);
        Assert.Null(ex.InnerException);
    }

    [Fact]
    public void CloudTestException_WithInnerException()
    {
        var inner = new InvalidOperationException("underlying");
        var ex = new CloudTestException("wrapper message", inner);
        Assert.Equal("wrapper message", ex.Message);
        Assert.Same(inner, ex.InnerException);
    }
}
