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
