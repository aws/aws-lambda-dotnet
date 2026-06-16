// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using System.Text.Json;
using Amazon.Lambda;
using Amazon.Lambda.DurableExecution.Testing;
using Amazon.Lambda.Model;
using Xunit;

namespace Amazon.Lambda.DurableExecution.Testing.Tests;

public class CloudDurableTestRunnerTests
{
    private const string FunctionArn = "arn:aws:lambda:us-east-1:123456789012:function:my-durable-fn:$LATEST";
    private const string ExecutionArn = "arn:aws:lambda:us-east-1:123456789012:execution:my-durable-fn:exec-123";

    [Fact]
    public async Task StartAsync_ExtractsArn_FromResponsePayload()
    {
        var mockClient = new MockCloudLambdaClient();
        mockClient.InvokeHandler = _ => new InvokeResponse
        {
            StatusCode = 200,
            Payload = new MemoryStream(Encoding.UTF8.GetBytes(
                JsonSerializer.Serialize(new { DurableExecutionArn = ExecutionArn })))
        };

        await using var runner = new CloudDurableTestRunner<string, string>(
            FunctionArn, mockClient);

        var arn = await runner.StartAsync("test-input");
        Assert.Equal(ExecutionArn, arn);
    }

    [Fact]
    public async Task StartAsync_ThrowsCloudTestException_WhenNoArn()
    {
        var mockClient = new MockCloudLambdaClient();
        mockClient.InvokeHandler = _ => new InvokeResponse
        {
            StatusCode = 200,
            Payload = new MemoryStream(Encoding.UTF8.GetBytes("{}"))
        };

        await using var runner = new CloudDurableTestRunner<string, string>(
            FunctionArn, mockClient);

        await Assert.ThrowsAsync<CloudTestException>(() => runner.StartAsync("test"));
    }

    [Fact]
    public async Task WaitForResultAsync_PollsUntilTerminal()
    {
        var mockClient = new MockCloudLambdaClient();
        var pollCount = 0;
        mockClient.GetExecutionStateHandler = _ =>
        {
            pollCount++;
            if (pollCount < 3)
            {
                return new GetDurableExecutionStateResponse
                {
                    Operations = new List<Amazon.Lambda.Model.Operation>
                    {
                        new() { Id = "exec-0", Type = "EXECUTION", Status = "STARTED" }
                    }
                };
            }

            return new GetDurableExecutionStateResponse
            {
                Operations = new List<Amazon.Lambda.Model.Operation>
                {
                    new()
                    {
                        Id = "exec-0",
                        Type = "EXECUTION",
                        Status = "SUCCEEDED",
                        ExecutionDetails = new Amazon.Lambda.Model.ExecutionDetails { InputPayload = """{"x":1}""" }
                    },
                    new()
                    {
                        Id = "op-1",
                        Type = "STEP",
                        Status = "SUCCEEDED",
                        Name = "step1",
                        StepDetails = new Amazon.Lambda.Model.StepDetails { Result = """{"Value":"hello"}""" }
                    }
                }
            };
        };

        await using var runner = new CloudDurableTestRunner<string, string>(
            FunctionArn, mockClient,
            new CloudTestRunnerOptions { PollInterval = TimeSpan.FromMilliseconds(10) });

        var result = await runner.WaitForResultAsync(ExecutionArn, timeout: TimeSpan.FromSeconds(5));

        Assert.Equal(InvocationStatus.Succeeded, result.Status);
        Assert.Equal(-1, result.InvocationCount);
        Assert.True(pollCount >= 3);
        Assert.Single(result.Steps);
        Assert.Equal("step1", result.Steps[0].Name);
    }

    [Fact]
    public async Task WaitForResultAsync_Timeout_Throws()
    {
        var mockClient = new MockCloudLambdaClient();
        mockClient.GetExecutionStateHandler = _ => new GetDurableExecutionStateResponse
        {
            Operations = new List<Amazon.Lambda.Model.Operation>
            {
                new() { Id = "exec-0", Type = "EXECUTION", Status = "STARTED" }
            }
        };

        await using var runner = new CloudDurableTestRunner<string, string>(
            FunctionArn, mockClient,
            new CloudTestRunnerOptions { PollInterval = TimeSpan.FromMilliseconds(10) });

        var ex = await Assert.ThrowsAnyAsync<Exception>(
            () => runner.WaitForResultAsync(ExecutionArn, timeout: TimeSpan.FromMilliseconds(50)));

        // Cancellation surfaces as OperationCanceledException or wrapped in DurableExecutionException
        Assert.True(
            ex is OperationCanceledException ||
            (ex is DurableExecutionException dee && dee.InnerException is OperationCanceledException),
            $"Expected cancellation exception but got: {ex.GetType().Name}: {ex.Message}");
    }

    [Fact]
    public async Task WaitForCallbackAsync_FindsCallbackFromPolledState()
    {
        var mockClient = new MockCloudLambdaClient();
        mockClient.GetExecutionStateHandler = _ => new GetDurableExecutionStateResponse
        {
            Operations = new List<Amazon.Lambda.Model.Operation>
            {
                new() { Id = "exec-0", Type = "EXECUTION", Status = "STARTED" },
                new()
                {
                    Id = "op-cb",
                    Type = "CALLBACK",
                    Status = "STARTED",
                    Name = "approval-callback",
                    CallbackDetails = new Amazon.Lambda.Model.CallbackDetails { CallbackId = "Y2IxMjM0NQ==" }
                }
            }
        };

        await using var runner = new CloudDurableTestRunner<string, string>(
            FunctionArn, mockClient,
            new CloudTestRunnerOptions { PollInterval = TimeSpan.FromMilliseconds(10) });

        var cbId = await runner.WaitForCallbackAsync(ExecutionArn, name: "approval", timeout: TimeSpan.FromSeconds(2));
        Assert.Equal("Y2IxMjM0NQ==", cbId);
    }

    [Fact]
    public async Task SendCallbackSuccessAsync_CallsLambdaApi()
    {
        var mockClient = new MockCloudLambdaClient();
        await using var runner = new CloudDurableTestRunner<string, string>(
            FunctionArn, mockClient);

        await runner.SendCallbackSuccessAsync("Y2IxMjM=", "approved");

        Assert.Single(mockClient.CallbackSuccessCalls);
        Assert.Equal("Y2IxMjM=", mockClient.CallbackSuccessCalls[0].CallbackId);
    }

    /// <summary>
    /// Minimal mock of IAmazonLambda for cloud runner tests.
    /// Subclasses AmazonLambdaClient to override relevant methods.
    /// </summary>
    private sealed class MockCloudLambdaClient : AmazonLambdaClient
    {
        public Func<InvokeRequest, InvokeResponse>? InvokeHandler { get; set; }
        public Func<GetDurableExecutionStateRequest, GetDurableExecutionStateResponse>? GetExecutionStateHandler { get; set; }
        public List<SendDurableExecutionCallbackSuccessRequest> CallbackSuccessCalls { get; } = new();

        public MockCloudLambdaClient() : base("fake-key", "fake-secret", Amazon.RegionEndpoint.USEast1) { }

        public override Task<InvokeResponse> InvokeAsync(InvokeRequest request, CancellationToken ct = default)
        {
            if (InvokeHandler is null)
                throw new InvalidOperationException("InvokeHandler not configured");
            return Task.FromResult(InvokeHandler(request));
        }

        public override Task<GetDurableExecutionStateResponse> GetDurableExecutionStateAsync(
            GetDurableExecutionStateRequest request, CancellationToken ct = default)
        {
            if (GetExecutionStateHandler is null)
                return Task.FromResult(new GetDurableExecutionStateResponse());
            return Task.FromResult(GetExecutionStateHandler(request));
        }

        public override Task<SendDurableExecutionCallbackSuccessResponse> SendDurableExecutionCallbackSuccessAsync(
            SendDurableExecutionCallbackSuccessRequest request, CancellationToken ct = default)
        {
            CallbackSuccessCalls.Add(request);
            return Task.FromResult(new SendDurableExecutionCallbackSuccessResponse());
        }

        public override Task<SendDurableExecutionCallbackFailureResponse> SendDurableExecutionCallbackFailureAsync(
            SendDurableExecutionCallbackFailureRequest request, CancellationToken ct = default)
        {
            return Task.FromResult(new SendDurableExecutionCallbackFailureResponse());
        }

        public override Task<SendDurableExecutionCallbackHeartbeatResponse> SendDurableExecutionCallbackHeartbeatAsync(
            SendDurableExecutionCallbackHeartbeatRequest request, CancellationToken ct = default)
        {
            return Task.FromResult(new SendDurableExecutionCallbackHeartbeatResponse());
        }
    }
}
