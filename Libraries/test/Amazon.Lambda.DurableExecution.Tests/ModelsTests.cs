// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.DurableExecution.Internal;
using Xunit;

namespace Amazon.Lambda.DurableExecution.Tests;

public class ModelsTests
{
    [Fact]
    public void Operation_PropertiesAssignable()
    {
        var op = new Operation
        {
            Id = "op-1",
            Type = OperationTypes.Step,
            Status = OperationStatuses.Succeeded,
            Name = "fetch_user",
            StepDetails = new StepDetails { Result = "{\"name\":\"Alice\"}" }
        };

        Assert.Equal("op-1", op.Id);
        Assert.Equal(OperationTypes.Step, op.Type);
        Assert.Equal(OperationStatuses.Succeeded, op.Status);
        Assert.Equal("fetch_user", op.Name);
        Assert.Equal("{\"name\":\"Alice\"}", op.StepDetails?.Result);
    }

    [Fact]
    public void Operation_WaitWithScheduledEndTimestamp()
    {
        var op = new Operation
        {
            Id = "op-2",
            Type = OperationTypes.Wait,
            Status = OperationStatuses.Pending,
            Name = "cooldown",
            WaitDetails = new WaitDetails
            {
                ScheduledEndTimestamp = 1767268830000L // 2026-01-01T12:00:30Z in ms
            }
        };

        Assert.Equal(OperationTypes.Wait, op.Type);
        Assert.Equal(1767268830000L, op.WaitDetails?.ScheduledEndTimestamp);
    }

    [Fact]
    public void ErrorObject_FromException()
    {
        var ex = new InvalidOperationException("something went wrong");
        var error = ErrorObject.FromException(ex);

        Assert.Equal("System.InvalidOperationException", error.ErrorType);
        Assert.Equal("something went wrong", error.ErrorMessage);
    }

    [Fact]
    public void ErrorObject_FromException_UnwrapsStepException()
    {
        // A failing user step gets wrapped as StepException carrying the original
        // ErrorType. Recording the wrapper's type would lose the user-facing
        // exception identity across a chained-invoke boundary, so FromException
        // pulls the original error fields through.
        var ex = new StepException("intentional child failure")
        {
            ErrorType = "System.InvalidOperationException",
            ErrorData = "{\"hint\":\"data\"}",
            OriginalStackTrace = new[] { "at User.Workflow.Body()" }
        };

        var error = ErrorObject.FromException(ex);

        Assert.Equal("System.InvalidOperationException", error.ErrorType);
        Assert.Equal("intentional child failure", error.ErrorMessage);
        Assert.Equal("{\"hint\":\"data\"}", error.ErrorData);
        Assert.Equal(new[] { "at User.Workflow.Body()" }, error.StackTrace);
    }

    [Fact]
    public void ErrorObject_FromException_UnwrapsChildContextException()
    {
        var ex = new ChildContextException("child failed")
        {
            ErrorType = "System.ArgumentException",
            ErrorData = "{\"k\":\"v\"}",
            OriginalStackTrace = new[] { "at Inner()" }
        };

        var error = ErrorObject.FromException(ex);

        Assert.Equal("System.ArgumentException", error.ErrorType);
        Assert.Equal("child failed", error.ErrorMessage);
        Assert.Equal("{\"k\":\"v\"}", error.ErrorData);
    }

    [Fact]
    public void ErrorObject_FromException_UnwrapsInvokeException()
    {
        var ex = new InvokeFailedException("downstream failed")
        {
            FunctionName = "arn:aws:lambda:...:function:downstream",
            ErrorType = "System.TimeoutException",
            ErrorData = "{\"region\":\"us-east-1\"}",
            OriginalStackTrace = new[] { "at Downstream.Run()" }
        };

        var error = ErrorObject.FromException(ex);

        Assert.Equal("System.TimeoutException", error.ErrorType);
        Assert.Equal("downstream failed", error.ErrorMessage);
        Assert.Equal("{\"region\":\"us-east-1\"}", error.ErrorData);
    }

    [Fact]
    public void ErrorObject_FromException_UnwrapsCallbackException()
    {
        var ex = new CallbackFailedException("callback failed")
        {
            CallbackId = "cb-123",
            ErrorType = "Acme.Errors.PaymentDeclined",
            ErrorData = "{\"code\":42}",
            OriginalStackTrace = new[] { "at External.Reject()" }
        };

        var error = ErrorObject.FromException(ex);

        Assert.Equal("Acme.Errors.PaymentDeclined", error.ErrorType);
        Assert.Equal("callback failed", error.ErrorMessage);
        Assert.Equal("{\"code\":42}", error.ErrorData);
    }

    [Fact]
    public void ErrorObject_FromException_UnwrapsStepException_WithNullErrorType()
    {
        // StepException without an explicit ErrorType (e.g., constructed by code
        // that didn't set the init-only property) records null rather than
        // falling back to the wrapper's type — the wrapper type is never useful.
        var ex = new StepException("no type set");

        var error = ErrorObject.FromException(ex);

        Assert.Null(error.ErrorType);
        Assert.Equal("no type set", error.ErrorMessage);
    }

    [Fact]
    public void ErrorObject_RoundTripSerialization()
    {
        var error = new ErrorObject
        {
            ErrorType = "System.TimeoutException",
            ErrorMessage = "timed out",
            StackTrace = new[] { "at Foo.Bar()", "at Baz.Qux()" },
            ErrorData = "{\"key\":\"value\"}"
        };

        var json = JsonSerializer.Serialize(error);
        var deserialized = JsonSerializer.Deserialize<ErrorObject>(json)!;

        Assert.Equal("System.TimeoutException", deserialized.ErrorType);
        Assert.Equal("timed out", deserialized.ErrorMessage);
        Assert.Equal(2, deserialized.StackTrace!.Count);
        Assert.Equal("{\"key\":\"value\"}", deserialized.ErrorData);
    }

    [Fact]
    public void DurableExecutionInvocationInput_Deserialization()
    {
        var json = """
        {
            "DurableExecutionArn": "arn:aws:lambda:us-east-1:123:durable-execution:abc",
            "CheckpointToken": "token-1",
            "InitialExecutionState": {
                "Operations": [
                    {
                        "Id": "exec-1",
                        "Type": "EXECUTION",
                        "Status": "STARTED",
                        "ExecutionDetails": {
                            "InputPayload": "{\"orderId\":\"order-123\",\"amount\":99.99}"
                        }
                    },
                    {
                        "Id": "op-1",
                        "Type": "STEP",
                        "Status": "SUCCEEDED",
                        "Name": "validate",
                        "StepDetails": {
                            "Result": "true"
                        }
                    }
                ]
            }
        }
        """;

        var input = JsonSerializer.Deserialize<DurableExecutionInvocationInput>(json)!;

        Assert.Equal("arn:aws:lambda:us-east-1:123:durable-execution:abc", input.DurableExecutionArn);
        Assert.Equal("token-1", input.CheckpointToken);
        Assert.NotNull(input.InitialExecutionState);
        Assert.Equal(2, input.InitialExecutionState!.Operations!.Count);

        var stepOp = input.InitialExecutionState.Operations![1];
        Assert.Equal("op-1", stepOp.Id);
        Assert.Equal(OperationTypes.Step, stepOp.Type);
        Assert.Equal("true", stepOp.StepDetails?.Result);

        // The EXECUTION operation carries the user payload in ExecutionDetails.InputPayload.
        var execOp = input.InitialExecutionState.Operations[0];
        Assert.Equal(OperationTypes.Execution, execOp.Type);
        var payload = JsonSerializer.Deserialize<TestOrderEvent>(execOp.ExecutionDetails!.InputPayload!);
        Assert.Equal("order-123", payload!.OrderId);
        Assert.Equal(99.99m, payload.Amount);
    }

    [Fact]
    public void DurableExecutionInvocationInput_NoExecutionOp_HasNullPayload()
    {
        var input = new DurableExecutionInvocationInput
        {
            DurableExecutionArn = "arn:test"
        };

        // No InitialExecutionState means no EXECUTION operation and thus no user payload
        Assert.Null(input.InitialExecutionState);
    }

    [Fact]
    public void DurableExecutionInvocationOutput_Succeeded()
    {
        var output = new DurableExecutionInvocationOutput
        {
            Status = InvocationStatus.Succeeded,
            Result = "{\"status\":\"approved\"}"
        };

        var json = JsonSerializer.Serialize(output);
        var deserialized = JsonSerializer.Deserialize<DurableExecutionInvocationOutput>(json)!;

        Assert.Equal(InvocationStatus.Succeeded, deserialized.Status);
        Assert.Equal("{\"status\":\"approved\"}", deserialized.Result);
    }

    [Fact]
    public void DurableExecutionInvocationOutput_Failed()
    {
        var output = new DurableExecutionInvocationOutput
        {
            Status = InvocationStatus.Failed,
            Error = new ErrorObject
            {
                ErrorMessage = "step failed",
                ErrorType = "StepException"
            }
        };

        var json = JsonSerializer.Serialize(output);
        var deserialized = JsonSerializer.Deserialize<DurableExecutionInvocationOutput>(json)!;

        Assert.Equal(InvocationStatus.Failed, deserialized.Status);
        Assert.NotNull(deserialized.Error);
        Assert.Equal("step failed", deserialized.Error!.ErrorMessage);
        Assert.Equal("StepException", deserialized.Error.ErrorType);
    }

    [Fact]
    public void DurableExecutionInvocationOutput_Pending()
    {
        var output = new DurableExecutionInvocationOutput
        {
            Status = InvocationStatus.Pending
        };

        var json = JsonSerializer.Serialize(output);
        var deserialized = JsonSerializer.Deserialize<DurableExecutionInvocationOutput>(json)!;

        Assert.Equal(InvocationStatus.Pending, deserialized.Status);
        Assert.Null(deserialized.Result);
        Assert.Null(deserialized.Error);
    }

    private class TestOrderEvent
    {
        [System.Text.Json.Serialization.JsonPropertyName("orderId")]
        public string? OrderId { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("amount")]
        public decimal Amount { get; set; }
    }
}
