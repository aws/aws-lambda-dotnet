// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using Amazon.Lambda;
using Amazon.Lambda.DurableExecution.Testing;
using Amazon.Lambda.Model;
using Xunit;

namespace Amazon.Lambda.DurableExecution.Testing.Tests;

public class CloudDurableTestRunnerTests
{
    private const string FunctionArn = "arn:aws:lambda:us-east-1:123456789012:function:my-durable-fn:$LATEST";
    private const string ExecutionArn = "arn:aws:lambda:us-east-1:123456789012:function:my-durable-fn:$LATEST/durable-execution/exec123/run456";

    [Fact]
    public async Task StartAsync_EventInvokes_AndResolvesArnByName()
    {
        var mockClient = new MockCloudLambdaClient();
        InvokeRequest? captured = null;
        mockClient.InvokeHandler = req =>
        {
            captured = req;
            return new InvokeResponse { StatusCode = 202 };
        };
        // The execution becomes listable under the minted name; return its ARN.
        mockClient.ListByFunctionHandler = req => new ListDurableExecutionsByFunctionResponse
        {
            DurableExecutions = new List<Execution>
            {
                new() { DurableExecutionName = req.DurableExecutionName, DurableExecutionArn = ExecutionArn }
            }
        };

        await using var runner = new CloudDurableTestRunner<string, string>(
            FunctionArn, mockClient,
            new CloudTestRunnerOptions { PollInterval = TimeSpan.FromMilliseconds(10) });

        var arn = await runner.StartAsync("test-input");

        Assert.Equal(ExecutionArn, arn);
        // A callback workflow would deadlock on a synchronous invoke — StartAsync
        // must fire-and-forget (Event) and carry a DurableExecutionName.
        Assert.NotNull(captured);
        Assert.Equal(InvocationType.Event, captured!.InvocationType);
        Assert.False(string.IsNullOrEmpty(captured.DurableExecutionName));
    }

    [Fact]
    public async Task StartAsync_PollsListing_UntilExecutionVisible()
    {
        var mockClient = new MockCloudLambdaClient();
        mockClient.InvokeHandler = _ => new InvokeResponse { StatusCode = 202 };

        var listCount = 0;
        mockClient.ListByFunctionHandler = req =>
        {
            listCount++;
            // Eventually-consistent listing: empty until the third poll.
            if (listCount < 3)
                return new ListDurableExecutionsByFunctionResponse();
            return new ListDurableExecutionsByFunctionResponse
            {
                DurableExecutions = new List<Execution>
                {
                    new() { DurableExecutionName = req.DurableExecutionName, DurableExecutionArn = ExecutionArn }
                }
            };
        };

        await using var runner = new CloudDurableTestRunner<string, string>(
            FunctionArn, mockClient,
            new CloudTestRunnerOptions { PollInterval = TimeSpan.FromMilliseconds(10) });

        var arn = await runner.StartAsync("test", timeout: TimeSpan.FromSeconds(5));

        Assert.Equal(ExecutionArn, arn);
        Assert.True(listCount >= 3);
    }

    [Fact]
    public async Task WaitForResultAsync_PollsUntilTerminal_AndReturnsTypedResult()
    {
        var mockClient = new MockCloudLambdaClient();
        var pollCount = 0;
        mockClient.GetDurableExecutionHandler = _ =>
        {
            pollCount++;
            if (pollCount < 3)
                return new GetDurableExecutionResponse { Status = ExecutionStatus.RUNNING };

            return new GetDurableExecutionResponse
            {
                Status = ExecutionStatus.SUCCEEDED,
                Result = """{"Value":"hello"}""",
            };
        };
        mockClient.GetHistoryHandler = _ => new GetDurableExecutionHistoryResponse
        {
            Events = new List<Event>
            {
                new() { Id = "exec-0", EventType = EventType.ExecutionStarted },
                new() { Id = "op-1", EventType = EventType.StepStarted, Name = "step1" },
                new()
                {
                    Id = "op-1",
                    EventType = EventType.StepSucceeded,
                    Name = "step1",
                    StepSucceededDetails = new StepSucceededDetails
                    {
                        Result = new EventResult { Payload = """{"Value":"hello"}""" }
                    }
                }
            }
        };

        await using var runner = new CloudDurableTestRunner<string, ResultPayload>(
            FunctionArn, mockClient,
            new CloudTestRunnerOptions { PollInterval = TimeSpan.FromMilliseconds(10) });

        var result = await runner.WaitForResultAsync(ExecutionArn, timeout: TimeSpan.FromSeconds(5));

        Assert.Equal(InvocationStatus.Succeeded, result.Status);
        Assert.True(result.IsSucceeded);
        Assert.Null(result.InvocationCount);
        Assert.True(pollCount >= 3);
        // Result is now populated from GetDurableExecution().Result (regression: B3).
        Assert.NotNull(result.Result);
        Assert.Equal("hello", result.Result!.Value);
        Assert.Single(result.Steps);
        Assert.Equal("step1", result.Steps[0].Name);
    }

    [Fact]
    public async Task WaitForResultAsync_FailedExecution_PopulatesError()
    {
        var mockClient = new MockCloudLambdaClient();
        mockClient.GetDurableExecutionHandler = _ => new GetDurableExecutionResponse
        {
            Status = ExecutionStatus.FAILED,
            Error = new Amazon.Lambda.Model.ErrorObject
            {
                ErrorType = "MyException",
                ErrorMessage = "it failed",
                ErrorData = "extra",
            },
        };

        await using var runner = new CloudDurableTestRunner<string, string>(
            FunctionArn, mockClient,
            new CloudTestRunnerOptions { PollInterval = TimeSpan.FromMilliseconds(10) });

        var result = await runner.WaitForResultAsync(ExecutionArn, timeout: TimeSpan.FromSeconds(5));

        Assert.Equal(InvocationStatus.Failed, result.Status);
        Assert.True(result.IsFailed);
        // Error is now sourced from GetDurableExecution().Error (regression: B5).
        Assert.NotNull(result.Error);
        Assert.Equal("MyException", result.Error!.ErrorType);
        Assert.Equal("it failed", result.Error.ErrorMessage);
        Assert.Equal("extra", result.Error.ErrorData);
    }

    [Theory]
    [InlineData("TIMED_OUT")]
    [InlineData("STOPPED")]
    public async Task WaitForResultAsync_TerminalNonFailedStatuses_MapToFailed(string status)
    {
        var mockClient = new MockCloudLambdaClient();
        mockClient.GetDurableExecutionHandler = _ => new GetDurableExecutionResponse
        {
            Status = ExecutionStatus.FindValue(status),
        };

        await using var runner = new CloudDurableTestRunner<string, string>(
            FunctionArn, mockClient,
            new CloudTestRunnerOptions { PollInterval = TimeSpan.FromMilliseconds(10) });

        // Regression (I5): TIMED_OUT/STOPPED are terminal — they must NOT poll to timeout.
        var result = await runner.WaitForResultAsync(ExecutionArn, timeout: TimeSpan.FromSeconds(2));
        Assert.Equal(InvocationStatus.Failed, result.Status);
    }

    [Fact]
    public async Task WaitForResultAsync_Timeout_Throws()
    {
        var mockClient = new MockCloudLambdaClient();
        mockClient.GetDurableExecutionHandler = _ => new GetDurableExecutionResponse
        {
            Status = ExecutionStatus.RUNNING,
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
        mockClient.GetHistoryHandler = _ => new GetDurableExecutionHistoryResponse
        {
            Events = new List<Event>
            {
                new() { Id = "exec-0", EventType = EventType.ExecutionStarted },
                new()
                {
                    Id = "op-cb",
                    EventType = EventType.CallbackStarted,
                    Name = "approval-callback",
                    CallbackStartedDetails = new CallbackStartedDetails { CallbackId = "Y2IxMjM0NQ==" }
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

    [Fact]
    public async Task SendCallbackFailureAsync_MapsAllErrorFields()
    {
        var mockClient = new MockCloudLambdaClient();
        await using var runner = new CloudDurableTestRunner<string, string>(
            FunctionArn, mockClient);

        await runner.SendCallbackFailureAsync("cb-1", new ErrorObject
        {
            ErrorType = "Rejected",
            ErrorMessage = "nope",
            ErrorData = "payload",
            StackTrace = new[] { "frame-1", "frame-2" },
        });

        // Regression (I8): StackTrace/ErrorData must round-trip to the SDK request.
        Assert.Single(mockClient.CallbackFailureCalls);
        var sent = mockClient.CallbackFailureCalls[0].Error;
        Assert.NotNull(sent);
        Assert.Equal("Rejected", sent!.ErrorType);
        Assert.Equal("nope", sent.ErrorMessage);
        Assert.Equal("payload", sent.ErrorData);
        Assert.Equal(new[] { "frame-1", "frame-2" }, sent.StackTrace);
    }

    public sealed class ResultPayload
    {
        public string? Value { get; set; }
    }

    /// <summary>
    /// Minimal mock of IAmazonLambda for cloud runner tests.
    /// Subclasses AmazonLambdaClient to override relevant methods.
    /// </summary>
    private sealed class MockCloudLambdaClient : AmazonLambdaClient
    {
        public Func<InvokeRequest, InvokeResponse>? InvokeHandler { get; set; }
        public Func<GetDurableExecutionStateRequest, GetDurableExecutionStateResponse>? GetExecutionStateHandler { get; set; }
        public Func<GetDurableExecutionHistoryRequest, GetDurableExecutionHistoryResponse>? GetHistoryHandler { get; set; }
        public Func<ListDurableExecutionsByFunctionRequest, ListDurableExecutionsByFunctionResponse>? ListByFunctionHandler { get; set; }
        public Func<GetDurableExecutionRequest, GetDurableExecutionResponse>? GetDurableExecutionHandler { get; set; }
        public List<SendDurableExecutionCallbackSuccessRequest> CallbackSuccessCalls { get; } = new();
        public List<SendDurableExecutionCallbackFailureRequest> CallbackFailureCalls { get; } = new();

        public MockCloudLambdaClient() : base("fake-key", "fake-secret", Amazon.RegionEndpoint.USEast1) { }

        public override Task<InvokeResponse> InvokeAsync(InvokeRequest request, CancellationToken ct = default)
        {
            if (InvokeHandler is null)
                throw new InvalidOperationException("InvokeHandler not configured");
            return Task.FromResult(InvokeHandler(request));
        }

        public override Task<GetDurableExecutionResponse> GetDurableExecutionAsync(
            GetDurableExecutionRequest request, CancellationToken ct = default)
        {
            if (GetDurableExecutionHandler is null)
                return Task.FromResult(new GetDurableExecutionResponse { Status = ExecutionStatus.RUNNING });
            return Task.FromResult(GetDurableExecutionHandler(request));
        }

        public override Task<GetDurableExecutionStateResponse> GetDurableExecutionStateAsync(
            GetDurableExecutionStateRequest request, CancellationToken ct = default)
        {
            if (GetExecutionStateHandler is null)
                return Task.FromResult(new GetDurableExecutionStateResponse());
            return Task.FromResult(GetExecutionStateHandler(request));
        }

        public override Task<GetDurableExecutionHistoryResponse> GetDurableExecutionHistoryAsync(
            GetDurableExecutionHistoryRequest request, CancellationToken ct = default)
        {
            if (GetHistoryHandler is null)
                return Task.FromResult(new GetDurableExecutionHistoryResponse());
            return Task.FromResult(GetHistoryHandler(request));
        }

        public override Task<ListDurableExecutionsByFunctionResponse> ListDurableExecutionsByFunctionAsync(
            ListDurableExecutionsByFunctionRequest request, CancellationToken ct = default)
        {
            if (ListByFunctionHandler is null)
                return Task.FromResult(new ListDurableExecutionsByFunctionResponse());
            return Task.FromResult(ListByFunctionHandler(request));
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
            CallbackFailureCalls.Add(request);
            return Task.FromResult(new SendDurableExecutionCallbackFailureResponse());
        }

        public override Task<SendDurableExecutionCallbackHeartbeatResponse> SendDurableExecutionCallbackHeartbeatAsync(
            SendDurableExecutionCallbackHeartbeatRequest request, CancellationToken ct = default)
        {
            return Task.FromResult(new SendDurableExecutionCallbackHeartbeatResponse());
        }
    }
}
