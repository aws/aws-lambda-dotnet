// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.DurableExecution.Internal;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.Lambda.TestUtilities;
using Xunit;

namespace Amazon.Lambda.DurableExecution.Tests;

public class CallbackOperationTests
{
    /// <summary>Reproduces the Id that <see cref="OperationIdGenerator"/> emits for the n-th root-level operation.</summary>
    private static string IdAt(int position) => OperationIdGenerator.HashOperationId(position.ToString());

    private static TestLambdaContext CreateLambdaContext() =>
#pragma warning disable AWSLAMBDA001 // TestLambdaContext.Serializer is experimental.
        new() { Serializer = new DefaultLambdaJsonSerializer() };
#pragma warning restore AWSLAMBDA001

    private static (DurableContext context, RecordingBatcher recorder, TerminationManager tm, ExecutionState state)
        CreateContext(InitialExecutionState? initialState = null)
    {
        var state = new ExecutionState();
        state.LoadFromCheckpoint(initialState);
        var tm = new TerminationManager();
        var idGen = new OperationIdGenerator();
        var lambdaContext = CreateLambdaContext();
        var recorder = new RecordingBatcher();
        var context = new DurableContext(state, tm, new WorkflowCancellation(tm), idGen, "arn:test", lambdaContext, recorder.Batcher);
        return (context, recorder, tm, state);
    }

    /// <summary>
    /// Wires a recorder so that the next CALLBACK START flush stamps the given
    /// callback ID into <paramref name="state"/> — modeling the durable-execution
    /// service's <c>NewExecutionState</c> response that allocates the ID.
    /// </summary>
    private static void WireServiceCallbackIdAllocation(
        RecordingBatcher recorder, ExecutionState state, string callbackId)
    {
        recorder.OnFlush = ops =>
        {
            foreach (var op in ops)
            {
                if (op.Type == OperationTypes.Callback && op.Action == "START")
                {
                    state.AddOperations(new[]
                    {
                        new Operation
                        {
                            Id = op.Id,
                            Type = OperationTypes.Callback,
                            Status = OperationStatuses.Started,
                            Name = op.Name,
                            CallbackDetails = new CallbackDetails { CallbackId = callbackId }
                        }
                    });
                }
            }
        };
    }

    [Fact]
    public async Task CreateCallbackAsync_FreshExecution_FlushesStartAndReturnsCallbackId()
    {
        var (context, recorder, tm, state) = CreateContext();
        WireServiceCallbackIdAllocation(recorder, state, "cb-abc-123");

        var callback = await context.CreateCallbackAsync<string>(name: "approval");

        Assert.Equal("cb-abc-123", callback.CallbackId);
        Assert.False(tm.IsTerminated);

        await recorder.Batcher.DrainAsync();

        // CreateCallbackAsync sync-flushes a single START checkpoint.
        var single = Assert.Single(recorder.Flushed);
        Assert.Equal(OperationTypes.Callback, single.Type);
        Assert.Equal("START", single.Action);
        Assert.Equal(OperationSubTypes.Callback, single.SubType);
        Assert.Equal("approval", single.Name);
        Assert.Equal(IdAt(1), single.Id);
    }

    [Fact]
    public async Task CreateCallbackAsync_FreshExecution_NoConfig_DoesNotEmitCallbackOptions()
    {
        var (context, recorder, _, state) = CreateContext();
        WireServiceCallbackIdAllocation(recorder, state, "cb-1");

        await context.CreateCallbackAsync<string>(name: "no_options");

        await recorder.Batcher.DrainAsync();

        var single = Assert.Single(recorder.Flushed);
        Assert.Null(single.CallbackOptions);
    }

    [Fact]
    public async Task CreateCallbackAsync_FreshExecution_WithConfig_EmitsCallbackOptions()
    {
        var (context, recorder, _, state) = CreateContext();
        WireServiceCallbackIdAllocation(recorder, state, "cb-1");

        await context.CreateCallbackAsync<string>(
            name: "with_options",
            config: new CallbackConfig
            {
                Timeout = TimeSpan.FromHours(1),
                HeartbeatTimeout = TimeSpan.FromMinutes(5)
            });

        await recorder.Batcher.DrainAsync();

        var single = Assert.Single(recorder.Flushed);
        Assert.NotNull(single.CallbackOptions);
        Assert.Equal(3600, single.CallbackOptions.TimeoutSeconds);
        Assert.Equal(300, single.CallbackOptions.HeartbeatTimeoutSeconds);
    }

    [Fact]
    public async Task CreateCallbackAsync_FreshExecution_OnlyTimeout_EmitsOnlyTimeout()
    {
        var (context, recorder, _, state) = CreateContext();
        WireServiceCallbackIdAllocation(recorder, state, "cb-1");

        await context.CreateCallbackAsync<string>(
            config: new CallbackConfig { Timeout = TimeSpan.FromSeconds(45) });

        await recorder.Batcher.DrainAsync();

        var single = Assert.Single(recorder.Flushed);
        Assert.NotNull(single.CallbackOptions);
        Assert.Equal(45, single.CallbackOptions.TimeoutSeconds);
        // HeartbeatTimeout was not set → property remains at its default
        // (the AWS SDK Marshaller will not serialize the field).
        Assert.True(
            single.CallbackOptions.HeartbeatTimeoutSeconds == null
            || single.CallbackOptions.HeartbeatTimeoutSeconds == 0);
    }

    [Fact]
    public async Task CreateCallbackAsync_ServiceMissingCallbackId_ThrowsNonDeterministic()
    {
        // Service doesn't stamp a CallbackId — RecordingBatcher's OnFlush left unset.
        var (context, _, _, _) = CreateContext();

        var ex = await Assert.ThrowsAsync<NonDeterministicExecutionException>(() =>
            context.CreateCallbackAsync<string>(name: "broken"));
        Assert.Contains("CallbackId", ex.Message);
    }

    [Fact]
    public async Task GetResultAsync_FreshExecution_SuspendsExecution()
    {
        var (context, recorder, tm, state) = CreateContext();
        WireServiceCallbackIdAllocation(recorder, state, "cb-1");

        var callback = await context.CreateCallbackAsync<string>(name: "approval");

        // GetResultAsync should signal termination and return a never-completing task.
        var resultTask = callback.GetResultAsync();
        await Task.Delay(10);

        Assert.True(tm.IsTerminated);
        Assert.False(resultTask.IsCompleted);
    }

    [Fact]
    public async Task ReplayStarted_DoesNotReFlushStart_AndSuspendsOnGetResult()
    {
        // STARTED on replay = service has stamped CallbackId but no terminal yet.
        var (context, recorder, tm, _) = CreateContext(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = IdAt(1),
                    Type = OperationTypes.Callback,
                    Status = OperationStatuses.Started,
                    Name = "approval",
                    CallbackDetails = new CallbackDetails { CallbackId = "cb-replay-1" }
                }
            }
        });

        var callback = await context.CreateCallbackAsync<string>(name: "approval");
        Assert.Equal("cb-replay-1", callback.CallbackId);
        Assert.False(tm.IsTerminated);

        var resultTask = callback.GetResultAsync();
        await Task.Delay(10);

        Assert.True(tm.IsTerminated);
        Assert.False(resultTask.IsCompleted);

        // No new checkpoints — replay path doesn't re-flush START.
        await recorder.Batcher.DrainAsync();
        Assert.Empty(recorder.Flushed);
    }

    [Fact]
    public async Task ReplaySucceeded_GetResultDeserializes_NoSuspension()
    {
        var (context, recorder, tm, _) = CreateContext(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = IdAt(1),
                    Type = OperationTypes.Callback,
                    Status = OperationStatuses.Succeeded,
                    Name = "approval",
                    CallbackDetails = new CallbackDetails
                    {
                        CallbackId = "cb-done-1",
                        Result = "\"approved\""
                    }
                }
            }
        });

        var callback = await context.CreateCallbackAsync<string>(name: "approval");
        var result = await callback.GetResultAsync();

        Assert.Equal("cb-done-1", callback.CallbackId);
        Assert.Equal("approved", result);
        Assert.False(tm.IsTerminated);

        await recorder.Batcher.DrainAsync();
        Assert.Empty(recorder.Flushed);
    }

    [Fact]
    public async Task ReplaySucceeded_NullResultReturnsDefault()
    {
        var (context, _, _, _) = CreateContext(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = IdAt(1),
                    Type = OperationTypes.Callback,
                    Status = OperationStatuses.Succeeded,
                    Name = "no_payload",
                    CallbackDetails = new CallbackDetails { CallbackId = "cb-1" }
                }
            }
        });

        var callback = await context.CreateCallbackAsync<string>(name: "no_payload");
        var result = await callback.GetResultAsync();
        Assert.Null(result);
    }

    [Fact]
    public async Task ReplayFailed_GetResultThrowsCallbackFailedException()
    {
        var (context, _, _, _) = CreateContext(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = IdAt(1),
                    Type = OperationTypes.Callback,
                    Status = OperationStatuses.Failed,
                    Name = "approval",
                    CallbackDetails = new CallbackDetails
                    {
                        CallbackId = "cb-fail-1",
                        Error = new ErrorObject
                        {
                            ErrorType = "ExternalSystemError",
                            ErrorMessage = "rejected by reviewer",
                            ErrorData = "{\"reviewer\":\"jane\"}"
                        }
                    }
                }
            }
        });

        var callback = await context.CreateCallbackAsync<string>(name: "approval");

        var ex = await Assert.ThrowsAsync<CallbackFailedException>(() => callback.GetResultAsync());
        Assert.IsAssignableFrom<CallbackException>(ex);
        Assert.Equal("rejected by reviewer", ex.Message);
        Assert.Equal("cb-fail-1", ex.CallbackId);
        Assert.Equal("ExternalSystemError", ex.ErrorType);
        Assert.Equal("{\"reviewer\":\"jane\"}", ex.ErrorData);
    }

    [Fact]
    public async Task ReplayTimedOut_GetResultThrowsCallbackTimeoutException()
    {
        var (context, _, _, _) = CreateContext(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = IdAt(1),
                    Type = OperationTypes.Callback,
                    Status = OperationStatuses.TimedOut,
                    Name = "approval",
                    CallbackDetails = new CallbackDetails
                    {
                        CallbackId = "cb-to-1",
                        Error = new ErrorObject
                        {
                            ErrorMessage = "callback timed out after 24h"
                        }
                    }
                }
            }
        });

        var callback = await context.CreateCallbackAsync<string>(name: "approval");

        var ex = await Assert.ThrowsAsync<CallbackTimeoutException>(() => callback.GetResultAsync());
        Assert.IsAssignableFrom<CallbackException>(ex);
        Assert.Equal("callback timed out after 24h", ex.Message);
        Assert.Equal("cb-to-1", ex.CallbackId);
    }

    [Fact]
    public async Task ReplayTimedOut_NoErrorDetails_DefaultMessage()
    {
        var (context, _, _, _) = CreateContext(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = IdAt(1),
                    Type = OperationTypes.Callback,
                    Status = OperationStatuses.TimedOut,
                    Name = "approval",
                    CallbackDetails = new CallbackDetails { CallbackId = "cb-1" }
                }
            }
        });

        var callback = await context.CreateCallbackAsync<string>(name: "approval");
        var ex = await Assert.ThrowsAsync<CallbackTimeoutException>(() => callback.GetResultAsync());
        Assert.Equal("Callback timed out", ex.Message);
    }

    [Fact]
    public async Task ReplayUnknownStatus_ThrowsNonDeterministic()
    {
        // Replay must throw on unexpected statuses (CANCELLED, garbage, etc.)
        // rather than silently degrading to a suspend. Mirrors WaitOperation
        // and ChildContextOperation's `default:` arms.
        var (context, _, _, _) = CreateContext(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = IdAt(1),
                    Type = OperationTypes.Callback,
                    Status = "CANCELLED",
                    Name = "approval",
                    CallbackDetails = new CallbackDetails { CallbackId = "cb-1" }
                }
            }
        });

        var ex = await Assert.ThrowsAsync<NonDeterministicExecutionException>(() =>
            context.CreateCallbackAsync<string>(name: "approval"));
        Assert.Contains("unexpected status", ex.Message);
        Assert.Contains("CANCELLED", ex.Message);
    }

    [Fact]
    public async Task ReplayMissingCallbackId_ThrowsNonDeterministic()
    {
        // Replay path expects the CallbackId to be present. If it's absent, surface
        // a clear non-deterministic error rather than letting users see a NRE later.
        var (context, _, _, _) = CreateContext(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = IdAt(1),
                    Type = OperationTypes.Callback,
                    Status = OperationStatuses.Started,
                    Name = "broken",
                    CallbackDetails = new CallbackDetails { CallbackId = null }
                }
            }
        });

        await Assert.ThrowsAsync<NonDeterministicExecutionException>(() =>
            context.CreateCallbackAsync<string>(name: "broken"));
    }

    [Fact]
    public async Task ReplayDeterministic_CallbackIdStableAcrossReplays()
    {
        // Round-trip: STARTED checkpoint with CallbackId X must yield the same X
        // on replay so external systems' references remain valid.
        const string id = "stable-cb-id-12345";

        var (context, _, _, _) = CreateContext(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = IdAt(1),
                    Type = OperationTypes.Callback,
                    Status = OperationStatuses.Started,
                    Name = "approval",
                    CallbackDetails = new CallbackDetails { CallbackId = id }
                }
            }
        });

        var callback = await context.CreateCallbackAsync<string>(name: "approval");
        Assert.Equal(id, callback.CallbackId);
    }

    [Fact]
    public async Task ReplayTypeMismatch_ThrowsNonDeterministic()
    {
        // What was a CALLBACK on a previous invocation is now arriving as something
        // else — code drift detection. ExecutionState.ValidateReplayConsistency
        // is the gate.
        var (context, _, _, _) = CreateContext(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = IdAt(1),
                    Type = OperationTypes.Step,
                    Status = OperationStatuses.Succeeded,
                    Name = "approval",
                    StepDetails = new StepDetails { Result = "\"ok\"" }
                }
            }
        });

        await Assert.ThrowsAsync<NonDeterministicExecutionException>(() =>
            context.CreateCallbackAsync<string>(name: "approval"));
    }

    [Fact]
    public async Task CreateCallbackAsync_CallbackIdAccessBeforeStart_Throws()
    {
        // Direct construction of the CallbackOperation without going through
        // ExecuteAsync — guard against bugs that try to read CallbackId early.
        var op = new CallbackOperation<string>(
            "op-id", "name", parentId: null, null, new DefaultLambdaJsonSerializer(),
            new ExecutionState(), new TerminationManager(), "arn", batcher: null);

        Assert.Throws<InvalidOperationException>(() => _ = ((ICallback<string>)op).CallbackId);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task CreateCallbackAsync_NoSerializer_Throws()
    {
        // No ILambdaSerializer registered on the LambdaContext — surface a clear
        // error instead of letting users see a NRE later.
        var state = new ExecutionState();
        var tm = new TerminationManager();
        var idGen = new OperationIdGenerator();
        var lambdaContext = new TestLambdaContext();  // no Serializer set
        var recorder = new RecordingBatcher();
        var context = new DurableContext(state, tm, new WorkflowCancellation(tm), idGen, "arn:test", lambdaContext, recorder.Batcher);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            context.CreateCallbackAsync<string>(name: "no-serializer"));
        Assert.Contains("ILambdaSerializer", ex.Message);
    }
}
