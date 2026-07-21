// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text.Json;
using Amazon.Lambda;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.DurableExecution.Internal;
using Amazon.Lambda.DurableExecution.Services;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.Lambda.TestUtilities;
using Amazon.Runtime;
using Xunit;

namespace Amazon.Lambda.DurableExecution.Tests;

public class DurableFunctionTests
{
    /// <summary>Reproduces the Id that <see cref="OperationIdGenerator"/> emits for the n-th root-level operation.</summary>
    private static string IdAt(int position) => OperationIdGenerator.HashOperationId(position.ToString());

    private static TestLambdaContext CreateLambdaContext() =>
        new() { Serializer = new DefaultLambdaJsonSerializer() };

    private readonly IAmazonLambda _mockClient = new MockLambdaClient();

    [Fact]
    public async Task WrapAsync_FreshExecution_StepThenWait_ReturnsPending()
    {
        var input = new DurableExecutionInvocationInput
        {
            DurableExecutionArn = "arn:aws:lambda:us-east-1:123:durable-execution:order-123",
            InitialExecutionState = new InitialExecutionState
            {
                Operations = new List<Operation>
                {
                    new()
                    {
                        Id = "exec-0",
                        Type = OperationTypes.Execution,
                        Status = OperationStatuses.Started,
                        ExecutionDetails = new ExecutionDetails { InputPayload = "{\"orderId\":\"order-123\"}" }
                    }
                }
            }
        };

        var output = await DurableFunction.WrapAsync<OrderEvent, OrderResult>(
            MyWorkflow,
            input,
            CreateLambdaContext(),
            _mockClient);

        Assert.Equal(InvocationStatus.Pending, output.Status);
    }

    [Fact]
    public async Task WrapAsync_ReplayWithElapsedWait_ReturnsSucceeded()
    {
        var pastExpirationMs = DateTimeOffset.UtcNow.AddSeconds(-5).ToUnixTimeMilliseconds();
        var input = new DurableExecutionInvocationInput
        {
            DurableExecutionArn = "arn:aws:lambda:us-east-1:123:durable-execution:order-123",
            InitialExecutionState = new InitialExecutionState
            {
                Operations = new List<Operation>
                {
                    new()
                    {
                        Id = "exec-0",
                        Type = OperationTypes.Execution,
                        Status = OperationStatuses.Started,
                        ExecutionDetails = new ExecutionDetails { InputPayload = "{\"orderId\":\"order-123\"}" }
                    },
                    new()
                    {
                        Id = IdAt(1),
                        Type = OperationTypes.Step,
                        Status = OperationStatuses.Succeeded,
                        StepDetails = new StepDetails { Result = "{\"IsValid\":true}" }
                    },
                    new()
                    {
                        Id = IdAt(2),
                        Type = OperationTypes.Wait,
                        Status = OperationStatuses.Pending,
                        WaitDetails = new WaitDetails { ScheduledEndTimestamp = pastExpirationMs }
                    }
                }
            }
        };

        var output = await DurableFunction.WrapAsync<OrderEvent, OrderResult>(
            MyWorkflow,
            input,
            CreateLambdaContext(),
            _mockClient);

        Assert.Equal(InvocationStatus.Succeeded, output.Status);
        Assert.NotNull(output.Result);
        var result = JsonSerializer.Deserialize<OrderResult>(output.Result!);
        Assert.Equal("approved", result!.Status);
    }

    [Fact]
    public async Task WrapAsync_WorkflowThrows_ReturnsFailed()
    {
        var input = new DurableExecutionInvocationInput
        {
            DurableExecutionArn = "arn:aws:lambda:us-east-1:123:durable-execution:fail-test",
            InitialExecutionState = new InitialExecutionState
            {
                Operations = new List<Operation>
                {
                    new()
                    {
                        Id = "exec-0",
                        Type = OperationTypes.Execution,
                        Status = OperationStatuses.Started,
                        ExecutionDetails = new ExecutionDetails { InputPayload = "{\"orderId\":\"bad-order\"}" }
                    }
                }
            }
        };

        var output = await DurableFunction.WrapAsync<OrderEvent, OrderResult>(
            async (evt, ctx) => throw new InvalidOperationException("workflow error"),
            input,
            CreateLambdaContext(),
            _mockClient);

        Assert.Equal(InvocationStatus.Failed, output.Status);
        Assert.NotNull(output.Error);
        Assert.Equal("workflow error", output.Error!.ErrorMessage);
        Assert.Contains("InvalidOperationException", output.Error.ErrorType!);
    }

    [Fact]
    public async Task WrapAsync_VoidWorkflow_ReturnSucceeded()
    {
        var input = new DurableExecutionInvocationInput
        {
            DurableExecutionArn = "arn:aws:lambda:us-east-1:123:durable-execution:void-test",
            InitialExecutionState = new InitialExecutionState
            {
                Operations = new List<Operation>
                {
                    new()
                    {
                        Id = "exec-0",
                        Type = OperationTypes.Execution,
                        Status = OperationStatuses.Started,
                        ExecutionDetails = new ExecutionDetails { InputPayload = "{\"orderId\":\"order-1\"}" }
                    }
                }
            }
        };

        var executed = false;
        var output = await DurableFunction.WrapAsync<OrderEvent>(
            async (evt, ctx) =>
            {
                await ctx.StepAsync(async (_, _) => { await Task.CompletedTask; executed = true; }, name: "do_work");
            },
            input,
            CreateLambdaContext(),
            _mockClient);

        Assert.Equal(InvocationStatus.Succeeded, output.Status);
        Assert.True(executed);
    }

    [Fact]
    public async Task WrapAsync_CheckpointsAreSentToService()
    {
        var mockClient = new MockLambdaClient();
        var input = new DurableExecutionInvocationInput
        {
            DurableExecutionArn = "arn:aws:lambda:us-east-1:123:durable-execution:checkpoint-test",
            CheckpointToken = "initial-token",
            InitialExecutionState = new InitialExecutionState
            {
                Operations = new List<Operation>
                {
                    new()
                    {
                        Id = "exec-0",
                        Type = OperationTypes.Execution,
                        Status = OperationStatuses.Started,
                        ExecutionDetails = new ExecutionDetails { InputPayload = "{\"orderId\":\"order-1\"}" }
                    }
                }
            }
        };

        var output = await DurableFunction.WrapAsync<OrderEvent, OrderResult>(
            MyWorkflow,
            input,
            CreateLambdaContext(),
            mockClient);

        Assert.Equal(InvocationStatus.Pending, output.Status);

        // Each StepAsync emits a fire-and-forget START before user code runs
        // (telemetry under AtLeastOncePerRetry). With FlushInterval = 0 the
        // worker may flush the START on its own before SUCCEED arrives, so the
        // exact batching of START vs SUCCEED is timing-dependent. Assert on
        // the flat sequence of updates instead.
        var allUpdates = mockClient.CheckpointCalls
            .SelectMany(c => c.Updates)
            .ToList();

        // Expect: step START, step SUCCEED, wait START (in that order).
        Assert.Equal(3, allUpdates.Count);

        Assert.Equal("STEP", allUpdates[0].Type);
        Assert.Equal("START", allUpdates[0].Action);
        Assert.Equal("validate", allUpdates[0].Name);

        Assert.Equal("STEP", allUpdates[1].Type);
        Assert.Equal("SUCCEED", allUpdates[1].Action);
        Assert.Equal("validate", allUpdates[1].Name);
        Assert.NotNull(allUpdates[1].Payload);

        Assert.Equal("WAIT", allUpdates[2].Type);
        Assert.Equal("START", allUpdates[2].Action);
        Assert.Equal("delay", allUpdates[2].Name);
        Assert.NotNull(allUpdates[2].WaitOptions);
        Assert.Equal(30, allUpdates[2].WaitOptions.WaitSeconds);

        // The first call sends the initial checkpoint token.
        Assert.Equal("arn:aws:lambda:us-east-1:123:durable-execution:checkpoint-test", mockClient.CheckpointCalls[0].DurableExecutionArn);
        Assert.Equal("initial-token", mockClient.CheckpointCalls[0].CheckpointToken);
    }

    [Fact]
    public async Task WrapAsync_UserPayload_BindsCamelCaseToPascalCaseProperty()
    {
        // The wire payload uses camelCase ("orderId"), the user POCO uses PascalCase (OrderId).
        // ExtractUserPayload must do case-insensitive binding so workflows can read input.OrderId.
        var input = new DurableExecutionInvocationInput
        {
            DurableExecutionArn = "arn:aws:lambda:us-east-1:123:durable-execution:case-test",
            InitialExecutionState = new InitialExecutionState
            {
                Operations = new List<Operation>
                {
                    new()
                    {
                        Id = "exec-0",
                        Type = OperationTypes.Execution,
                        Status = OperationStatuses.Started,
                        ExecutionDetails = new ExecutionDetails { InputPayload = "{\"orderId\":\"abc-123\"}" }
                    }
                }
            }
        };

        string? observedOrderId = null;
        var output = await DurableFunction.WrapAsync<OrderEvent, OrderResult>(
            async (evt, ctx) =>
            {
                observedOrderId = evt.OrderId;
                await Task.CompletedTask;
                return new OrderResult { Status = "ok", OrderId = evt.OrderId };
            },
            input,
            CreateLambdaContext(),
            _mockClient);

        Assert.Equal(InvocationStatus.Succeeded, output.Status);
        Assert.Equal("abc-123", observedOrderId);
    }

    [Fact]
    public async Task WrapAsync_NoExecutionOp_ThrowsMalformedEnvelope()
    {
        // No EXECUTION operation in the envelope — ExtractUserPayload must throw a typed
        // DurableExecutionException so the malformed envelope surfaces as a clear error
        // instead of leaking default!/null into user code as a NullReferenceException.
        var input = new DurableExecutionInvocationInput
        {
            DurableExecutionArn = "arn:aws:lambda:us-east-1:123:durable-execution:no-exec",
            InitialExecutionState = new InitialExecutionState
            {
                Operations = new List<Operation>()
            }
        };

        var ex = await Assert.ThrowsAsync<DurableExecutionException>(() =>
            DurableFunction.WrapAsync<OrderEvent, OrderResult>(
                async (evt, ctx) =>
                {
                    await Task.CompletedTask;
                    return new OrderResult { Status = "ok" };
                },
                input,
                CreateLambdaContext(),
                _mockClient));

        Assert.Contains("malformed", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("EXECUTION", ex.Message);
    }

    [Fact]
    public async Task WrapAsync_PaginatedInitialState_HydratesAllPages()
    {
        // The service can return execution state across multiple pages — the first
        // page comes inline on the invocation envelope (InitialExecutionState) and
        // subsequent pages must be fetched via GetDurableExecutionState. Verify the
        // pagination loop in WrapAsyncCore (DurableFunction.cs:160-167) walks every
        // page so the workflow sees the full operation history on replay.
        var arn = "arn:aws:lambda:us-east-1:123:durable-execution:paginated";

        // Page 0 (in InitialExecutionState): EXECUTION op + step1 SUCCEEDED.
        // Page 1 (fetched with marker "marker-1"): step2 SUCCEEDED, points to marker-2.
        // Page 2 (fetched with marker "marker-2"): step3 SUCCEEDED, no NextMarker — loop exits.
        var input = new DurableExecutionInvocationInput
        {
            DurableExecutionArn = arn,
            CheckpointToken = "ckpt-0",
            InitialExecutionState = new InitialExecutionState
            {
                Operations = new List<Operation>
                {
                    new()
                    {
                        Id = "exec-0",
                        Type = OperationTypes.Execution,
                        Status = OperationStatuses.Started,
                        ExecutionDetails = new ExecutionDetails { InputPayload = "{\"orderId\":\"order-1\"}" }
                    },
                    new()
                    {
                        Id = IdAt(1),
                        Type = OperationTypes.Step,
                        Status = OperationStatuses.Succeeded,
                        StepDetails = new StepDetails { Result = "\"page-0-result\"" }
                    }
                },
                NextMarker = "marker-1"
            }
        };

        var mockClient = new MockLambdaClient
        {
            GetExecutionStateHandler = req => req.Marker switch
            {
                "marker-1" => new Amazon.Lambda.Model.GetDurableExecutionStateResponse
                {
                    Operations = new List<Amazon.Lambda.Model.Operation>
                    {
                        new()
                        {
                            Id = IdAt(2),
                            Type = OperationTypes.Step,
                            Status = OperationStatuses.Succeeded,
                            StepDetails = new Amazon.Lambda.Model.StepDetails { Result = "\"page-1-result\"" }
                        }
                    },
                    NextMarker = "marker-2"
                },
                "marker-2" => new Amazon.Lambda.Model.GetDurableExecutionStateResponse
                {
                    Operations = new List<Amazon.Lambda.Model.Operation>
                    {
                        new()
                        {
                            Id = IdAt(3),
                            Type = OperationTypes.Step,
                            Status = OperationStatuses.Succeeded,
                            StepDetails = new Amazon.Lambda.Model.StepDetails { Result = "\"page-2-result\"" }
                        }
                    }
                    // NextMarker omitted -> loop terminates.
                },
                _ => throw new InvalidOperationException($"Unexpected marker: {req.Marker}")
            }
        };

        var observed = new List<string>();
        var output = await DurableFunction.WrapAsync<OrderEvent, OrderResult>(
            async (evt, ctx) =>
            {
                // All three steps must replay the cached results from the paginated state
                // without re-executing — if the loop missed a page, the corresponding step
                // would run fresh and append a different value to `observed`.
                observed.Add(await ctx.StepAsync(
                    async (_, _) => { await Task.CompletedTask; return "fresh"; }, name: "step1"));
                observed.Add(await ctx.StepAsync(
                    async (_, _) => { await Task.CompletedTask; return "fresh"; }, name: "step2"));
                observed.Add(await ctx.StepAsync(
                    async (_, _) => { await Task.CompletedTask; return "fresh"; }, name: "step3"));
                return new OrderResult { Status = "ok", OrderId = evt.OrderId };
            },
            input,
            CreateLambdaContext(),
            mockClient);

        Assert.Equal(InvocationStatus.Succeeded, output.Status);

        // Two GetDurableExecutionState calls — one per fetched page (page 0 was inline).
        Assert.Equal(2, mockClient.GetExecutionStateCalls.Count);
        Assert.Equal("marker-1", mockClient.GetExecutionStateCalls[0].Marker);
        Assert.Equal(arn, mockClient.GetExecutionStateCalls[0].DurableExecutionArn);
        Assert.Equal("ckpt-0", mockClient.GetExecutionStateCalls[0].CheckpointToken);
        Assert.Equal("marker-2", mockClient.GetExecutionStateCalls[1].Marker);

        // The workflow saw replayed results from ALL three pages — none re-executed.
        Assert.Equal(new[] { "page-0-result", "page-1-result", "page-2-result" }, observed);

        // No checkpoints were written: every step replayed from cache.
        Assert.Empty(mockClient.CheckpointCalls);
    }

    [Fact]
    public async Task WrapAsync_NullInitialExecutionState_ThrowsMalformedEnvelope()
    {
        // No initial execution state at all — same malformed-envelope branch in ExtractUserPayload.
        var input = new DurableExecutionInvocationInput
        {
            DurableExecutionArn = "arn:aws:lambda:us-east-1:123:durable-execution:null-state"
        };

        var ex = await Assert.ThrowsAsync<DurableExecutionException>(() =>
            DurableFunction.WrapAsync<OrderEvent, OrderResult>(
                async (evt, ctx) =>
                {
                    await Task.CompletedTask;
                    return new OrderResult { Status = "ok" };
                },
                input,
                CreateLambdaContext(),
                _mockClient));

        Assert.Contains("malformed", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────────────────────────────
    // IsTerminalCheckpointError classification (mirrors CheckpointError in
    // aws-durable-execution-sdk-python):
    //   4xx (except 429) → terminal (Failed envelope)
    //   429 / 5xx / no status → transient (escapes to host for Lambda retry)
    //   Carve-out: InvalidParameterValueException "Invalid Checkpoint Token" → transient
    //
    // Driven through CheckpointDurableExecution: a workflow that succeeds a single Step
    // forces the batcher to flush, which is wrapped by the try/catch in WrapAsyncCore.
    // ──────────────────────────────────────────────────────────────────────

    public static IEnumerable<object[]> TerminalCheckpointErrorCases() => new[]
    {
        new object[] { MakeServiceException("ResourceNotFoundException", HttpStatusCode.NotFound, "ARN not found") },
        new object[] { MakeServiceException("AccessDeniedException", HttpStatusCode.Forbidden, "denied") },
        new object[] { MakeServiceException("KMSAccessDeniedException", HttpStatusCode.BadRequest, "kms denied") },
        new object[] { MakeServiceException("ValidationException", HttpStatusCode.BadRequest, "bad input") },
        new object[] { MakeServiceException("InvalidParameterValueException", HttpStatusCode.BadRequest, "Some other parameter") },
    };

    [Theory]
    [MemberData(nameof(TerminalCheckpointErrorCases))]
    public async Task WrapAsync_CheckpointThrowsTerminal_ReturnsFailed(AmazonServiceException ex)
    {
        // LambdaDurableServiceClient now wraps SDK exceptions in DurableExecutionException
        // so user logs carry context (which call, which ARN). The outer message includes
        // the inner SDK message; the classifier matches on the wrapper's InnerException.
        var input = MakeCheckpointInput();
        var mockClient = new MockLambdaClient { CheckpointThrows = ex };

        var output = await DurableFunction.WrapAsync<OrderEvent, OrderResult>(
            SingleStepWorkflow, input, CreateLambdaContext(), mockClient);

        Assert.Equal(InvocationStatus.Failed, output.Status);
        Assert.NotNull(output.Error);
        Assert.Contains(ex.Message, output.Error!.ErrorMessage);
        Assert.Contains("Failed to checkpoint", output.Error.ErrorMessage);
    }

    public static IEnumerable<object[]> TransientCheckpointErrorCases() => new[]
    {
        // 5xx
        new object[] { MakeServiceException("InternalServerError", HttpStatusCode.InternalServerError, "boom") },
        new object[] { MakeServiceException("ServiceUnavailable", HttpStatusCode.ServiceUnavailable, "down") },
        // 429
        new object[] { MakeServiceException("TooManyRequestsException", (HttpStatusCode)429, "throttled") },
        // No status (network / SDK-internal). HttpStatusCode default (0) → classifier treats < 400 as transient.
        new object[] { MakeServiceException("RequestTimeout", 0, "timeout") },
        // Carve-out: stale checkpoint token is transient.
        new object[] { MakeServiceException("InvalidParameterValueException", HttpStatusCode.BadRequest, "Invalid Checkpoint Token: stale") },
    };

    [Theory]
    [MemberData(nameof(TransientCheckpointErrorCases))]
    public async Task WrapAsync_CheckpointThrowsTransient_PropagatesToHost(AmazonServiceException ex)
    {
        // Transient SDK errors escape the IsTerminalCheckpointError catch and propagate
        // to the host as DurableExecutionException wrapping the original SDK exception
        // — Lambda's normal retry semantics fire on the wrapper. The original SDK
        // exception is preserved as InnerException so callers can still introspect
        // the original status code / error code.
        var input = MakeCheckpointInput();
        var mockClient = new MockLambdaClient { CheckpointThrows = ex };

        var thrown = await Assert.ThrowsAsync<DurableExecutionException>(() =>
            DurableFunction.WrapAsync<OrderEvent, OrderResult>(
                SingleStepWorkflow, input, CreateLambdaContext(), mockClient));

        Assert.Same(ex, thrown.InnerException);
    }

    [Fact]
    public async Task WrapAsync_HydrationThrows_AlwaysPropagatesToHost()
    {
        // State hydration is OUTSIDE the IsTerminalCheckpointError try/catch — every
        // GetExecutionStateAsync failure escapes for Lambda retry. Use a 4xx that
        // *would* be terminal if it came from a checkpoint flush to prove the path
        // isn't classified.
        var input = new DurableExecutionInvocationInput
        {
            DurableExecutionArn = "arn:aws:lambda:us-east-1:123:durable-execution:hydrate-fail",
            InitialExecutionState = new InitialExecutionState
            {
                Operations = new List<Operation>
                {
                    new()
                    {
                        Id = "exec-0",
                        Type = OperationTypes.Execution,
                        Status = OperationStatuses.Started,
                        ExecutionDetails = new ExecutionDetails { InputPayload = "{\"orderId\":\"order-1\"}" }
                    }
                },
                NextMarker = "page-1"  // force the hydration loop to run
            }
        };
        var ex = MakeServiceException("ResourceNotFoundException", HttpStatusCode.NotFound, "ARN gone");
        var mockClient = new MockLambdaClient { GetExecutionStateThrows = ex };

        // Hydration errors are wrapped in DurableExecutionException by
        // LambdaDurableServiceClient.GetExecutionStateAsync but are NOT caught by the
        // IsTerminalCheckpointError filter, so they escape to the host.
        var thrown = await Assert.ThrowsAsync<DurableExecutionException>(() =>
            DurableFunction.WrapAsync<OrderEvent, OrderResult>(
                MyWorkflow, input, CreateLambdaContext(), mockClient));

        Assert.Same(ex, thrown.InnerException);
        Assert.Contains("Failed to fetch execution state", thrown.Message);
    }

    private static AmazonServiceException MakeServiceException(string code, HttpStatusCode status, string message)
    {
        return new AmazonServiceException(message, innerException: null, ErrorType.Unknown, code, requestId: "req-1", statusCode: status);
    }

    private static DurableExecutionInvocationInput MakeCheckpointInput() => new()
    {
        DurableExecutionArn = "arn:aws:lambda:us-east-1:123:durable-execution:checkpoint-fail",
        InitialExecutionState = new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = "exec-0",
                    Type = OperationTypes.Execution,
                    Status = OperationStatuses.Started,
                    ExecutionDetails = new ExecutionDetails { InputPayload = "{\"orderId\":\"order-1\"}" }
                }
            }
        }
    };

    private static async Task<OrderResult> SingleStepWorkflow(OrderEvent input, IDurableContext context)
    {
        // One step succeed → forces a checkpoint flush, which the mock fails.
        await context.StepAsync(async (_, _) => { await Task.CompletedTask; return "ok"; }, name: "s1");
        return new OrderResult { Status = "done" };
    }

    [Fact]
    public async Task WrapAsync_CreateCallbackThenWait_AllocatesCallbackIdAndSuspends()
    {
        // End-to-end through the real LambdaDurableServiceClient: the mock
        // client returns NewExecutionState carrying a CallbackId on the
        // CALLBACK START checkpoint response, and the SDK plumbs it through.
        var input = new DurableExecutionInvocationInput
        {
            DurableExecutionArn = "arn:aws:lambda:us-east-1:123:durable-execution:cb-test",
            InitialExecutionState = new InitialExecutionState
            {
                Operations = new List<Operation>
                {
                    new()
                    {
                        Id = "exec-0",
                        Type = OperationTypes.Execution,
                        Status = OperationStatuses.Started,
                        ExecutionDetails = new ExecutionDetails { InputPayload = "{\"OrderId\":\"o-1\"}" }
                    }
                }
            }
        };

        var capturedCallbackId = (string?)null;
        var mockClient = new MockLambdaClient
        {
            CheckpointHandler = req =>
            {
                // Echo back any CALLBACK START as a STARTED op with a service-allocated id.
                var newOps = new List<Amazon.Lambda.Model.Operation>();
                foreach (var u in req.Updates)
                {
                    if (u.Type == OperationTypes.Callback && u.Action == "START")
                    {
                        newOps.Add(new Amazon.Lambda.Model.Operation
                        {
                            Id = u.Id,
                            Type = OperationTypes.Callback,
                            Status = OperationStatuses.Started,
                            Name = u.Name,
                            CallbackDetails = new Amazon.Lambda.Model.CallbackDetails
                            {
                                CallbackId = "servicealloccbid"
                            }
                        });
                    }
                }
                return new Amazon.Lambda.Model.CheckpointDurableExecutionResponse
                {
                    NewExecutionState = newOps.Count == 0
                        ? null
                        : new Amazon.Lambda.Model.CheckpointUpdatedExecutionState { Operations = newOps }
                };
            }
        };

        var output = await DurableFunction.WrapAsync<OrderEvent, OrderResult>(
            async (e, ctx) =>
            {
                var cb = await ctx.CreateCallbackAsync<string>(name: "approval");
                capturedCallbackId = cb.CallbackId;
                var status = await cb.GetResultAsync();
                return new OrderResult { Status = status, OrderId = e.OrderId };
            },
            input,
            CreateLambdaContext(),
            mockClient);

        Assert.Equal(InvocationStatus.Pending, output.Status);
        Assert.Equal("servicealloccbid", capturedCallbackId);
    }

    [Fact]
    public async Task WrapAsync_ReplayCallbackSucceeded_ReturnsResultAfterSuspend()
    {
        // Second invocation: the callback's checkpoint is now SUCCEEDED;
        // the workflow returns the deserialized result.
        var input = new DurableExecutionInvocationInput
        {
            DurableExecutionArn = "arn:aws:lambda:us-east-1:123:durable-execution:cb-test",
            InitialExecutionState = new InitialExecutionState
            {
                Operations = new List<Operation>
                {
                    new()
                    {
                        Id = "exec-0",
                        Type = OperationTypes.Execution,
                        Status = OperationStatuses.Started,
                        ExecutionDetails = new ExecutionDetails { InputPayload = "{\"OrderId\":\"o-1\"}" }
                    },
                    new()
                    {
                        Id = IdAt(1),
                        Type = OperationTypes.Callback,
                        Status = OperationStatuses.Succeeded,
                        Name = "approval",
                        CallbackDetails = new CallbackDetails
                        {
                            CallbackId = "servicealloccbid",
                            Result = "\"approved\""
                        }
                    }
                }
            }
        };

        var output = await DurableFunction.WrapAsync<OrderEvent, OrderResult>(
            async (e, ctx) =>
            {
                var cb = await ctx.CreateCallbackAsync<string>(name: "approval");
                var status = await cb.GetResultAsync();
                return new OrderResult { Status = status, OrderId = e.OrderId };
            },
            input,
            CreateLambdaContext(),
            new MockLambdaClient());

        Assert.Equal(InvocationStatus.Succeeded, output.Status);
        Assert.NotNull(output.Result);
        var result = JsonSerializer.Deserialize<OrderResult>(output.Result!);
        Assert.Equal("approved", result!.Status);
    }

    [Fact]
    public async Task WrapAsync_ReplayDeterminism_CallbackIdStableAcrossInvocations()
    {
        // First invocation allocates a callback ID via the mock; in a real run
        // that ID would be persisted in the service's checkpoint state and
        // returned to the second invocation via InitialExecutionState. Verify
        // the same ID survives that round-trip (we model "round-trip" by
        // replaying with a STARTED checkpoint that carries the same ID).
        const string id = "stablecbidreplay";
        var input = new DurableExecutionInvocationInput
        {
            DurableExecutionArn = "arn:test",
            InitialExecutionState = new InitialExecutionState
            {
                Operations = new List<Operation>
                {
                    new()
                    {
                        Id = "exec-0",
                        Type = OperationTypes.Execution,
                        Status = OperationStatuses.Started,
                        ExecutionDetails = new ExecutionDetails { InputPayload = "{\"OrderId\":\"o-1\"}" }
                    },
                    new()
                    {
                        Id = IdAt(1),
                        Type = OperationTypes.Callback,
                        Status = OperationStatuses.Started,
                        Name = "approval",
                        CallbackDetails = new CallbackDetails { CallbackId = id }
                    }
                }
            }
        };

        string? observed = null;
        var output = await DurableFunction.WrapAsync<OrderEvent, OrderResult>(
            async (e, ctx) =>
            {
                var cb = await ctx.CreateCallbackAsync<string>(name: "approval");
                observed = cb.CallbackId;
                var status = await cb.GetResultAsync();
                return new OrderResult { Status = status, OrderId = e.OrderId };
            },
            input,
            CreateLambdaContext(),
            new MockLambdaClient());

        Assert.Equal(InvocationStatus.Pending, output.Status);
        Assert.Equal(id, observed);
    }

    [Fact]
    public async Task WrapAsync_InternalOverloadWithIDurableServiceClient_WorksIdenticallyToPublicOverload()
    {
        var pastExpirationMs = DateTimeOffset.UtcNow.AddSeconds(-5).ToUnixTimeMilliseconds();
        var input = new DurableExecutionInvocationInput
        {
            DurableExecutionArn = "arn:aws:lambda:us-east-1:123:durable-execution:seam-test",
            InitialExecutionState = new InitialExecutionState
            {
                Operations = new List<Operation>
                {
                    new()
                    {
                        Id = "exec-0",
                        Type = OperationTypes.Execution,
                        Status = OperationStatuses.Started,
                        ExecutionDetails = new ExecutionDetails { InputPayload = "{\"orderId\":\"order-123\"}" }
                    },
                    new()
                    {
                        Id = IdAt(1),
                        Type = OperationTypes.Step,
                        Status = OperationStatuses.Succeeded,
                        StepDetails = new StepDetails { Result = "{\"IsValid\":true}" }
                    },
                    new()
                    {
                        Id = IdAt(2),
                        Type = OperationTypes.Wait,
                        Status = OperationStatuses.Pending,
                        WaitDetails = new WaitDetails { ScheduledEndTimestamp = pastExpirationMs }
                    }
                }
            }
        };

        var serviceClient = new Services.LambdaDurableServiceClient(new MockLambdaClient());

        var output = await DurableFunction.WrapAsync<OrderEvent, OrderResult>(
            MyWorkflow,
            input,
            CreateLambdaContext(),
            (Services.IDurableServiceClient)serviceClient);

        Assert.Equal(InvocationStatus.Succeeded, output.Status);
        Assert.NotNull(output.Result);
        var result = JsonSerializer.Deserialize<OrderResult>(output.Result!);
        Assert.Equal("approved", result!.Status);
    }

    private static async Task<OrderResult> MyWorkflow(OrderEvent input, IDurableContext context)
    {
        var validation = await context.StepAsync(
            async (_, _) => { await Task.CompletedTask; return new ValidationResult { IsValid = true }; },
            name: "validate");

        await context.WaitAsync(TimeSpan.FromSeconds(30), name: "delay");

        return new OrderResult { Status = "approved", OrderId = input.OrderId };
    }

    private class OrderEvent
    {
        public string? OrderId { get; set; }
    }

    private class OrderResult
    {
        public string? Status { get; set; }
        public string? OrderId { get; set; }
    }

    private class ValidationResult
    {
        public bool IsValid { get; set; }
    }
}
