// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.DurableExecution.Internal;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.Lambda.TestUtilities;
using Xunit;

namespace Amazon.Lambda.DurableExecution.Tests;

public class InvokeOperationTests
{
    /// <summary>Reproduces the Id that <see cref="OperationIdGenerator"/> emits for the n-th root-level operation.</summary>
    private static string IdAt(int position) => OperationIdGenerator.HashOperationId(position.ToString());

    private const string FunctionArn = "arn:aws:lambda:us-east-1:123456789012:function:downstream:prod";

    private static (DurableContext context, RecordingBatcher recorder, TerminationManager tm, ExecutionState state)
        CreateContext(InitialExecutionState? initialState = null)
    {
        var state = new ExecutionState();
        state.LoadFromCheckpoint(initialState);
        var tm = new TerminationManager();
        var idGen = new OperationIdGenerator();
#pragma warning disable AWSLAMBDA001 // TestLambdaContext.Serializer is experimental.
        var lambdaContext = new TestLambdaContext { Serializer = new DefaultLambdaJsonSerializer() };
#pragma warning restore AWSLAMBDA001
        var recorder = new RecordingBatcher();
        var context = new DurableContext(state, tm, idGen, "arn:test", lambdaContext, recorder.Batcher);
        return (context, recorder, tm, state);
    }

    #region Argument validation

    [Fact]
    public async Task InvokeAsync_NullFunctionName_ThrowsArgumentNullException()
    {
        var (context, _, _, _) = CreateContext();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            context.InvokeAsync<string, string>(functionName: null!, payload: "x"));
    }

    [Fact]
    public async Task InvokeAsync_EmptyFunctionName_ThrowsArgumentException()
    {
        var (context, _, _, _) = CreateContext();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            context.InvokeAsync<string, string>(functionName: "", payload: "x"));
    }

    [Fact]
    public async Task InvokeAsync_WhitespaceFunctionName_ThrowsArgumentException()
    {
        var (context, _, _, _) = CreateContext();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            context.InvokeAsync<string, string>(functionName: "   ", payload: "x"));
    }

    [Fact]
    public async Task InvokeAsync_PreservesUnqualifiedArn_AndPassesItThrough()
    {
        // The SDK does NOT regex-validate qualified ARNs. The service enforces
        // that rule. We verify the value is propagated unmodified to the
        // ChainedInvokeOptions.FunctionName so that service-side rejection
        // surfaces with the user's exact input.
        var (context, recorder, tm, _) = CreateContext();

        var task = context.InvokeAsync<string, string>(
            "arn:aws:lambda:us-east-1:123456789012:function:no-version",
            payload: "x",
            name: "noversion");

        await Task.Delay(20);
        Assert.True(tm.IsTerminated);
        Assert.False(task.IsCompleted);

        var start = recorder.Flushed.Single(o => o.Action == "START");
        Assert.Equal("arn:aws:lambda:us-east-1:123456789012:function:no-version",
            start.ChainedInvokeOptions.FunctionName);
    }

    #endregion

    #region Fresh execution

    [Fact]
    public async Task InvokeAsync_FreshExecution_CheckpointsStartAndSuspends()
    {
        var (context, recorder, tm, _) = CreateContext();

        var task = context.InvokeAsync<RequestPayload, ResponsePayload>(
            FunctionArn,
            new RequestPayload { Amount = 100, Currency = "USD" },
            name: "process_payment",
            config: new InvokeConfig { TenantId = "tenant-A" });

        // Service-side suspend mechanics: TerminationManager fires before the
        // user task completes; the task itself never resolves on the fresh path.
        await Task.Delay(20);
        Assert.True(tm.IsTerminated);
        Assert.False(task.IsCompleted);

        await recorder.Batcher.DrainAsync();

        var start = recorder.Flushed.Single();
        Assert.Equal("CHAINED_INVOKE", start.Type);
        Assert.Equal("START", start.Action);
        Assert.Equal("ChainedInvoke", start.SubType);
        Assert.Equal("process_payment", start.Name);
        Assert.Equal(IdAt(1), start.Id);

        // Payload is JSON-serialized via the registered ILambdaSerializer.
        Assert.Contains("\"Amount\":100", start.Payload);
        Assert.Contains("\"Currency\":\"USD\"", start.Payload);

        // ChainedInvokeOptions carries function name + tenant id.
        Assert.NotNull(start.ChainedInvokeOptions);
        Assert.Equal(FunctionArn, start.ChainedInvokeOptions.FunctionName);
        Assert.Equal("tenant-A", start.ChainedInvokeOptions.TenantId);
    }

    [Fact]
    public async Task InvokeAsync_FreshExecution_NoTenantId_OmitsTenantId()
    {
        var (context, recorder, tm, _) = CreateContext();

        var task = context.InvokeAsync<string, string>(FunctionArn, "payload", name: "no_tenant");

        await Task.Delay(20);
        Assert.True(tm.IsTerminated);
        Assert.False(task.IsCompleted);

        await recorder.Batcher.DrainAsync();

        var start = recorder.Flushed.Single();
        Assert.NotNull(start.ChainedInvokeOptions);
        Assert.Equal(FunctionArn, start.ChainedInvokeOptions.FunctionName);
        // null tenant means the SDK didn't set the field; the AWS SDK model's
        // IsSet property is what callers actually inspect, but the easy
        // deterministic assertion is that the property is null.
        Assert.Null(start.ChainedInvokeOptions.TenantId);
    }

    [Fact]
    public async Task InvokeAsync_FreshExecution_StartIsSyncFlushed()
    {
        // Critical correctness invariant: START must be flushed BEFORE we
        // suspend. A queued-but-unflushed START is "the service doesn't know
        // about the chained invocation," so the parent suspends forever.
        var (context, recorder, tm, _) = CreateContext();

        var task = context.InvokeAsync<string, string>(FunctionArn, "x", name: "sync_flush");
        await Task.Delay(20);

        Assert.True(tm.IsTerminated);
        Assert.False(task.IsCompleted);

        // No DrainAsync — the START must already be flushed at the moment
        // suspension is signaled. This mirrors WaitOperation_NewExecution_SignalsTermination's
        // contract: TerminationManager firing implies the matching START is durable.
        Assert.Single(recorder.Flushed);
        Assert.Equal("START", recorder.Flushed[0].Action);
    }

    [Fact]
    public async Task InvokeAsync_TerminationReason_IsInvokePending()
    {
        var (context, _, tm, _) = CreateContext();

        _ = context.InvokeAsync<string, string>(FunctionArn, "x", name: "reason_check");
        var termination = await tm.TerminationTask;

        Assert.Equal(TerminationReason.InvokePending, termination.Reason);
    }

    [Fact]
    public async Task InvokeAsync_NoSerializerRegistered_ThrowsInvalidOperationException()
    {
        // If the user constructs a Lambda runtime without a serializer (or in
        // tests, neglects to set TestLambdaContext.Serializer), InvokeAsync
        // surfaces a helpful error rather than NREing inside InvokeOperation.
        var state = new ExecutionState();
        state.LoadFromCheckpoint(null);
        var tm = new TerminationManager();
        var idGen = new OperationIdGenerator();
        var lambdaContext = new TestLambdaContext(); // no serializer!
        var recorder = new RecordingBatcher();
        var context = new DurableContext(state, tm, idGen, "arn:test", lambdaContext, recorder.Batcher);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            context.InvokeAsync<string, string>(FunctionArn, "x", name: "no_serializer"));
    }

    #endregion

    #region Replay — terminal status mapping

    [Fact]
    public async Task InvokeAsync_ReplaySucceeded_ReturnsCachedResultWithoutRescheduling()
    {
        var (context, recorder, tm, _) = CreateContext(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = IdAt(1),
                    Type = OperationTypes.ChainedInvoke,
                    Status = OperationStatuses.Succeeded,
                    Name = "cached",
                    ChainedInvokeDetails = new ChainedInvokeDetails
                    {
                        Result = "{\"OrderId\":\"abc\",\"Total\":42}"
                    }
                }
            }
        });

        var result = await context.InvokeAsync<string, ResponsePayload>(
            FunctionArn, "x", name: "cached");

        Assert.False(tm.IsTerminated);
        Assert.Equal("abc", result.OrderId);
        Assert.Equal(42, result.Total);

        await recorder.Batcher.DrainAsync();
        Assert.Empty(recorder.Flushed);
    }

    [Fact]
    public async Task InvokeAsync_ReplayFailed_ThrowsInvokeFailedException()
    {
        var (context, _, _, _) = CreateContext(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = IdAt(1),
                    Type = OperationTypes.ChainedInvoke,
                    Status = OperationStatuses.Failed,
                    Name = "boom",
                    ChainedInvokeDetails = new ChainedInvokeDetails
                    {
                        Error = new ErrorObject
                        {
                            ErrorType = "System.InvalidOperationException",
                            ErrorMessage = "downstream exploded",
                            ErrorData = "{\"detail\":\"x\"}",
                            StackTrace = new[] { "at A.B()", "at C.D()" }
                        }
                    }
                }
            }
        });

        var ex = await Assert.ThrowsAsync<InvokeFailedException>(() =>
            context.InvokeAsync<string, string>(FunctionArn, "x", name: "boom"));

        Assert.Equal("downstream exploded", ex.Message);
        Assert.Equal(FunctionArn, ex.FunctionName);
        Assert.Equal("System.InvalidOperationException", ex.ErrorType);
        Assert.Equal("{\"detail\":\"x\"}", ex.ErrorData);
        Assert.NotNull(ex.OriginalStackTrace);
        Assert.Equal(2, ex.OriginalStackTrace!.Count);

        // Subclass relationship — `catch (InvokeException)` catches all three.
        Assert.IsAssignableFrom<InvokeException>(ex);
    }

    [Fact]
    public async Task InvokeAsync_ReplayTimedOut_ThrowsInvokeTimedOutException()
    {
        var (context, _, _, _) = CreateContext(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = IdAt(1),
                    Type = OperationTypes.ChainedInvoke,
                    Status = OperationStatuses.TimedOut,
                    Name = "slow",
                    ChainedInvokeDetails = new ChainedInvokeDetails
                    {
                        Error = new ErrorObject
                        {
                            ErrorMessage = "execution timed out after 60s"
                        }
                    }
                }
            }
        });

        var ex = await Assert.ThrowsAsync<InvokeTimedOutException>(() =>
            context.InvokeAsync<string, string>(FunctionArn, "x", name: "slow"));

        Assert.Equal("execution timed out after 60s", ex.Message);
        Assert.Equal(FunctionArn, ex.FunctionName);
        Assert.IsAssignableFrom<InvokeException>(ex);
    }

    [Fact]
    public async Task InvokeAsync_ReplayStopped_ThrowsInvokeStoppedException()
    {
        var (context, _, _, _) = CreateContext(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = IdAt(1),
                    Type = OperationTypes.ChainedInvoke,
                    Status = OperationStatuses.Stopped,
                    Name = "stopped"
                }
            }
        });

        var ex = await Assert.ThrowsAsync<InvokeStoppedException>(() =>
            context.InvokeAsync<string, string>(FunctionArn, "x", name: "stopped"));

        // No recorded ErrorMessage → fallback default.
        Assert.Equal("Chained invoke was stopped.", ex.Message);
        Assert.Equal(FunctionArn, ex.FunctionName);
        Assert.IsAssignableFrom<InvokeException>(ex);
    }

    [Fact]
    public async Task InvokeAsync_ReplayStarted_ResuspendsWithoutRecheckpoint()
    {
        // Service hasn't reached terminal yet. The original START is still
        // authoritative; do not re-emit, just suspend.
        var (context, recorder, tm, _) = CreateContext(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = IdAt(1),
                    Type = OperationTypes.ChainedInvoke,
                    Status = OperationStatuses.Started,
                    Name = "still_running"
                }
            }
        });

        var task = context.InvokeAsync<string, string>(FunctionArn, "x", name: "still_running");
        await Task.Delay(20);

        Assert.True(tm.IsTerminated);
        Assert.False(task.IsCompleted);

        // Crucially: no checkpoint was emitted. Original START is authoritative.
        Assert.Empty(recorder.Flushed);
    }

    [Fact]
    public async Task InvokeAsync_ReplayPending_ResuspendsWithoutRecheckpoint()
    {
        var (context, recorder, tm, _) = CreateContext(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = IdAt(1),
                    Type = OperationTypes.ChainedInvoke,
                    Status = OperationStatuses.Pending,
                    Name = "pending"
                }
            }
        });

        var task = context.InvokeAsync<string, string>(FunctionArn, "x", name: "pending");
        await Task.Delay(20);

        Assert.True(tm.IsTerminated);
        Assert.False(task.IsCompleted);
        Assert.Empty(recorder.Flushed);
    }

    [Fact]
    public async Task InvokeAsync_ReplayUnknownStatus_ThrowsNonDeterministicException()
    {
        var (context, _, _, _) = CreateContext(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = IdAt(1),
                    Type = OperationTypes.ChainedInvoke,
                    Status = "TOTALLY_BOGUS",
                    Name = "mystery"
                }
            }
        });

        await Assert.ThrowsAsync<NonDeterministicExecutionException>(() =>
            context.InvokeAsync<string, string>(FunctionArn, "x", name: "mystery"));
    }

    [Fact]
    public async Task InvokeAsync_ReplayTypeMismatch_ThrowsNonDeterministicException()
    {
        var (context, _, _, _) = CreateContext(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = IdAt(1),
                    Type = OperationTypes.Step,                  // wrong type
                    Status = OperationStatuses.Succeeded,
                    Name = "kept_consistent",
                    StepDetails = new StepDetails { Result = "\"x\"" }
                }
            }
        });

        var ex = await Assert.ThrowsAsync<NonDeterministicExecutionException>(() =>
            context.InvokeAsync<string, string>(FunctionArn, "x", name: "kept_consistent"));

        Assert.Contains("expected type 'CHAINED_INVOKE'", ex.Message);
        Assert.Contains("found 'STEP'", ex.Message);
    }

    #endregion

    #region Serialization

    [Fact]
    public async Task InvokeAsync_DeserializesResultViaRegisteredSerializer()
    {
        var (context, _, _, _) = CreateContext(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = IdAt(1),
                    Type = OperationTypes.ChainedInvoke,
                    Status = OperationStatuses.Succeeded,
                    Name = "json_result",
                    ChainedInvokeDetails = new ChainedInvokeDetails
                    {
                        Result = "{\"OrderId\":\"o-7\",\"Total\":1024}"
                    }
                }
            }
        });

        var result = await context.InvokeAsync<RequestPayload, ResponsePayload>(
            FunctionArn,
            new RequestPayload { Amount = 1, Currency = "USD" },
            name: "json_result");

        Assert.Equal("o-7", result.OrderId);
        Assert.Equal(1024, result.Total);
    }

    #endregion

    #region End-to-end suspension / resume parity

    [Fact]
    public async Task EndToEnd_StepInvokeStep_FirstInvocation_SuspendsOnInvoke()
    {
        var tm = new TerminationManager();
        var state = new ExecutionState();
        state.LoadFromCheckpoint(null);
        var idGen = new OperationIdGenerator();
#pragma warning disable AWSLAMBDA001
        var lambdaContext = new TestLambdaContext { Serializer = new DefaultLambdaJsonSerializer() };
#pragma warning restore AWSLAMBDA001
        var batcher = new RecordingBatcher();
        var context = new DurableContext(state, tm, idGen, "arn:test", lambdaContext, batcher.Batcher);

        var result = await DurableExecutionHandler.RunAsync<string>(
            state, tm,
            async () =>
            {
                await context.StepAsync(async (_) => { await Task.CompletedTask; return "validated"; }, name: "validate");
                var paymentId = await context.InvokeAsync<string, string>(
                    FunctionArn, "validated", name: "process_payment");
                return await context.StepAsync(async (_) => { await Task.CompletedTask; return paymentId + "-done"; }, name: "finalize");
            });

        Assert.Equal(InvocationStatus.Pending, result.Status);

        await batcher.Batcher.DrainAsync();
        Assert.Contains(batcher.Flushed, o => o.Type == "CHAINED_INVOKE" && o.Action == "START");
        Assert.DoesNotContain(batcher.Flushed, o => o.Type == "STEP" && o.Name == "finalize");
    }

    [Fact]
    public async Task EndToEnd_StepInvokeStep_SecondInvocation_ResumesAndCompletes()
    {
        var tm = new TerminationManager();
        var state = new ExecutionState();
        state.LoadFromCheckpoint(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = IdAt(1),
                    Type = OperationTypes.Step,
                    Status = OperationStatuses.Succeeded,
                    Name = "validate",
                    StepDetails = new StepDetails { Result = "\"validated\"" }
                },
                new()
                {
                    Id = IdAt(2),
                    Type = OperationTypes.ChainedInvoke,
                    Status = OperationStatuses.Succeeded,
                    Name = "process_payment",
                    ChainedInvokeDetails = new ChainedInvokeDetails { Result = "\"pmt-42\"" }
                }
            }
        });

        var idGen = new OperationIdGenerator();
#pragma warning disable AWSLAMBDA001
        var lambdaContext = new TestLambdaContext { Serializer = new DefaultLambdaJsonSerializer() };
#pragma warning restore AWSLAMBDA001
        var context = new DurableContext(state, tm, idGen, "arn:test", lambdaContext);
        var finalizeRan = false;

        var result = await DurableExecutionHandler.RunAsync<string>(
            state, tm,
            async () =>
            {
                var validated = await context.StepAsync(async (_) => { await Task.CompletedTask; return "fresh-validated"; }, name: "validate");
                Assert.Equal("validated", validated); // cached

                var paymentId = await context.InvokeAsync<string, string>(
                    FunctionArn, validated, name: "process_payment");
                Assert.Equal("pmt-42", paymentId); // cached

                return await context.StepAsync(async (_) =>
                {
                    finalizeRan = true;
                    await Task.CompletedTask;
                    return paymentId + "-done";
                }, name: "finalize");
            });

        Assert.Equal(InvocationStatus.Succeeded, result.Status);
        Assert.Equal("pmt-42-done", result.Result);
        Assert.True(finalizeRan);
    }

    #endregion

    #region Test-only types

    private class RequestPayload
    {
        public int Amount { get; set; }
        public string? Currency { get; set; }
    }

    private class ResponsePayload
    {
        public string? OrderId { get; set; }
        public long Total { get; set; }
    }

    #endregion
}
