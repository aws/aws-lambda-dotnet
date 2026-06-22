// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.DurableExecution.Internal;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.Lambda.TestUtilities;
using Xunit;

namespace Amazon.Lambda.DurableExecution.Tests;

public class ChildContextOperationTests
{
    /// <summary>Reproduces the Id that <see cref="OperationIdGenerator"/> emits for the n-th root-level operation.</summary>
    private static string IdAt(int position) => OperationIdGenerator.HashOperationId(position.ToString());

    /// <summary>The hashed ID of the n-th child operation under <paramref name="parentOpId"/>.</summary>
    private static string ChildIdAt(string parentOpId, int position) =>
        OperationIdGenerator.HashOperationId($"{parentOpId}-{position}");

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
        var context = new DurableContext(state, tm, new WorkflowCancellation(tm), idGen, "arn:test", lambdaContext, recorder.Batcher);
        return (context, recorder, tm, state);
    }

    [Fact]
    public async Task RunInChildContextAsync_FreshExecution_RunsFuncAndCheckpoints()
    {
        var (context, recorder, tm, _) = CreateContext();

        var executed = false;
        var result = await context.RunInChildContextAsync(
            async (childCtx, _) =>
            {
                executed = true;
                return await childCtx.StepAsync(async (_, _) => { await Task.CompletedTask; return "inner"; }, name: "inner_step");
            },
            name: "phase");

        Assert.True(executed);
        Assert.Equal("inner", result);
        Assert.False(tm.IsTerminated);

        // CONTEXT START → STEP START (fire-and-forget, but flushed before drain)
        // → STEP SUCCEED → CONTEXT SUCCEED
        await recorder.Batcher.DrainAsync();

        var actions = recorder.Flushed.Select(o => $"{o.Type}:{o.Action}").ToArray();
        Assert.Equal(new[]
        {
            "CONTEXT:START",
            "STEP:START",
            "STEP:SUCCEED",
            "CONTEXT:SUCCEED"
        }, actions);

        var contextSucceed = recorder.Flushed.Single(o => o.Type == "CONTEXT" && o.Action == "SUCCEED");
        Assert.Equal(IdAt(1), contextSucceed.Id);
        Assert.Equal("phase", contextSucceed.Name);
        Assert.Equal("\"inner\"", contextSucceed.Payload);
    }

    [Fact]
    public async Task RunInChildContextAsync_FreshExecution_ChildOperationIdsDeterministic()
    {
        var (context, recorder, _, _) = CreateContext();

        await context.RunInChildContextAsync(
            async (childCtx, _) =>
            {
                await childCtx.StepAsync(async (_, _) => { await Task.CompletedTask; return "a"; }, name: "first");
                await childCtx.StepAsync(async (_, _) => { await Task.CompletedTask; return "b"; }, name: "second");
                return 0;
            },
            name: "phase");

        await recorder.Batcher.DrainAsync();

        var parentOpId = IdAt(1);
        var firstChildOpId = ChildIdAt(parentOpId, 1);
        var secondChildOpId = ChildIdAt(parentOpId, 2);

        var stepStarts = recorder.Flushed.Where(o => o.Type == "STEP" && o.Action == "START").ToArray();
        Assert.Equal(2, stepStarts.Length);
        Assert.Equal(firstChildOpId, stepStarts[0].Id);
        Assert.Equal(secondChildOpId, stepStarts[1].Id);
    }

    [Fact]
    public async Task RunInChildContextAsync_ReplaySucceeded_ReturnsCachedAndDoesNotRun()
    {
        var (context, recorder, _, _) = CreateContext(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = IdAt(1),
                    Type = OperationTypes.Context,
                    Status = OperationStatuses.Succeeded,
                    Name = "phase",
                    ContextDetails = new ContextDetails { Result = "\"cached\"" }
                }
            }
        });

        var executed = false;
        var result = await context.RunInChildContextAsync(
            async (childCtx, _) =>
            {
                executed = true;
                await Task.CompletedTask;
                return "fresh";
            },
            name: "phase");

        Assert.False(executed);
        Assert.Equal("cached", result);

        await recorder.Batcher.DrainAsync();
        Assert.Empty(recorder.Flushed);
    }

    [Fact]
    public async Task RunInChildContextAsync_ReplayFailed_ThrowsChildContextException()
    {
        var (context, recorder, _, _) = CreateContext(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = IdAt(1),
                    Type = OperationTypes.Context,
                    Status = OperationStatuses.Failed,
                    Name = "phase",
                    SubType = "WaitForCallback",
                    ContextDetails = new ContextDetails
                    {
                        Error = new ErrorObject
                        {
                            ErrorType = "System.InvalidOperationException",
                            ErrorMessage = "child went wrong",
                            ErrorData = "{\"detail\":\"x\"}",
                            StackTrace = new[] { "at A.B()", "at C.D()" }
                        }
                    }
                }
            }
        });

        var ex = await Assert.ThrowsAsync<ChildContextException>(() =>
            context.RunInChildContextAsync<string>(
                async (_, _) => { await Task.CompletedTask; return "should not run"; },
                name: "phase"));

        Assert.Equal("child went wrong", ex.Message);
        Assert.Equal("System.InvalidOperationException", ex.ErrorType);
        Assert.Equal("{\"detail\":\"x\"}", ex.ErrorData);
        Assert.Equal("WaitForCallback", ex.SubType);
        Assert.NotNull(ex.OriginalStackTrace);
        Assert.Equal(2, ex.OriginalStackTrace!.Count);

        await recorder.Batcher.DrainAsync();
        Assert.Empty(recorder.Flushed);
    }

    [Fact]
    public async Task RunInChildContextAsync_ReplayFailed_AppliesErrorMapping()
    {
        var (context, _, _, _) = CreateContext(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = IdAt(1),
                    Type = OperationTypes.Context,
                    Status = OperationStatuses.Failed,
                    Name = "phase",
                    ContextDetails = new ContextDetails
                    {
                        Error = new ErrorObject
                        {
                            ErrorType = "System.InvalidOperationException",
                            ErrorMessage = "boom"
                        }
                    }
                }
            }
        });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            context.RunInChildContextAsync<string>(
                async (_, _) => { await Task.CompletedTask; return "x"; },
                name: "phase",
                config: new ChildContextConfig
                {
                    // Mapper sees the ChildContextException and remaps to a
                    // domain-specific exception, preserving the original via
                    // InnerException.
                    ErrorMapping = e => new InvalidOperationException("mapped", e)
                }));

        Assert.Equal("mapped", ex.Message);
        Assert.IsType<ChildContextException>(ex.InnerException);
    }

    [Fact]
    public async Task RunInChildContextAsync_FuncThrows_CheckpointsFailAndThrows()
    {
        var (context, recorder, _, _) = CreateContext();

        var ex = await Assert.ThrowsAsync<ChildContextException>(() =>
            context.RunInChildContextAsync<string>(
                async (_, _) => { await Task.CompletedTask; throw new InvalidOperationException("inner boom"); },
                name: "phase"));

        Assert.Equal("inner boom", ex.Message);
        Assert.Equal("System.InvalidOperationException", ex.ErrorType);
        // Fresh-path failures populate OriginalStackTrace alongside ErrorType so
        // ErrorMapping callbacks see the same shape on both fresh and replay paths.
        Assert.NotNull(ex.OriginalStackTrace);
        Assert.NotEmpty(ex.OriginalStackTrace!);

        await recorder.Batcher.DrainAsync();
        var contextActions = recorder.Flushed
            .Where(o => o.Type == "CONTEXT")
            .Select(o => o.Action.ToString())
            .ToArray();
        Assert.Equal(new[] { "START", "FAIL" }, contextActions);
    }

    [Fact]
    public async Task RunInChildContextAsync_InnerNonDeterminism_BubblesUpWithoutCheckpointingFail()
    {
        // A child context whose inner step's checkpoint type doesn't match the
        // user code (replay mismatch) must NOT be wrapped/checkpointed as
        // CONTEXT FAIL — that would freeze the corruption into history.
        var parentOpId = IdAt(1);
        var innerOpId = ChildIdAt(parentOpId, 1);

        var (context, recorder, _, _) = CreateContext(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = parentOpId,
                    Type = OperationTypes.Context,
                    Status = OperationStatuses.Started,
                    Name = "phase"
                },
                new()
                {
                    Id = innerOpId,
                    Type = OperationTypes.Wait,            // wrong type — code calls StepAsync
                    Status = OperationStatuses.Succeeded,
                    Name = "inner_step"
                }
            }
        });

        await Assert.ThrowsAsync<NonDeterministicExecutionException>(() =>
            context.RunInChildContextAsync(
                async (childCtx, _) =>
                {
                    return await childCtx.StepAsync(
                        async (_, _) => { await Task.CompletedTask; return "x"; },
                        name: "inner_step");
                },
                name: "phase"));

        await recorder.Batcher.DrainAsync();
        Assert.DoesNotContain(recorder.Flushed, o => o.Type == "CONTEXT" && o.Action == "FAIL");
    }

    [Fact]
    public async Task RunInChildContextAsync_FuncThrows_AppliesErrorMapping()
    {
        var (context, _, _, _) = CreateContext();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            context.RunInChildContextAsync<string>(
                async (_, _) => { await Task.CompletedTask; throw new TimeoutException("inner timeout"); },
                name: "phase",
                config: new ChildContextConfig
                {
                    ErrorMapping = e => new InvalidOperationException("mapped", e)
                }));

        Assert.Equal("mapped", ex.Message);
        Assert.IsType<ChildContextException>(ex.InnerException);
    }

    [Fact]
    public async Task RunInChildContextAsync_ChildSuspendsOnWait_TerminatesWithWaitScheduled()
    {
        var (context, recorder, tm, _) = CreateContext();

        // Suspending child: the inner Wait flushes WAIT START sync, then
        // returns a never-completing Task via TerminationManager.SuspendAndAwait.
        // The outer ChildContextOperation awaits that and never reaches
        // CONTEXT SUCCEED. DurableExecutionHandler.RunAsync's WhenAny race
        // wins on the termination signal; the test below short-circuits via
        // the same TerminationManager.IsTerminated check.
        var task = context.RunInChildContextAsync(
            async (childCtx, _) =>
            {
                await childCtx.WaitAsync(TimeSpan.FromSeconds(5), name: "wait_inside");
                return "should not return";
            },
            name: "phase");

        await Task.Delay(50);

        Assert.True(tm.IsTerminated);
        Assert.False(task.IsCompleted);

        // CONTEXT START + WAIT START have flushed; no SUCCEED/FAIL since the
        // child is suspended.
        var actions = recorder.Flushed.Select(o => $"{o.Type}:{o.Action}").ToArray();
        Assert.Contains("CONTEXT:START", actions);
        Assert.Contains("WAIT:START", actions);
        Assert.DoesNotContain("CONTEXT:SUCCEED", actions);
        Assert.DoesNotContain("CONTEXT:FAIL", actions);
    }

    [Fact]
    public async Task RunInChildContextAsync_ReplayStarted_ReExecutesFuncWithInnerCacheReplay()
    {
        var parentOpId = IdAt(1);
        var innerStepOpId = ChildIdAt(parentOpId, 1);

        var (context, recorder, _, _) = CreateContext(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = parentOpId,
                    Type = OperationTypes.Context,
                    Status = OperationStatuses.Started,
                    Name = "phase"
                },
                new()
                {
                    Id = innerStepOpId,
                    Type = OperationTypes.Step,
                    Status = OperationStatuses.Succeeded,
                    Name = "inner_step",
                    StepDetails = new StepDetails { Result = "\"cached_inner\"" }
                }
            }
        });

        var innerExecuted = false;
        var result = await context.RunInChildContextAsync(
            async (childCtx, _) =>
            {
                return await childCtx.StepAsync(
                    async (_, _) => { innerExecuted = true; await Task.CompletedTask; return "fresh_inner"; },
                    name: "inner_step");
            },
            name: "phase");

        // The user func re-runs (replay propagation), but its inner step
        // replays the cached value without invoking the inner code.
        Assert.False(innerExecuted);
        Assert.Equal("cached_inner", result);

        await recorder.Batcher.DrainAsync();

        // Critical: do NOT re-checkpoint CONTEXT START on replay. The original
        // STARTED checkpoint is still authoritative.
        Assert.DoesNotContain(recorder.Flushed, o => o.Type == "CONTEXT" && o.Action == "START");

        // The CONTEXT SUCCEED happens only this time, since the user func
        // returned successfully.
        Assert.Contains(recorder.Flushed, o => o.Type == "CONTEXT" && o.Action == "SUCCEED");
    }

    [Fact]
    public async Task RunInChildContextAsync_VoidOverload_RunsAndCheckpoints()
    {
        var (context, recorder, _, _) = CreateContext();

        var executed = false;
        await context.RunInChildContextAsync(
            async (childCtx, _) =>
            {
                await childCtx.StepAsync(
                    async (_, _) => { executed = true; await Task.CompletedTask; },
                    name: "inner_void");
            },
            name: "phase");

        Assert.True(executed);

        await recorder.Batcher.DrainAsync();

        var actions = recorder.Flushed.Select(o => $"{o.Type}:{o.Action}").ToArray();
        Assert.Equal(new[]
        {
            "CONTEXT:START",
            "STEP:START",
            "STEP:SUCCEED",
            "CONTEXT:SUCCEED"
        }, actions);

        // Void overload returns a null object<?>, which the registered
        // ILambdaSerializer serializes as the literal "null" payload.
        var contextSucceed = recorder.Flushed.Single(o => o.Type == "CONTEXT" && o.Action == "SUCCEED");
        Assert.Equal("null", contextSucceed.Payload);
    }

    [Fact]
    public async Task RunInChildContextAsync_ReplayTypeMismatch_ThrowsNonDeterministicException()
    {
        var (context, _, _, _) = CreateContext(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = IdAt(1),
                    Type = OperationTypes.Step,           // wrong type — should be CONTEXT
                    Status = OperationStatuses.Succeeded,
                    Name = "phase",
                    StepDetails = new StepDetails { Result = "\"x\"" }
                }
            }
        });

        var ex = await Assert.ThrowsAsync<NonDeterministicExecutionException>(() =>
            context.RunInChildContextAsync<string>(
                async (_, _) => { await Task.CompletedTask; return "x"; },
                name: "phase"));

        Assert.Contains("expected type 'CONTEXT'", ex.Message);
        Assert.Contains("found 'STEP'", ex.Message);
    }

    [Fact]
    public async Task RunInChildContextAsync_ReplayNameMismatch_ThrowsNonDeterministicException()
    {
        var (context, _, _, _) = CreateContext(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = IdAt(1),
                    Type = OperationTypes.Context,
                    Status = OperationStatuses.Succeeded,
                    Name = "old_name",
                    ContextDetails = new ContextDetails { Result = "\"x\"" }
                }
            }
        });

        var ex = await Assert.ThrowsAsync<NonDeterministicExecutionException>(() =>
            context.RunInChildContextAsync<string>(
                async (_, _) => { await Task.CompletedTask; return "x"; },
                name: "new_name"));

        Assert.Contains("expected name 'new_name'", ex.Message);
        Assert.Contains("found 'old_name'", ex.Message);
    }

    [Fact]
    public async Task RunInChildContextAsync_ReplayUnknownStatus_ThrowsNonDeterministicException()
    {
        var (context, _, _, _) = CreateContext(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = IdAt(1),
                    Type = OperationTypes.Context,
                    Status = "BOGUS",
                    Name = "phase"
                }
            }
        });

        await Assert.ThrowsAsync<NonDeterministicExecutionException>(() =>
            context.RunInChildContextAsync<string>(
                async (_, _) => { await Task.CompletedTask; return "x"; },
                name: "phase"));
    }

    [Fact]
    public async Task RunInChildContextAsync_ResultOverThreshold_EmitsEmptyPayloadAndReplayChildren()
    {
        var (context, recorder, _, _) = CreateContext();
        var big = new string('y', 300 * 1024);

        var result = await context.RunInChildContextAsync(
            async (_, _) => { await Task.Yield(); return big; },
            name: "phase");

        Assert.Equal(big, result); // in-memory value intact for this invoke

        await recorder.Batcher.DrainAsync();

        var succeed = recorder.Flushed.Single(o =>
            o.Type == "CONTEXT" && o.Action == "SUCCEED");
        Assert.Equal(string.Empty, succeed.Payload);
        Assert.NotNull(succeed.ContextOptions);
        Assert.True(succeed.ContextOptions.ReplayChildren);
    }

    [Fact]
    public async Task RunInChildContextAsync_ReplayChildren_ReExecutesBodyWithoutRecheckpoint()
    {
        var childOpId = IdAt(1); // first root-level op

        var (context, recorder, _, _) = CreateContext(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = childOpId,
                    Type = OperationTypes.Context,
                    Status = OperationStatuses.Succeeded,
                    Name = "phase",
                    // Result == "" matches the overflow emission (string.Empty).
                    ContextDetails = new ContextDetails { Result = "", ReplayChildren = true }
                }
            }
        });

        var executed = false;
        var result = await context.RunInChildContextAsync(
            async (_, _) => { executed = true; await Task.Yield(); return "rebuilt"; },
            name: "phase");

        Assert.True(executed);
        Assert.Equal("rebuilt", result);

        await recorder.Batcher.DrainAsync();
        // Already-terminal child must not be re-checkpointed.
        Assert.DoesNotContain(recorder.Flushed, o => o.Type == "CONTEXT" && o.Action == "SUCCEED");
    }

    [Fact]
    public async Task RunInChildContextAsync_ReplayChildren_BodyThrows_DoesNotEmitFailCheckpoint()
    {
        var childOpId = IdAt(1); // first root-level op

        var (context, recorder, _, _) = CreateContext(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = childOpId,
                    Type = OperationTypes.Context,
                    Status = OperationStatuses.Succeeded,
                    Name = "phase",
                    // Result == "" matches the overflow emission (string.Empty).
                    ContextDetails = new ContextDetails { Result = "", ReplayChildren = true }
                }
            }
        });

        // The op is already terminal (SUCCEEDED). If the overflow re-run body
        // throws, the recovery path must NOT re-checkpoint a CONTEXT FAIL over
        // the already-SUCCEEDED record — but the exception still propagates.
        await Assert.ThrowsAsync<ChildContextException>(() =>
            context.RunInChildContextAsync<string>(
                async (_, _) => { await Task.Yield(); throw new InvalidOperationException("nondeterministic re-run"); },
                name: "phase"));

        await recorder.Batcher.DrainAsync();
        Assert.DoesNotContain(recorder.Flushed, o => o.Type == "CONTEXT" && o.Action == "FAIL");
    }

    [Fact]
    public async Task RunInChildContextAsync_SubTypeAndName_PropagateToCheckpoint()
    {
        var (context, recorder, _, _) = CreateContext();

        await context.RunInChildContextAsync<string>(
            async (_, _) => { await Task.CompletedTask; return "ok"; },
            name: "phase",
            config: new ChildContextConfig { SubType = "WaitForCallback" });

        await recorder.Batcher.DrainAsync();

        var contextOps = recorder.Flushed.Where(o => o.Type == "CONTEXT").ToArray();
        Assert.Equal(2, contextOps.Length);
        foreach (var op in contextOps)
        {
            Assert.Equal("WaitForCallback", op.SubType);
            Assert.Equal("phase", op.Name);
        }
    }

}
