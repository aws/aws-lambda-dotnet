// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.DurableExecution.Services;
using Amazon.Lambda.Model;
using SdkErrorObject = Amazon.Lambda.Model.ErrorObject;
using Xunit;

namespace Amazon.Lambda.DurableExecution.Tests;

public class LambdaDurableServiceClientTests
{
    [Fact]
    public async Task CheckpointAsync_EmptyOperations_NoApiCallReturnsToken()
    {
        var mockClient = new MockLambdaClient();
        var client = new LambdaDurableServiceClient(mockClient);

        var token = await client.CheckpointAsync(
            "arn:aws:lambda:us-east-1:123:durable-execution:e1",
            "input-token",
            Array.Empty<OperationUpdate>());

        Assert.Equal("input-token", token);
        Assert.Empty(mockClient.CheckpointCalls);
    }

    [Fact]
    public async Task CheckpointAsync_NullCheckpointToken_SendsEmptyString()
    {
        var mockClient = new MockLambdaClient();
        var client = new LambdaDurableServiceClient(mockClient);

        await client.CheckpointAsync(
            "arn:aws:lambda:us-east-1:123:durable-execution:e1",
            checkpointToken: null,
            new[]
            {
                new OperationUpdate
                {
                    Id = "0-step",
                    Type = "STEP",
                    Action = "SUCCEED",
                    SubType = "Step",
                    Name = "do_thing",
                    Payload = "\"ok\""
                }
            });

        var call = Assert.Single(mockClient.CheckpointCalls);
        Assert.Equal("", call.CheckpointToken);
    }

    [Fact]
    public async Task CheckpointAsync_StepWithError_PropagatesError()
    {
        var mockClient = new MockLambdaClient();
        var client = new LambdaDurableServiceClient(mockClient);

        await client.CheckpointAsync(
            "arn:aws:lambda:us-east-1:123:durable-execution:e1",
            "tok",
            new[]
            {
                new OperationUpdate
                {
                    Id = "0-bad",
                    Type = "STEP",
                    Action = "FAIL",
                    SubType = "Step",
                    Name = "bad",
                    Error = new SdkErrorObject
                    {
                        ErrorType = "System.TimeoutException",
                        ErrorMessage = "timed out",
                        ErrorData = "{\"detail\":\"x\"}",
                        StackTrace = new List<string> { "at A.B()", "at C.D()" }
                    }
                }
            });

        var call = Assert.Single(mockClient.CheckpointCalls);
        var update = Assert.Single(call.Updates);
        Assert.Equal("STEP", update.Type);
        Assert.Equal("FAIL", update.Action);
        Assert.NotNull(update.Error);
        Assert.Equal("System.TimeoutException", update.Error.ErrorType);
        Assert.Equal("timed out", update.Error.ErrorMessage);
        Assert.Equal("{\"detail\":\"x\"}", update.Error.ErrorData);
        Assert.Equal(2, update.Error.StackTrace.Count);
    }

    [Fact]
    public async Task CheckpointAsync_WaitWithOptions_PropagatesWaitOptions()
    {
        var mockClient = new MockLambdaClient();
        var client = new LambdaDurableServiceClient(mockClient);

        await client.CheckpointAsync(
            "arn",
            "tok",
            new[]
            {
                new OperationUpdate
                {
                    Id = "0-wait",
                    Type = "WAIT",
                    Action = "START",
                    SubType = "Wait",
                    Name = "delay",
                    WaitOptions = new WaitOptions { WaitSeconds = 45 }
                }
            });

        var update = mockClient.CheckpointCalls[0].Updates[0];
        Assert.NotNull(update.WaitOptions);
        Assert.Equal(45, update.WaitOptions.WaitSeconds);
    }

    [Fact]
    public async Task CheckpointAsync_ParentIdAndPayload_ArePropagated()
    {
        var mockClient = new MockLambdaClient();
        var client = new LambdaDurableServiceClient(mockClient);

        await client.CheckpointAsync(
            "arn",
            "tok",
            new[]
            {
                new OperationUpdate
                {
                    Id = "child-1",
                    ParentId = "parent-0",
                    Type = "STEP",
                    Action = "SUCCEED",
                    SubType = "Step",
                    Payload = "{\"a\":1}"
                }
            });

        var update = mockClient.CheckpointCalls[0].Updates[0];
        Assert.Equal("parent-0", update.ParentId);
        Assert.Equal("{\"a\":1}", update.Payload);
    }

    [Fact]
    public async Task CheckpointAsync_MultipleUpdates_AllForwarded()
    {
        var mockClient = new MockLambdaClient();
        var client = new LambdaDurableServiceClient(mockClient);

        await client.CheckpointAsync(
            "arn",
            "tok",
            new[]
            {
                new OperationUpdate
                {
                    Id = "0-step",
                    Type = "STEP",
                    Action = "SUCCEED",
                    SubType = "Step",
                    Name = "validate"
                },
                new OperationUpdate
                {
                    Id = "1-wait",
                    Type = "WAIT",
                    Action = "START",
                    SubType = "Wait",
                    Name = "delay",
                    WaitOptions = new WaitOptions { WaitSeconds = 30 }
                }
            });

        var call = Assert.Single(mockClient.CheckpointCalls);
        Assert.Equal(2, call.Updates.Count);
        Assert.Equal("STEP", call.Updates[0].Type);
        Assert.Equal("WAIT", call.Updates[1].Type);
    }

    [Fact]
    public async Task GetExecutionStateAsync_CopiesContextDetailsResultAndError()
    {
        var mockClient = new MockLambdaClient
        {
            GetExecutionStateHandler = _ => new GetDurableExecutionStateResponse
            {
                Operations = new List<Amazon.Lambda.Model.Operation>
                {
                    new Amazon.Lambda.Model.Operation
                    {
                        Id = "ctx-1",
                        Type = "CONTEXT",
                        Status = "SUCCEEDED",
                        Name = "phase",
                        ContextDetails = new Amazon.Lambda.Model.ContextDetails
                        {
                            Result = "\"ok\""
                        }
                    },
                    new Amazon.Lambda.Model.Operation
                    {
                        Id = "ctx-2",
                        Type = "CONTEXT",
                        Status = "FAILED",
                        Name = "phase2",
                        ContextDetails = new Amazon.Lambda.Model.ContextDetails
                        {
                            Error = new SdkErrorObject
                            {
                                ErrorType = "System.InvalidOperationException",
                                ErrorMessage = "boom",
                                ErrorData = "{\"detail\":\"x\"}",
                                StackTrace = new List<string> { "at A.B()", "at C.D()" }
                            }
                        }
                    }
                }
            }
        };
        var client = new LambdaDurableServiceClient(mockClient);

        var (operations, _) = await client.GetExecutionStateAsync("arn", "tok", "marker");

        Assert.Equal(2, operations.Count);

        Assert.NotNull(operations[0].ContextDetails);
        Assert.Equal("\"ok\"", operations[0].ContextDetails!.Result);
        Assert.Null(operations[0].ContextDetails!.Error);

        Assert.NotNull(operations[1].ContextDetails);
        Assert.NotNull(operations[1].ContextDetails!.Error);
        Assert.Equal("System.InvalidOperationException", operations[1].ContextDetails!.Error!.ErrorType);
        Assert.Equal("boom", operations[1].ContextDetails!.Error!.ErrorMessage);
        Assert.Equal("{\"detail\":\"x\"}", operations[1].ContextDetails!.Error!.ErrorData);
        Assert.Equal(new[] { "at A.B()", "at C.D()" }, operations[1].ContextDetails!.Error!.StackTrace);
    }

    [Fact]
    public async Task GetExecutionStateAsync_CopiesStepDetailsErrorStackTraceAndErrorData()
    {
        // Round-trip safety: the SDK returns ErrorObject with all four fields,
        // and Internal.Operation must preserve them so StepException can surface
        // OriginalStackTrace / ErrorData on replay.
        var mockClient = new MockLambdaClient
        {
            GetExecutionStateHandler = _ => new GetDurableExecutionStateResponse
            {
                Operations = new List<Amazon.Lambda.Model.Operation>
                {
                    new Amazon.Lambda.Model.Operation
                    {
                        Id = "step-1",
                        Type = "STEP",
                        Status = "FAILED",
                        Name = "charge",
                        StepDetails = new Amazon.Lambda.Model.StepDetails
                        {
                            Error = new SdkErrorObject
                            {
                                ErrorType = "System.TimeoutException",
                                ErrorMessage = "timed out",
                                ErrorData = "{\"detail\":\"y\"}",
                                StackTrace = new List<string> { "at E.F()", "at G.H()" }
                            }
                        }
                    }
                }
            }
        };
        var client = new LambdaDurableServiceClient(mockClient);

        var (operations, _) = await client.GetExecutionStateAsync("arn", "tok", "marker");

        var op = Assert.Single(operations);
        Assert.NotNull(op.StepDetails);
        Assert.NotNull(op.StepDetails!.Error);
        Assert.Equal("System.TimeoutException", op.StepDetails!.Error!.ErrorType);
        Assert.Equal("timed out", op.StepDetails!.Error!.ErrorMessage);
        Assert.Equal("{\"detail\":\"y\"}", op.StepDetails!.Error!.ErrorData);
        Assert.Equal(new[] { "at E.F()", "at G.H()" }, op.StepDetails!.Error!.StackTrace);
    }

    [Fact]
    public async Task GetExecutionStateAsync_MapFromSdkOperation_RoundTripsAllErrorFields()
    {
        // Pre-existing bug guard: MapFromSdkOperation used to drop ErrorData
        // and StackTrace from the SDK error object, so the durable exception
        // builders (StepException, ChildContextException, and the
        // InvokeException tree) always saw nulls for those fields on
        // real-service replay. This test pins down the fix for all three
        // operation types that carry an error.
        var stack = new List<string> { "at Frame.One()", "at Frame.Two()" };

        var mockClient = new MockLambdaClient
        {
            GetExecutionStateHandler = _ => new GetDurableExecutionStateResponse
            {
                Operations = new List<Amazon.Lambda.Model.Operation>
                {
                    new Amazon.Lambda.Model.Operation
                    {
                        Id = "step-1",
                        Type = "STEP",
                        Status = "FAILED",
                        StepDetails = new Amazon.Lambda.Model.StepDetails
                        {
                            Error = new SdkErrorObject
                            {
                                ErrorType = "System.InvalidOperationException",
                                ErrorMessage = "step blew up",
                                ErrorData = "{\"detail\":\"step\"}",
                                StackTrace = stack
                            }
                        }
                    },
                    new Amazon.Lambda.Model.Operation
                    {
                        Id = "ctx-1",
                        Type = "CONTEXT",
                        Status = "FAILED",
                        ContextDetails = new Amazon.Lambda.Model.ContextDetails
                        {
                            Error = new SdkErrorObject
                            {
                                ErrorType = "System.ArgumentException",
                                ErrorMessage = "ctx blew up",
                                ErrorData = "{\"detail\":\"ctx\"}",
                                StackTrace = stack
                            }
                        }
                    },
                    new Amazon.Lambda.Model.Operation
                    {
                        Id = "inv-1",
                        Type = "CHAINED_INVOKE",
                        Status = "FAILED",
                        ChainedInvokeDetails = new Amazon.Lambda.Model.ChainedInvokeDetails
                        {
                            Error = new SdkErrorObject
                            {
                                ErrorType = "System.TimeoutException",
                                ErrorMessage = "invoke blew up",
                                ErrorData = "{\"detail\":\"invoke\"}",
                                StackTrace = stack
                            }
                        }
                    }
                }
            }
        };
        var client = new LambdaDurableServiceClient(mockClient);

        var (operations, _) = await client.GetExecutionStateAsync("arn", "tok", "marker");

        Assert.Equal(3, operations.Count);

        // STEP — all four fields propagate.
        var stepError = operations[0].StepDetails!.Error!;
        Assert.Equal("System.InvalidOperationException", stepError.ErrorType);
        Assert.Equal("step blew up", stepError.ErrorMessage);
        Assert.Equal("{\"detail\":\"step\"}", stepError.ErrorData);
        Assert.NotNull(stepError.StackTrace);
        Assert.Equal(new[] { "at Frame.One()", "at Frame.Two()" }, stepError.StackTrace!);

        // CHILD CONTEXT — all four fields propagate.
        var ctxError = operations[1].ContextDetails!.Error!;
        Assert.Equal("System.ArgumentException", ctxError.ErrorType);
        Assert.Equal("ctx blew up", ctxError.ErrorMessage);
        Assert.Equal("{\"detail\":\"ctx\"}", ctxError.ErrorData);
        Assert.NotNull(ctxError.StackTrace);
        Assert.Equal(new[] { "at Frame.One()", "at Frame.Two()" }, ctxError.StackTrace!);

        // CHAINED_INVOKE — all four fields propagate.
        var invError = operations[2].ChainedInvokeDetails!.Error!;
        Assert.Equal("System.TimeoutException", invError.ErrorType);
        Assert.Equal("invoke blew up", invError.ErrorMessage);
        Assert.Equal("{\"detail\":\"invoke\"}", invError.ErrorData);
        Assert.NotNull(invError.StackTrace);
        Assert.Equal(new[] { "at Frame.One()", "at Frame.Two()" }, invError.StackTrace!);
    }

    [Fact]
    public void MapFromSdkOperation_CopiesReplayChildren()
    {
        var sdkOp = new Amazon.Lambda.Model.Operation
        {
            Id = "ctx-1",
            Type = "CONTEXT",
            Status = "SUCCEEDED",
            ContextDetails = new Amazon.Lambda.Model.ContextDetails
            {
                Result = "{}",
                ReplayChildren = true
            }
        };

        var mapped = LambdaDurableServiceClient.MapFromSdkOperationForTest(sdkOp);

        Assert.True(mapped.ContextDetails!.ReplayChildren);
    }

    [Fact]
    public async Task CheckpointAsync_ReturnsNewToken()
    {
        var mockClient = new MockLambdaClient();
        var client = new LambdaDurableServiceClient(mockClient);

        var newToken = await client.CheckpointAsync(
            "arn",
            "old-token",
            new[]
            {
                new OperationUpdate
                {
                    Id = "0-x",
                    Type = "STEP",
                    Action = "SUCCEED"
                }
            });

        // MockLambdaClient returns "token-1", "token-2", etc.
        Assert.Equal("token-1", newToken);
    }
}
