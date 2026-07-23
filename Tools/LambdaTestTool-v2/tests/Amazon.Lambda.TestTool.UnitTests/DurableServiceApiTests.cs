// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.Model;
using Amazon.Lambda.TestTool.Commands.Settings;
using Amazon.Lambda.TestTool.Processes;
using Amazon.Lambda.TestTool.Tests.Common.Helpers;
using Amazon.Runtime;
using Xunit;
using Environment = System.Environment;

namespace Amazon.Lambda.TestTool.UnitTests;

/// <summary>
/// End-to-end tests for the durable-execution service emulator (Phase 1: data plane).
/// A real <see cref="AmazonLambdaClient"/> is pointed at the Test Tool host and calls the actual
/// AWSSDK durable operations (<c>CheckpointDurableExecution</c> / <c>GetDurableExecutionState</c>),
/// so the wire serialization, catch-all ARN routing, and state machine are all exercised together.
/// </summary>
[Collection(DurableTestCollection.Name)]
public class DurableServiceApiTests
{
    private const string FunctionName = "DurableFoo";
    // A realistic slash-bearing durable-execution ARN — the SDK URL-encodes the slashes and the
    // emulator's catch-all route must reconstruct it intact.
    private const string Arn =
        "arn:aws:lambda:us-east-1:000000000000:function:DurableFoo:$LATEST/durable-execution/local/exec-1";

    private static (TestToolProcess Process, CancellationTokenSource Cts) StartTool(bool skipTime = true)
    {
        var lambdaPort = TestHelpers.GetNextLambdaRuntimePort();
        var cts = new CancellationTokenSource();
        cts.CancelAfter(30_000);
        var options = new RunCommandSettings
        {
            LambdaEmulatorPort = lambdaPort,
            DurableExecution = true,
            DurableTimeSkip = skipTime
        };
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        var process = TestToolProcess.Startup(options, cts.Token);
        return (process, cts);
    }

    private static IAmazonLambda ConstructLambdaServiceClient(string url)
    {
        var config = new AmazonLambdaConfig
        {
            ServiceURL = url,
            MaxErrorRetry = 0
        };
        var credentials = new BasicAWSCredentials("accessKeyId", "secretKey");
        return new AmazonLambdaClient(credentials, config);
    }

    [Fact]
    public async Task Checkpoint_StepStartThenSucceed_RoundTripsThroughState()
    {
        var (process, cts) = StartTool();
        try
        {
            Assert.True(await TestHelpers.WaitForApiToStartAsync($"{process.ServiceUrl}/lambda-runtime-api/healthcheck"));
            var client = ConstructLambdaServiceClient(process.ServiceUrl);

            // START a step.
            var startResp = await client.CheckpointDurableExecutionAsync(new CheckpointDurableExecutionRequest
            {
                DurableExecutionArn = Arn,
                CheckpointToken = "0",
                Updates = new List<OperationUpdate>
                {
                    new()
                    {
                        Id = "op-step-1",
                        Type = OperationType.STEP,
                        Action = OperationAction.START,
                        Name = "my-step"
                    }
                }
            }, cts.Token);

            // Server rotates the token and echoes the new op back in NewExecutionState.
            Assert.NotEqual("0", startResp.CheckpointToken);
            Assert.NotNull(startResp.NewExecutionState);
            var startedOp = Assert.Single(startResp.NewExecutionState.Operations);
            Assert.Equal("op-step-1", startedOp.Id);
            Assert.Equal("STARTED", startedOp.Status.Value);
            Assert.Equal(1, startedOp.StepDetails.Attempt);

            // SUCCEED the step with a result payload, using the rotated token.
            var succeedResp = await client.CheckpointDurableExecutionAsync(new CheckpointDurableExecutionRequest
            {
                DurableExecutionArn = Arn,
                CheckpointToken = startResp.CheckpointToken,
                Updates = new List<OperationUpdate>
                {
                    new()
                    {
                        Id = "op-step-1",
                        Type = OperationType.STEP,
                        Action = OperationAction.SUCCEED,
                        Payload = "\"step-result\""
                    }
                }
            }, cts.Token);

            var succeededOp = Assert.Single(succeedResp.NewExecutionState.Operations);
            Assert.Equal("SUCCEEDED", succeededOp.Status.Value);
            Assert.Equal("\"step-result\"", succeededOp.StepDetails.Result);

            // GetState returns the full (single-op) history.
            var state = await client.GetDurableExecutionStateAsync(new GetDurableExecutionStateRequest
            {
                DurableExecutionArn = Arn,
                CheckpointToken = succeedResp.CheckpointToken,
                Marker = ""
            }, cts.Token);

            var op = Assert.Single(state.Operations);
            Assert.Equal("op-step-1", op.Id);
            Assert.Equal("SUCCEEDED", op.Status.Value);
            Assert.Equal("\"step-result\"", op.StepDetails.Result);
            Assert.Null(state.NextMarker);
        }
        finally
        {
            await cts.CancelAsync();
        }
    }

    [Fact]
    public async Task Checkpoint_WaitStart_WithTimeSkip_IsImmediatelySucceeded()
    {
        var (process, cts) = StartTool(skipTime: true);
        try
        {
            Assert.True(await TestHelpers.WaitForApiToStartAsync($"{process.ServiceUrl}/lambda-runtime-api/healthcheck"));
            var client = ConstructLambdaServiceClient(process.ServiceUrl);

            var resp = await client.CheckpointDurableExecutionAsync(new CheckpointDurableExecutionRequest
            {
                DurableExecutionArn = Arn,
                CheckpointToken = "0",
                Updates = new List<OperationUpdate>
                {
                    new()
                    {
                        Id = "op-wait-1",
                        Type = OperationType.WAIT,
                        Action = OperationAction.START,
                        WaitOptions = new WaitOptions { WaitSeconds = 3600 }
                    }
                }
            }, cts.Token);

            // Time-skip flips a WAIT START straight to SUCCEEDED so the next replay proceeds.
            var op = Assert.Single(resp.NewExecutionState.Operations);
            Assert.Equal("op-wait-1", op.Id);
            Assert.Equal("SUCCEEDED", op.Status.Value);
        }
        finally
        {
            await cts.CancelAsync();
        }
    }

    [Fact]
    public async Task Checkpoint_WaitStart_WithoutTimeSkip_StaysStartedWithScheduledEnd()
    {
        var (process, cts) = StartTool(skipTime: false);
        try
        {
            Assert.True(await TestHelpers.WaitForApiToStartAsync($"{process.ServiceUrl}/lambda-runtime-api/healthcheck"));
            var client = ConstructLambdaServiceClient(process.ServiceUrl);

            var resp = await client.CheckpointDurableExecutionAsync(new CheckpointDurableExecutionRequest
            {
                DurableExecutionArn = Arn,
                CheckpointToken = "0",
                Updates = new List<OperationUpdate>
                {
                    new()
                    {
                        Id = "op-wait-1",
                        Type = OperationType.WAIT,
                        Action = OperationAction.START,
                        WaitOptions = new WaitOptions { WaitSeconds = 3600 }
                    }
                }
            }, cts.Token);

            var op = Assert.Single(resp.NewExecutionState.Operations);
            Assert.Equal("STARTED", op.Status.Value);
            Assert.NotNull(op.WaitDetails.ScheduledEndTimestamp);
        }
        finally
        {
            await cts.CancelAsync();
        }
    }

    [Fact]
    public async Task Checkpoint_CallbackStart_MintsCallbackId()
    {
        var (process, cts) = StartTool();
        try
        {
            Assert.True(await TestHelpers.WaitForApiToStartAsync($"{process.ServiceUrl}/lambda-runtime-api/healthcheck"));
            var client = ConstructLambdaServiceClient(process.ServiceUrl);

            var resp = await client.CheckpointDurableExecutionAsync(new CheckpointDurableExecutionRequest
            {
                DurableExecutionArn = Arn,
                CheckpointToken = "0",
                Updates = new List<OperationUpdate>
                {
                    new()
                    {
                        Id = "op-cb-1",
                        Type = OperationType.CALLBACK,
                        Action = OperationAction.START
                    }
                }
            }, cts.Token);

            var op = Assert.Single(resp.NewExecutionState.Operations);
            Assert.Equal("cb-op-cb-1", op.CallbackDetails.CallbackId);
        }
        finally
        {
            await cts.CancelAsync();
        }
    }

    [Fact]
    public async Task GetState_PagesHistory_WithNextMarker()
    {
        var (process, cts) = StartTool();
        try
        {
            Assert.True(await TestHelpers.WaitForApiToStartAsync($"{process.ServiceUrl}/lambda-runtime-api/healthcheck"));
            var client = ConstructLambdaServiceClient(process.ServiceUrl);

            // Write more operations than one page (StatePageSize = 100).
            const int total = 150;
            var updates = new List<OperationUpdate>();
            for (var i = 0; i < total; i++)
            {
                updates.Add(new OperationUpdate
                {
                    Id = $"op-{i}",
                    Type = OperationType.STEP,
                    Action = OperationAction.START,
                    Name = $"step-{i}"
                });
            }
            await client.CheckpointDurableExecutionAsync(new CheckpointDurableExecutionRequest
            {
                DurableExecutionArn = Arn,
                CheckpointToken = "0",
                Updates = updates
            }, cts.Token);

            // First page: 100 ops + a NextMarker.
            var page1 = await client.GetDurableExecutionStateAsync(new GetDurableExecutionStateRequest
            {
                DurableExecutionArn = Arn,
                CheckpointToken = "1",
                Marker = ""
            }, cts.Token);
            Assert.Equal(100, page1.Operations.Count);
            Assert.False(string.IsNullOrEmpty(page1.NextMarker));

            // Second page: remaining 50 ops, no NextMarker.
            var page2 = await client.GetDurableExecutionStateAsync(new GetDurableExecutionStateRequest
            {
                DurableExecutionArn = Arn,
                CheckpointToken = "1",
                Marker = page1.NextMarker
            }, cts.Token);
            Assert.Equal(50, page2.Operations.Count);
            Assert.True(string.IsNullOrEmpty(page2.NextMarker));
        }
        finally
        {
            await cts.CancelAsync();
        }
    }

    [Fact]
    public async Task Checkpoint_OversizedStepPayload_IsRejected()
    {
        var (process, cts) = StartTool();
        try
        {
            Assert.True(await TestHelpers.WaitForApiToStartAsync($"{process.ServiceUrl}/lambda-runtime-api/healthcheck"));
            var client = ConstructLambdaServiceClient(process.ServiceUrl);

            // A STEP result over the 256 KB cap must be rejected.
            var oversized = "\"" + new string('x', 300 * 1024) + "\"";
            var ex = await Assert.ThrowsAsync<AmazonLambdaException>(async () =>
                await client.CheckpointDurableExecutionAsync(new CheckpointDurableExecutionRequest
                {
                    DurableExecutionArn = Arn,
                    CheckpointToken = "0",
                    Updates = new List<OperationUpdate>
                    {
                        new() { Id = "big", Type = OperationType.STEP, Action = OperationAction.SUCCEED, Payload = oversized }
                    }
                }, cts.Token));

            Assert.Equal(System.Net.HttpStatusCode.RequestEntityTooLarge, ex.StatusCode);
        }
        finally
        {
            await cts.CancelAsync();
        }
    }

    [Fact]
    public async Task DurableEndpoints_NotRegistered_WhenFlagDisabled()
    {
        var lambdaPort = TestHelpers.GetNextLambdaRuntimePort();
        var cts = new CancellationTokenSource();
        cts.CancelAfter(30_000);
        var options = new RunCommandSettings
        {
            LambdaEmulatorPort = lambdaPort,
            DurableExecution = false
        };
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        var process = TestToolProcess.Startup(options, cts.Token);
        try
        {
            Assert.True(await TestHelpers.WaitForApiToStartAsync($"{process.ServiceUrl}/lambda-runtime-api/healthcheck"));
            var client = ConstructLambdaServiceClient(process.ServiceUrl);

            // With the emulator off, the durable route is absent → the SDK gets a 404.
            await Assert.ThrowsAsync<AmazonLambdaException>(async () =>
                await client.CheckpointDurableExecutionAsync(new CheckpointDurableExecutionRequest
                {
                    DurableExecutionArn = Arn,
                    CheckpointToken = "0",
                    Updates = new List<OperationUpdate>
                    {
                        new() { Id = "op-1", Type = OperationType.STEP, Action = OperationAction.START }
                    }
                }, cts.Token));
        }
        finally
        {
            await cts.CancelAsync();
        }
    }
}
