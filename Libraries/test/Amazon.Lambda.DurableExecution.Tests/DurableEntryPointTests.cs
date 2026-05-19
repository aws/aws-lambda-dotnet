using System.IO;
using System.Net;
using System.Text.Json;
using Amazon.Lambda;
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.DurableExecution.Internal;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.Lambda.TestUtilities;
using Amazon.Runtime;
using Xunit;
using Operation = Amazon.Lambda.DurableExecution.Internal.Operation;
using StepDetails = Amazon.Lambda.DurableExecution.Internal.StepDetails;
using WaitDetails = Amazon.Lambda.DurableExecution.Internal.WaitDetails;
using ExecutionDetails = Amazon.Lambda.DurableExecution.Internal.ExecutionDetails;

namespace Amazon.Lambda.DurableExecution.Tests;

/// <summary>
/// Drives <see cref="DurableEntryPoint{TInput,TOutput}"/> through its public
/// <c>Stream → Stream</c> contract. Builds an internal envelope POCO,
/// serializes it via the library's internal envelope context (mirroring what the
/// real Lambda runtime delivers on the wire), then asserts on the deserialized
/// output envelope. <c>InternalsVisibleTo</c> lets us reach the internal types.
/// </summary>
public class DurableEntryPointTests
{
    /// <summary>Reproduces the Id that <see cref="OperationIdGenerator"/> emits for the n-th root-level operation.</summary>
    private static string IdAt(int position) => OperationIdGenerator.HashOperationId(position.ToString());

    private static TestLambdaContext CreateLambdaContext() =>
#pragma warning disable AWSLAMBDA001 // TestLambdaContext.Serializer is experimental.
        new() { Serializer = new DefaultLambdaJsonSerializer() };
#pragma warning restore AWSLAMBDA001

    private readonly IAmazonLambda _mockClient = new MockLambdaClient();

    private static MemoryStream EnvelopeStream(DurableExecutionInvocationInput input)
    {
        var ms = new MemoryStream();
        JsonSerializer.Serialize(ms, input, DurableEnvelopeJsonContext.Default.DurableExecutionInvocationInput);
        ms.Position = 0;
        return ms;
    }

    private static async Task<DurableExecutionInvocationOutput> InvokeAsync<TInput, TOutput>(
        Func<TInput, IDurableContext, Task<TOutput>> workflow,
        DurableExecutionInvocationInput input,
        ILambdaContext ctx,
        IAmazonLambda lambdaClient)
    {
        var entry = new DurableEntryPoint<TInput, TOutput>(workflow, lambdaClient);
        using var inStream = EnvelopeStream(input);
        var outStream = await entry.InvokeAsync(inStream, ctx);
        return JsonSerializer.Deserialize(outStream, DurableEnvelopeJsonContext.Default.DurableExecutionInvocationOutput)!;
    }

    [Fact]
    public async Task FreshExecution_StepThenWait_ReturnsPending()
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

        var output = await InvokeAsync<OrderEvent, OrderResult>(
            MyWorkflow, input, CreateLambdaContext(), _mockClient);

        Assert.Equal(InvocationStatus.Pending, output.Status);
    }

    [Fact]
    public async Task ReplayWithElapsedWait_ReturnsSucceeded()
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

        var output = await InvokeAsync<OrderEvent, OrderResult>(
            MyWorkflow, input, CreateLambdaContext(), _mockClient);

        Assert.Equal(InvocationStatus.Succeeded, output.Status);
        Assert.NotNull(output.Result);
        var result = JsonSerializer.Deserialize<OrderResult>(output.Result!);
        Assert.Equal("approved", result!.Status);
    }

    [Fact]
    public async Task WorkflowThrows_ReturnsFailed()
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

        var output = await InvokeAsync<OrderEvent, OrderResult>(
            async (evt, ctx) => throw new InvalidOperationException("workflow error"),
            input, CreateLambdaContext(), _mockClient);

        Assert.Equal(InvocationStatus.Failed, output.Status);
        Assert.NotNull(output.Error);
        Assert.Equal("workflow error", output.Error!.ErrorMessage);
        Assert.Contains("InvalidOperationException", output.Error.ErrorType!);
    }

    [Fact]
    public async Task VoidWorkflow_ReturnsSucceeded()
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
        var entry = new DurableEntryPoint<OrderEvent>(
            async (evt, ctx) =>
            {
                await ctx.StepAsync(async (_) => { await Task.CompletedTask; executed = true; }, name: "do_work");
            },
            _mockClient);

        using var inStream = EnvelopeStream(input);
        var outStream = await entry.InvokeAsync(inStream, CreateLambdaContext());
        var output = JsonSerializer.Deserialize(outStream, DurableEnvelopeJsonContext.Default.DurableExecutionInvocationOutput)!;

        Assert.Equal(InvocationStatus.Succeeded, output.Status);
        Assert.True(executed);
    }

    [Fact]
    public async Task CheckpointsAreSentToService()
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

        var output = await InvokeAsync<OrderEvent, OrderResult>(
            MyWorkflow, input, CreateLambdaContext(), mockClient);

        Assert.Equal(InvocationStatus.Pending, output.Status);
        Assert.Equal(2, mockClient.CheckpointCalls.Count);

        // First flush: step SUCCEED (the user awaits StepAsync, which awaits
        // its SUCCEED enqueue, which blocks until the batcher flushes it).
        var firstCall = mockClient.CheckpointCalls[0];
        Assert.Equal("arn:aws:lambda:us-east-1:123:durable-execution:checkpoint-test", firstCall.DurableExecutionArn);
        Assert.Equal("initial-token", firstCall.CheckpointToken);
        Assert.Single(firstCall.Updates);
        var stepUpdate = firstCall.Updates[0];
        Assert.Equal("STEP", stepUpdate.Type);
        Assert.Equal("SUCCEED", stepUpdate.Action);
        Assert.Equal("validate", stepUpdate.Name);
        Assert.NotNull(stepUpdate.Payload);

        // Second flush: wait START (blocks until the service has the timer
        // recorded before WaitAsync suspends).
        var secondCall = mockClient.CheckpointCalls[1];
        Assert.Single(secondCall.Updates);
        var waitUpdate = secondCall.Updates[0];
        Assert.Equal("WAIT", waitUpdate.Type);
        Assert.Equal("START", waitUpdate.Action);
        Assert.Equal("delay", waitUpdate.Name);
        Assert.NotNull(waitUpdate.WaitOptions);
        Assert.Equal(30, waitUpdate.WaitOptions.WaitSeconds);
    }

    [Fact]
    public async Task UserPayload_BindsCamelCaseToPascalCaseProperty()
    {
        // The wire payload uses camelCase ("orderId"), the user POCO uses PascalCase (OrderId).
        // Stage-2 deserialization must do case-insensitive binding so workflows can read input.OrderId.
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
        var output = await InvokeAsync<OrderEvent, OrderResult>(
            async (evt, ctx) =>
            {
                observedOrderId = evt.OrderId;
                await Task.CompletedTask;
                return new OrderResult { Status = "ok", OrderId = evt.OrderId };
            },
            input, CreateLambdaContext(), _mockClient);

        Assert.Equal(InvocationStatus.Succeeded, output.Status);
        Assert.Equal("abc-123", observedOrderId);
    }

    [Fact]
    public async Task NoExecutionOp_ThrowsMalformedEnvelope()
    {
        // No EXECUTION operation in the envelope — DurableEntryPointCore.ExtractUserPayload must
        // throw a typed DurableExecutionException so the malformed envelope surfaces as a clear
        // error instead of leaking default!/null into user code as a NullReferenceException.
        var input = new DurableExecutionInvocationInput
        {
            DurableExecutionArn = "arn:aws:lambda:us-east-1:123:durable-execution:no-exec",
            InitialExecutionState = new InitialExecutionState
            {
                Operations = new List<Operation>()
            }
        };

        var entry = new DurableEntryPoint<OrderEvent, OrderResult>(
            async (evt, ctx) => { await Task.CompletedTask; return new OrderResult { Status = "ok" }; },
            _mockClient);

        using var inStream = EnvelopeStream(input);
        var ex = await Assert.ThrowsAsync<DurableExecutionException>(
            () => entry.InvokeAsync(inStream, CreateLambdaContext()));

        Assert.Contains("malformed", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("EXECUTION", ex.Message);
    }

    [Fact]
    public async Task PaginatedInitialState_HydratesAllPages()
    {
        // The service can return execution state across multiple pages — the first
        // page comes inline on the invocation envelope (InitialExecutionState) and
        // subsequent pages must be fetched via GetDurableExecutionState. Verify the
        // pagination loop in DurableEntryPointCore walks every page so the workflow
        // sees the full operation history on replay.
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
        var output = await InvokeAsync<OrderEvent, OrderResult>(
            async (evt, ctx) =>
            {
                // All three steps must replay the cached results from the paginated state
                // without re-executing — if the loop missed a page, the corresponding step
                // would run fresh and append a different value to `observed`.
                observed.Add(await ctx.StepAsync(
                    async (_) => { await Task.CompletedTask; return "fresh"; }, name: "step1"));
                observed.Add(await ctx.StepAsync(
                    async (_) => { await Task.CompletedTask; return "fresh"; }, name: "step2"));
                observed.Add(await ctx.StepAsync(
                    async (_) => { await Task.CompletedTask; return "fresh"; }, name: "step3"));
                return new OrderResult { Status = "ok", OrderId = evt.OrderId };
            },
            input, CreateLambdaContext(), mockClient);

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
    public async Task NullInitialExecutionState_ThrowsMalformedEnvelope()
    {
        // No initial execution state at all — same malformed-envelope branch in ExtractUserPayload.
        var input = new DurableExecutionInvocationInput
        {
            DurableExecutionArn = "arn:aws:lambda:us-east-1:123:durable-execution:null-state"
        };

        var entry = new DurableEntryPoint<OrderEvent, OrderResult>(
            async (evt, ctx) => { await Task.CompletedTask; return new OrderResult { Status = "ok" }; },
            _mockClient);

        using var inStream = EnvelopeStream(input);
        var ex = await Assert.ThrowsAsync<DurableExecutionException>(
            () => entry.InvokeAsync(inStream, CreateLambdaContext()));

        Assert.Contains("malformed", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task NoSerializerOnContext_ThrowsHelpfulException()
    {
        // Stage 2 (user payload (de)serialization) requires a serializer registered on
        // ILambdaContext.Serializer. Without one, the entry point should throw a
        // self-explanatory InvalidOperationException rather than NRE on the next line.
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
                        ExecutionDetails = new ExecutionDetails { InputPayload = "{\"orderId\":\"x\"}" }
                    }
                }
            }
        };

        var entry = new DurableEntryPoint<OrderEvent, OrderResult>(
            async (evt, ctx) => { await Task.CompletedTask; return new OrderResult(); },
            _mockClient);

        using var inStream = EnvelopeStream(input);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => entry.InvokeAsync(inStream, new TestLambdaContext()));

        Assert.Contains("ILambdaContext.Serializer", ex.Message);
    }

    // ──────────────────────────────────────────────────────────────────────
    // IsTerminalCheckpointError classification (mirrors CheckpointError in
    // aws-durable-execution-sdk-python):
    //   4xx (except 429) → terminal (Failed envelope)
    //   429 / 5xx / no status → transient (escapes to host for Lambda retry)
    //   Carve-out: InvalidParameterValueException "Invalid Checkpoint Token" → transient
    //
    // Driven through CheckpointDurableExecution: a workflow that succeeds a single Step
    // forces the batcher to flush, which is wrapped by the try/catch in DurableEntryPointCore.
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
    public async Task CheckpointThrowsTerminal_ReturnsFailed(AmazonServiceException ex)
    {
        var input = MakeCheckpointInput();
        var mockClient = new MockLambdaClient { CheckpointThrows = ex };

        var output = await InvokeAsync<OrderEvent, OrderResult>(
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
    public async Task CheckpointThrowsTransient_PropagatesToHost(AmazonServiceException ex)
    {
        var input = MakeCheckpointInput();
        var mockClient = new MockLambdaClient { CheckpointThrows = ex };
        var entry = new DurableEntryPoint<OrderEvent, OrderResult>(SingleStepWorkflow, mockClient);

        using var inStream = EnvelopeStream(input);
        var thrown = await Assert.ThrowsAsync<DurableExecutionException>(
            () => entry.InvokeAsync(inStream, CreateLambdaContext()));

        Assert.Same(ex, thrown.InnerException);
    }

    [Fact]
    public async Task HydrationThrows_AlwaysPropagatesToHost()
    {
        // State hydration is OUTSIDE the IsTerminalCheckpointError try/catch — every
        // GetExecutionStateAsync failure escapes for Lambda retry, matching Python's
        // GetExecutionStateError (an InvocationError). Use a 4xx that *would* be terminal
        // if it came from a checkpoint flush to prove the path isn't classified.
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
        var entry = new DurableEntryPoint<OrderEvent, OrderResult>(MyWorkflow, mockClient);

        using var inStream = EnvelopeStream(input);
        var thrown = await Assert.ThrowsAsync<DurableExecutionException>(
            () => entry.InvokeAsync(inStream, CreateLambdaContext()));

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
        await context.StepAsync(async (_) => { await Task.CompletedTask; return "ok"; }, name: "s1");
        return new OrderResult { Status = "done" };
    }

    private static async Task<OrderResult> MyWorkflow(OrderEvent input, IDurableContext context)
    {
        var validation = await context.StepAsync(
            async (_) => { await Task.CompletedTask; return new ValidationResult { IsValid = true }; },
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
