// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Net.Http.Json;
using System.Text.Json;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.Model;
using Amazon.Lambda.TestTool.Commands.Settings;
using Amazon.Lambda.TestTool.Processes;
using Amazon.Lambda.TestTool.Tests.Common.Helpers;
using Amazon.Runtime;
using Xunit;
using Environment = System.Environment;
using DurableOperation = Amazon.Lambda.DurableExecution.Operation;
using DurableErrorObject = Amazon.Lambda.DurableExecution.ErrorObject;

namespace Amazon.Lambda.TestTool.UnitTests;

/// <summary>
/// End-to-end tests for the Phase 2 durable-execution driver: the start hook
/// (<c>X-Amz-Durable-Execution-Name</c>), the re-invocation drive loop, and time-skipping —
/// all exercised together against a durable "function" that speaks the real wire protocol.
///
/// The fake function polls the Runtime API (next/response) exactly as a real Lambda bootstrap
/// does, and checkpoints via a real <see cref="AmazonLambdaClient"/> pointed at the Test Tool.
/// It deliberately models a wait-then-step workflow so the driver must re-invoke: invocation 1
/// starts a WAIT and returns Pending; the driver time-skips the wait and re-invokes; invocation 2
/// sees the wait completed, runs a step, and returns Succeeded. This proves the multi-invocation
/// replay cycle without depending on the DurableExecution SDK's internal engine (tested in its
/// own package) or its process-global cached checkpoint client.
/// </summary>
public class DurableExecutionDriverTests
{
    private const string FunctionName = "DurableDriverFoo";

    private static readonly JsonSerializerOptions Json = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private static (TestToolProcess Process, RunCommandSettings Options, CancellationTokenSource Cts) StartTool(bool skipTime = true)
    {
        var lambdaPort = TestHelpers.GetNextLambdaRuntimePort();
        var cts = new CancellationTokenSource();
        cts.CancelAfter(60_000);
        var options = new RunCommandSettings
        {
            LambdaEmulatorPort = lambdaPort,
            DurableExecution = true,
            DurableTimeSkip = skipTime
        };
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        var process = TestToolProcess.Startup(options, cts.Token);
        return (process, options, cts);
    }

    private static IAmazonLambda ConstructLambdaServiceClient(string url)
    {
        var config = new AmazonLambdaConfig { ServiceURL = url, MaxErrorRetry = 0 };
        var credentials = new BasicAWSCredentials("accessKeyId", "secretKey");
        return new AmazonLambdaClient(credentials, config);
    }

    /// <summary>
    /// Runs a fake durable function loop: polls the Runtime API, checkpoints a wait-then-step
    /// workflow, and returns Pending then Succeeded. Returns the number of invocations processed.
    /// </summary>
    private static async Task<int> RunFakeDurableFunctionAsync(
        string serviceUrl, RunCommandSettings options, CancellationToken ct)
    {
        // Note: no HttpClient.BaseAddress — a leading-slash relative path would replace the
        // {FunctionName} prefix and poll the default-function partition instead of the one the
        // driver queues into. Build absolute URLs against the named runtime-API partition.
        var funcBase = $"http://{options.LambdaEmulatorHost}:{options.LambdaEmulatorPort}/{FunctionName}";
        using var http = new HttpClient();
        using var lambda = ConstructLambdaServiceClient(serviceUrl);

        var invocations = 0;
        while (!ct.IsCancellationRequested)
        {
            // GET next invocation (long-polls until the driver queues an envelope).
            HttpResponseMessage next;
            try
            {
                next = await http.GetAsync($"{funcBase}/2018-06-01/runtime/invocation/next", ct);
            }
            catch (OperationCanceledException) { break; }

            if (!next.IsSuccessStatusCode)
                continue;

            var requestId = next.Headers.TryGetValues("Lambda-Runtime-Aws-Request-Id", out var ids)
                ? ids.First() : null;
            if (requestId is null)
                continue;

            var envelopeJson = await next.Content.ReadAsStringAsync(ct);
            var input = JsonSerializer.Deserialize<DurableExecutionInvocationInput>(envelopeJson, Json)!;
            var ops = input.InitialExecutionState?.Operations ?? new List<DurableOperation>();
            invocations++;

            DurableExecutionInvocationOutput output;

            var waitOp = ops.FirstOrDefault(o => o.Id == "w1");
            if (waitOp is null)
            {
                // Invocation 1: start a 1-hour wait, then suspend (Pending).
                await lambda.CheckpointDurableExecutionAsync(new CheckpointDurableExecutionRequest
                {
                    DurableExecutionArn = input.DurableExecutionArn,
                    CheckpointToken = input.CheckpointToken,
                    Updates = new List<OperationUpdate>
                    {
                        new()
                        {
                            Id = "w1",
                            Type = OperationType.WAIT,
                            Action = OperationAction.START,
                            WaitOptions = new WaitOptions { WaitSeconds = 3600 }
                        }
                    }
                }, ct);
                output = new DurableExecutionInvocationOutput { Status = InvocationStatus.Pending };
            }
            else if (waitOp.Status == "SUCCEEDED")
            {
                // Invocation 2: the driver time-skipped the wait. Run a step and finish.
                await lambda.CheckpointDurableExecutionAsync(new CheckpointDurableExecutionRequest
                {
                    DurableExecutionArn = input.DurableExecutionArn,
                    CheckpointToken = input.CheckpointToken,
                    Updates = new List<OperationUpdate>
                    {
                        new() { Id = "s1", Type = OperationType.STEP, Action = OperationAction.START, Name = "finalize" },
                        new() { Id = "s1", Type = OperationType.STEP, Action = OperationAction.SUCCEED, Payload = "\"done\"" }
                    }
                }, ct);
                output = new DurableExecutionInvocationOutput
                {
                    Status = InvocationStatus.Succeeded,
                    Result = "\"done\""
                };
            }
            else
            {
                // Unexpected replay state — fail loudly so the test surfaces it.
                output = new DurableExecutionInvocationOutput
                {
                    Status = InvocationStatus.Failed,
                    Error = new DurableErrorObject { ErrorType = "UnexpectedReplayState", ErrorMessage = $"wait status={waitOp.Status}" }
                };
            }

            // POST the response back to the Runtime API, unblocking the driver's WaitForCompletion.
            await http.PostAsync(
                $"{funcBase}/2018-06-01/runtime/invocation/{requestId}/response",
                new StringContent(JsonSerializer.Serialize(output, Json)),
                ct);

            if (output.Status != InvocationStatus.Pending)
                break;
        }

        return invocations;
    }

    /// <summary>
    /// Runs a fake durable function whose workflow starts a callback and waits for it: invocation 1
    /// starts a CALLBACK (Pending); the driver parks until a callback is sent; the replay sees the
    /// callback SUCCEEDED and returns its result. Reports the callback id it minted (via a
    /// TaskCompletionSource) so the test can send the callback, and returns the invocation count +
    /// the observed callback result.
    /// </summary>
    private static async Task<(int Invocations, string? CallbackResult)> RunFakeCallbackFunctionAsync(
        string functionName, string serviceUrl, RunCommandSettings options,
        TaskCompletionSource<string> callbackIdReady, CancellationToken ct)
    {
        var funcBase = $"http://{options.LambdaEmulatorHost}:{options.LambdaEmulatorPort}/{functionName}";
        using var http = new HttpClient();
        using var lambda = ConstructLambdaServiceClient(serviceUrl);

        var invocations = 0;
        string? callbackResult = null;
        while (!ct.IsCancellationRequested)
        {
            HttpResponseMessage next;
            try { next = await http.GetAsync($"{funcBase}/2018-06-01/runtime/invocation/next", ct); }
            catch (OperationCanceledException) { break; }
            if (!next.IsSuccessStatusCode) continue;

            var requestId = next.Headers.TryGetValues("Lambda-Runtime-Aws-Request-Id", out var ids) ? ids.First() : null;
            if (requestId is null) continue;

            var envelopeJson = await next.Content.ReadAsStringAsync(ct);
            var input = JsonSerializer.Deserialize<DurableExecutionInvocationInput>(envelopeJson, Json)!;
            var ops = input.InitialExecutionState?.Operations ?? new List<DurableOperation>();
            invocations++;

            DurableExecutionInvocationOutput output;
            var cbOp = ops.FirstOrDefault(o => o.Id == "cb1");

            if (cbOp is null)
            {
                // Invocation 1: start a callback and suspend. The checkpoint response carries the
                // minted callback id in NewExecutionState — surface it so the test can send it.
                var resp = await lambda.CheckpointDurableExecutionAsync(new CheckpointDurableExecutionRequest
                {
                    DurableExecutionArn = input.DurableExecutionArn,
                    CheckpointToken = input.CheckpointToken,
                    Updates = new List<OperationUpdate>
                    {
                        new() { Id = "cb1", Type = OperationType.CALLBACK, Action = OperationAction.START, Name = "await-approval" }
                    }
                }, ct);

                var mintedId = resp.NewExecutionState?.Operations?
                    .FirstOrDefault(o => o.Id == "cb1")?.CallbackDetails?.CallbackId;
                if (mintedId is not null)
                    callbackIdReady.TrySetResult(mintedId);

                output = new DurableExecutionInvocationOutput { Status = InvocationStatus.Pending };
            }
            else if (cbOp.Status == "SUCCEEDED")
            {
                // Invocation 2: callback resolved. Return its result as the workflow output.
                callbackResult = cbOp.CallbackDetails?.Result;
                output = new DurableExecutionInvocationOutput
                {
                    Status = InvocationStatus.Succeeded,
                    Result = callbackResult
                };
            }
            else
            {
                output = new DurableExecutionInvocationOutput
                {
                    Status = InvocationStatus.Failed,
                    Error = new DurableErrorObject { ErrorType = "UnexpectedCallbackState", ErrorMessage = $"cb status={cbOp.Status}" }
                };
            }

            await http.PostAsync(
                $"{funcBase}/2018-06-01/runtime/invocation/{requestId}/response",
                new StringContent(JsonSerializer.Serialize(output, Json)), ct);

            if (output.Status != InvocationStatus.Pending)
                break;
        }

        return (invocations, callbackResult);
    }

    [Fact]
    public async Task Callback_ParksThenResumes_OnSendCallbackSuccess()
    {
        const string callbackFunction = "DurableCallbackFoo";
        var (process, options, cts) = StartTool(skipTime: true);
        try
        {
            Assert.True(await TestHelpers.WaitForApiToStartAsync($"{process.ServiceUrl}/lambda-runtime-api/healthcheck"));

            var callbackIdReady = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var functionTask = RunFakeCallbackFunctionAsync(callbackFunction, process.ServiceUrl, options, callbackIdReady, cts.Token);

            var client = ConstructLambdaServiceClient(process.ServiceUrl);
            var invoke = await client.InvokeAsync(new InvokeRequest
            {
                FunctionName = callbackFunction,
                Payload = "{\"OrderId\":\"o-2\"}",
                DurableExecutionName = "exec-callback"
            }, cts.Token);
            Assert.Equal(System.Net.HttpStatusCode.Accepted, invoke.HttpStatusCode);

            // Wait until the workflow has started the callback and reported its minted id.
            var callbackId = await callbackIdReady.Task.WaitAsync(TimeSpan.FromSeconds(30), cts.Token);
            Assert.StartsWith("cb-", callbackId);

            // Send the callback success — this should wake the parked driver.
            using var resultStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("\"approved\""));
            var sendResp = await client.SendDurableExecutionCallbackSuccessAsync(new SendDurableExecutionCallbackSuccessRequest
            {
                CallbackId = callbackId,
                Result = resultStream
            }, cts.Token);
            Assert.Equal(System.Net.HttpStatusCode.OK, sendResp.HttpStatusCode);

            // The workflow resumes and completes with the callback result across 2 invocations.
            var (invocations, callbackResult) = await functionTask;
            Assert.Equal(2, invocations);
            Assert.Equal("\"approved\"", callbackResult);
        }
        finally
        {
            await cts.CancelAsync();
        }
    }

    [Fact]
    public async Task StartHook_DrivesWaitThenStepWorkflow_ToSucceeded()
    {
        var (process, options, cts) = StartTool(skipTime: true);
        try
        {
            Assert.True(await TestHelpers.WaitForApiToStartAsync($"{process.ServiceUrl}/lambda-runtime-api/healthcheck"));

            // Start the fake durable function loop in the background.
            var functionTask = RunFakeDurableFunctionAsync(process.ServiceUrl, options, cts.Token);

            // Start the durable execution via the SDK Invoke + durable-execution-name header.
            var client = ConstructLambdaServiceClient(process.ServiceUrl);
            var invoke = await client.InvokeAsync(new InvokeRequest
            {
                FunctionName = FunctionName,
                Payload = "{\"OrderId\":\"o-1\"}",
                DurableExecutionName = "exec-alpha"
            }, cts.Token);

            // The start hook returns the minted ARN in the X-Amz-Durable-Execution-Arn header,
            // which the SDK surfaces on the modeled InvokeResponse.DurableExecutionArn property.
            Assert.Equal(System.Net.HttpStatusCode.Accepted, invoke.HttpStatusCode);
            Assert.False(string.IsNullOrEmpty(invoke.DurableExecutionArn));

            // The driver should reach a terminal state; the function processed exactly 2 invocations.
            var invocations = await functionTask;
            Assert.Equal(2, invocations);
        }
        finally
        {
            await cts.CancelAsync();
        }
    }
}
