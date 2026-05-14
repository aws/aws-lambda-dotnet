using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.DurableExecution.Internal;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.Lambda.TestUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using ILambdaSerializer = Amazon.Lambda.Core.ILambdaSerializer;

namespace Amazon.Lambda.DurableExecution.Tests;

public class WaitForConditionOperationTests
{
    /// <summary>Reproduces the Id that <see cref="OperationIdGenerator"/> emits for the n-th root-level operation.</summary>
    private static string IdAt(int position) => OperationIdGenerator.HashOperationId(position.ToString());

    private static TestLambdaContext CreateLambdaContext(ILambdaSerializer? serializer = null) =>
#pragma warning disable AWSLAMBDA001 // TestLambdaContext.Serializer is experimental.
        new() { Serializer = serializer ?? new DefaultLambdaJsonSerializer() };
#pragma warning restore AWSLAMBDA001

    private static (DurableContext context, RecordingBatcher recorder, TerminationManager tm, ExecutionState state)
        CreateContext(InitialExecutionState? initialState = null, ILambdaSerializer? serializer = null)
    {
        var state = new ExecutionState();
        state.LoadFromCheckpoint(initialState);
        var tm = new TerminationManager();
        var idGen = new OperationIdGenerator();
        var lambdaContext = CreateLambdaContext(serializer);
        var recorder = new RecordingBatcher();
        var context = new DurableContext(state, tm, idGen, "arn:test", lambdaContext, recorder.Batcher);
        return (context, recorder, tm, state);
    }

    // ── Fresh execution ─────────────────────────────────────────────────

    [Fact]
    public async Task FreshExecution_StrategyStopsImmediately_SucceedsWithFinalState()
    {
        var (context, recorder, tm, _) = CreateContext();

        // The check function "advances" the state to 42; the strategy's
        // isDone predicate matches immediately. This exercises the synchronous
        // success path with no polling iterations.
        int checkInvocations = 0;
        var result = await context.WaitForConditionAsync<int>(
            check: async (state, ctx) =>
            {
                checkInvocations++;
                Assert.Equal(checkInvocations, ctx.AttemptNumber);
                await Task.CompletedTask;
                return 42;
            },
            config: new WaitForConditionConfig<int>
            {
                InitialState = 0,
                WaitStrategy = WaitStrategy.Exponential<int>(isDone: s => s == 42)
            },
            name: "poll");

        Assert.Equal(42, result);
        Assert.Equal(1, checkInvocations);
        Assert.False(tm.IsTerminated);

        await recorder.Batcher.DrainAsync();

        var actions = recorder.Flushed.Select(o => $"{o.Type}:{o.Action}").ToArray();
        Assert.Equal(new[] { "STEP:START", "STEP:SUCCEED" }, actions);

        var succeed = recorder.Flushed.Single(o => o.Action == "SUCCEED");
        Assert.Equal(IdAt(1), succeed.Id);
        Assert.Equal("WaitForCondition", succeed.SubType);
        Assert.Equal("poll", succeed.Name);
        Assert.Equal("42", succeed.Payload);
    }

    [Fact]
    public async Task FreshExecution_StrategyContinues_EmitsRetryAndSuspends()
    {
        var (context, recorder, tm, _) = CreateContext();

        // Strategy says continue → operation must emit RETRY and suspend.
        var task = context.WaitForConditionAsync<int>(
            check: async (state, _) => { await Task.CompletedTask; return state + 1; },
            config: new WaitForConditionConfig<int>
            {
                InitialState = 0,
                WaitStrategy = WaitStrategy.Fixed<int>(TimeSpan.FromSeconds(3), maxAttempts: 10)
            },
            name: "poll");

        await Task.Delay(50);

        Assert.True(tm.IsTerminated);
        Assert.False(task.IsCompleted);

        await recorder.Batcher.DrainAsync();

        var actions = recorder.Flushed.Select(o => $"{o.Type}:{o.Action}").ToArray();
        Assert.Equal(new[] { "STEP:START", "STEP:RETRY" }, actions);

        var retry = recorder.Flushed.Single(o => o.Action == "RETRY");
        Assert.Equal("WaitForCondition", retry.SubType);
        Assert.Equal("1", retry.Payload);  // state advanced to 1
        Assert.NotNull(retry.StepOptions);
        Assert.Equal(3, retry.StepOptions.NextAttemptDelaySeconds);
    }

    [Fact]
    public async Task FreshExecution_UsesInitialStateOnFirstCall()
    {
        var (context, _, _, _) = CreateContext();

        int? observedInitial = null;
        await context.WaitForConditionAsync<int>(
            check: async (state, _) =>
            {
                observedInitial ??= state;
                await Task.CompletedTask;
                return state;
            },
            config: new WaitForConditionConfig<int>
            {
                InitialState = 99,
                WaitStrategy = WaitStrategy.Fixed<int>(TimeSpan.FromSeconds(1), maxAttempts: 10, isDone: _ => true)
            },
            name: "poll");

        Assert.Equal(99, observedInitial);
    }

    [Fact]
    public async Task FreshExecution_AttemptNumberIs1OnFirstCall()
    {
        var (context, _, _, _) = CreateContext();

        int observed = -1;
        await context.WaitForConditionAsync<int>(
            check: async (state, ctx) =>
            {
                observed = ctx.AttemptNumber;
                await Task.CompletedTask;
                return state;
            },
            config: new WaitForConditionConfig<int>
            {
                InitialState = 0,
                WaitStrategy = WaitStrategy.Fixed<int>(TimeSpan.FromSeconds(1), maxAttempts: 5, isDone: _ => true)
            });

        Assert.Equal(1, observed);
    }

    [Fact]
    public async Task CheckContext_ExposesLogger()
    {
        // The check function receives an IConditionCheckContext whose Logger
        // is a real ILogger forwarded from the durable runtime — user code
        // can use it to emit observability without threading a logger in.
        var (context, _, _, _) = CreateContext();

        ILogger? observedLogger = null;
        await context.WaitForConditionAsync<int>(
            check: async (state, ctx) =>
            {
                observedLogger = ctx.Logger;
                await Task.CompletedTask;
                return state;
            },
            config: new WaitForConditionConfig<int>
            {
                InitialState = 0,
                WaitStrategy = WaitStrategy.Fixed<int>(TimeSpan.FromSeconds(1), maxAttempts: 5, isDone: _ => true)
            });

        Assert.NotNull(observedLogger);
    }

    // ── Replay paths ────────────────────────────────────────────────────

    [Fact]
    public async Task Replay_Succeeded_ReturnsCachedAndSkipsCheck()
    {
        var (context, recorder, _, _) = CreateContext(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = IdAt(1),
                    Type = OperationTypes.Step,
                    SubType = OperationSubTypes.WaitForCondition,
                    Status = OperationStatuses.Succeeded,
                    Name = "poll",
                    StepDetails = new StepDetails { Result = "7" }
                }
            }
        });

        var checkInvoked = false;
        var result = await context.WaitForConditionAsync<int>(
            check: async (_, _) => { checkInvoked = true; await Task.CompletedTask; return 0; },
            config: new WaitForConditionConfig<int>
            {
                InitialState = 0,
                WaitStrategy = WaitStrategy.Fixed<int>(TimeSpan.FromSeconds(1))
            },
            name: "poll");

        Assert.False(checkInvoked);
        Assert.Equal(7, result);

        await recorder.Batcher.DrainAsync();
        Assert.Empty(recorder.Flushed);
    }

    [Fact]
    public async Task Replay_PendingTimerNotFired_ReSuspends()
    {
        // NextAttemptTimestamp 1 hour in the future → timer hasn't fired,
        // operation must re-suspend without re-checkpointing or re-running.
        var futureMs = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeMilliseconds();

        var (context, recorder, tm, _) = CreateContext(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = IdAt(1),
                    Type = OperationTypes.Step,
                    SubType = OperationSubTypes.WaitForCondition,
                    Status = OperationStatuses.Pending,
                    Name = "poll",
                    StepDetails = new StepDetails
                    {
                        Result = "5",
                        Attempt = 2,
                        NextAttemptTimestamp = futureMs
                    }
                }
            }
        });

        var checkInvoked = false;
        var task = context.WaitForConditionAsync<int>(
            check: async (_, _) => { checkInvoked = true; await Task.CompletedTask; return 0; },
            config: new WaitForConditionConfig<int>
            {
                InitialState = 0,
                WaitStrategy = WaitStrategy.Fixed<int>(TimeSpan.FromSeconds(1))
            },
            name: "poll");

        await Task.Delay(50);

        Assert.False(checkInvoked);
        Assert.True(tm.IsTerminated);
        Assert.False(task.IsCompleted);

        await recorder.Batcher.DrainAsync();
        Assert.Empty(recorder.Flushed);
    }

    [Fact]
    public async Task Replay_PendingTimerFired_ResumesWithCheckpointedState()
    {
        // NextAttemptTimestamp 1 hour in the past → timer fired (service
        // hasn't yet stamped READY but the deadline is met). Continue.
        var pastMs = DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeMilliseconds();

        var (context, recorder, _, _) = CreateContext(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = IdAt(1),
                    Type = OperationTypes.Step,
                    SubType = OperationSubTypes.WaitForCondition,
                    Status = OperationStatuses.Pending,
                    Name = "poll",
                    StepDetails = new StepDetails
                    {
                        Result = "5",
                        Attempt = 2,
                        NextAttemptTimestamp = pastMs
                    }
                }
            }
        });

        int? observedState = null;
        int? observedAttempt = null;
        var result = await context.WaitForConditionAsync<int>(
            check: async (state, ctx) =>
            {
                observedState = state;
                observedAttempt = ctx.AttemptNumber;
                await Task.CompletedTask;
                return state;  // condition met (isDone returns true)
            },
            config: new WaitForConditionConfig<int>
            {
                InitialState = 0,
                WaitStrategy = WaitStrategy.Fixed<int>(TimeSpan.FromSeconds(1), isDone: _ => true)
            },
            name: "poll");

        // Critical: state survives across iterations. Check receives the
        // PRIOR state (5, from the prior RETRY's payload), not InitialState (0).
        Assert.Equal(5, observedState);
        Assert.Equal(3, observedAttempt);  // prior attempt was 2, this is attempt 3
        Assert.Equal(5, result);

        await recorder.Batcher.DrainAsync();

        // No new START — original is authoritative.
        Assert.DoesNotContain(recorder.Flushed, o => o.Action == "START");
        Assert.Contains(recorder.Flushed, o => o.Action == "SUCCEED");
    }

    [Fact]
    public async Task Replay_Ready_ResumesWithCheckpointedState()
    {
        var (context, recorder, _, _) = CreateContext(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = IdAt(1),
                    Type = OperationTypes.Step,
                    SubType = OperationSubTypes.WaitForCondition,
                    Status = OperationStatuses.Ready,
                    Name = "poll",
                    StepDetails = new StepDetails
                    {
                        Result = "11",
                        Attempt = 3
                    }
                }
            }
        });

        int? observedState = null;
        int? observedAttempt = null;
        var result = await context.WaitForConditionAsync<int>(
            check: async (state, ctx) =>
            {
                observedState = state;
                observedAttempt = ctx.AttemptNumber;
                await Task.CompletedTask;
                return state * 2;
            },
            config: new WaitForConditionConfig<int>
            {
                InitialState = 0,
                WaitStrategy = WaitStrategy.Fixed<int>(TimeSpan.FromSeconds(1), isDone: _ => true)
            },
            name: "poll");

        Assert.Equal(11, observedState);
        Assert.Equal(4, observedAttempt);  // prior=3 → next=4
        Assert.Equal(22, result);

        await recorder.Batcher.DrainAsync();
        Assert.DoesNotContain(recorder.Flushed, o => o.Action == "START");
    }

    [Fact]
    public async Task Replay_Started_ResumesWithInitialState()
    {
        // STARTED with no payload means the very first check attempt was
        // lost (Lambda crash before RETRY/SUCCEED). Re-execute with
        // InitialState since no prior state is available.
        var (context, recorder, _, _) = CreateContext(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = IdAt(1),
                    Type = OperationTypes.Step,
                    SubType = OperationSubTypes.WaitForCondition,
                    Status = OperationStatuses.Started,
                    Name = "poll"
                }
            }
        });

        int? observedState = null;
        int? observedAttempt = null;
        var result = await context.WaitForConditionAsync<int>(
            check: async (state, ctx) =>
            {
                observedState = state;
                observedAttempt = ctx.AttemptNumber;
                await Task.CompletedTask;
                return state + 100;
            },
            config: new WaitForConditionConfig<int>
            {
                InitialState = 50,
                WaitStrategy = WaitStrategy.Fixed<int>(TimeSpan.FromSeconds(1), isDone: _ => true)
            },
            name: "poll");

        Assert.Equal(50, observedState);  // InitialState is the seed
        Assert.Equal(1, observedAttempt);
        Assert.Equal(150, result);

        await recorder.Batcher.DrainAsync();
        // Do NOT re-emit START on STARTED replay.
        Assert.DoesNotContain(recorder.Flushed, o => o.Action == "START");
    }

    [Fact]
    public async Task Replay_Failed_FromCheckException_ThrowsStepException()
    {
        var (context, _, _, _) = CreateContext(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = IdAt(1),
                    Type = OperationTypes.Step,
                    SubType = OperationSubTypes.WaitForCondition,
                    Status = OperationStatuses.Failed,
                    Name = "poll",
                    StepDetails = new StepDetails
                    {
                        Error = new ErrorObject
                        {
                            ErrorType = "System.InvalidOperationException",
                            ErrorMessage = "check went wrong",
                            StackTrace = new[] { "at A.B()" }
                        }
                    }
                }
            }
        });

        var ex = await Assert.ThrowsAsync<StepException>(() =>
            context.WaitForConditionAsync<int>(
                check: async (_, _) => { await Task.CompletedTask; return 0; },
                config: new WaitForConditionConfig<int>
                {
                    InitialState = 0,
                    WaitStrategy = WaitStrategy.Fixed<int>(TimeSpan.FromSeconds(1))
                },
                name: "poll"));

        Assert.Equal("check went wrong", ex.Message);
        Assert.Equal("System.InvalidOperationException", ex.ErrorType);
    }

    [Fact]
    public async Task Replay_Failed_FromMaxAttempts_ThrowsWaitForConditionException()
    {
        // The FAIL checkpoint records LastState in Error.ErrorData (the wire
        // protocol disallows a Payload on FAIL updates) so replay can
        // reconstruct an identically-populated exception. Live execution sets
        // the same field in MaxAttemptsExhausted_FreshExecution.
        var (context, _, _, _) = CreateContext(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = IdAt(1),
                    Type = OperationTypes.Step,
                    SubType = OperationSubTypes.WaitForCondition,
                    Status = OperationStatuses.Failed,
                    Name = "poll",
                    StepDetails = new StepDetails
                    {
                        Attempt = 3,
                        Error = new ErrorObject
                        {
                            ErrorType = typeof(WaitForConditionException).FullName,
                            ErrorMessage = "exhausted",
                            ErrorData = "42"
                        }
                    }
                }
            }
        });

        var ex = await Assert.ThrowsAsync<WaitForConditionException>(() =>
            context.WaitForConditionAsync<int>(
                check: async (_, _) => { await Task.CompletedTask; return 0; },
                config: new WaitForConditionConfig<int>
                {
                    InitialState = 0,
                    WaitStrategy = WaitStrategy.Fixed<int>(TimeSpan.FromSeconds(1))
                },
                name: "poll"));

        Assert.Equal(3, ex.AttemptsExhausted);
        Assert.Equal("exhausted", ex.Message);
        Assert.Equal(42, ex.LastState);  // round-tripped from FAIL Error.ErrorData
    }

    [Fact]
    public async Task Replay_Failed_FromMaxAttempts_LastState_MatchesLiveExecution()
    {
        // Live execution path: exhaust max-attempts and capture the
        // exception's LastState. Then construct a FAIL checkpoint mirroring
        // what was written, replay, and assert LastState round-trips.
        var (liveCtx, liveRecorder, _, _) = CreateContext();

        var liveEx = await Assert.ThrowsAsync<WaitForConditionException>(() =>
            liveCtx.WaitForConditionAsync<int>(
                check: async (state, _) => { await Task.CompletedTask; return state + 1; },
                config: new WaitForConditionConfig<int>
                {
                    InitialState = 5,
                    WaitStrategy = WaitStrategy.Fixed<int>(TimeSpan.FromSeconds(1), maxAttempts: 1)
                },
                name: "poll"));

        await liveRecorder.Batcher.DrainAsync();
        var failUpdate = liveRecorder.Flushed.Single(o => o.Action == "FAIL");
        Assert.Null(failUpdate.Payload);  // wire protocol forbids Payload on FAIL
        Assert.Equal("6", failUpdate.Error?.ErrorData);  // last state was 5+1=6, stored in ErrorData

        // Reconstruct the operation as the service would echo it back on
        // replay (Error → StepDetails.Error; LastState lives in Error.ErrorData).
        var (replayCtx, _, _, _) = CreateContext(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = IdAt(1),
                    Type = OperationTypes.Step,
                    SubType = OperationSubTypes.WaitForCondition,
                    Status = OperationStatuses.Failed,
                    Name = "poll",
                    StepDetails = new StepDetails
                    {
                        Attempt = liveEx.AttemptsExhausted,
                        Error = new ErrorObject
                        {
                            ErrorType = failUpdate.Error?.ErrorType,
                            ErrorMessage = failUpdate.Error?.ErrorMessage,
                            ErrorData = failUpdate.Error?.ErrorData
                        }
                    }
                }
            }
        });

        var replayEx = await Assert.ThrowsAsync<WaitForConditionException>(() =>
            replayCtx.WaitForConditionAsync<int>(
                check: async (_, _) => { await Task.CompletedTask; return 0; },
                config: new WaitForConditionConfig<int>
                {
                    InitialState = 0,
                    WaitStrategy = WaitStrategy.Fixed<int>(TimeSpan.FromSeconds(1), maxAttempts: 1)
                },
                name: "poll"));

        Assert.Equal(liveEx.AttemptsExhausted, replayEx.AttemptsExhausted);
        Assert.NotNull(replayEx.LastState);
        Assert.Equal(liveEx.LastState, replayEx.LastState);
    }

    [Fact]
    public async Task Replay_Failed_FromMaxAttempts_NullPayload_LeavesLastStateNull()
    {
        // Backwards-compat: a FAIL checkpoint produced before LastState
        // was stored in ErrorData (or one that lost its ErrorData) should
        // not blow up — LastState falls back to null.
        var (context, _, _, _) = CreateContext(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = IdAt(1),
                    Type = OperationTypes.Step,
                    SubType = OperationSubTypes.WaitForCondition,
                    Status = OperationStatuses.Failed,
                    Name = "poll",
                    StepDetails = new StepDetails
                    {
                        Attempt = 2,
                        Error = new ErrorObject
                        {
                            ErrorType = typeof(WaitForConditionException).FullName,
                            ErrorMessage = "exhausted"
                            // ErrorData intentionally null (legacy FAIL).
                        }
                    }
                }
            }
        });

        var ex = await Assert.ThrowsAsync<WaitForConditionException>(() =>
            context.WaitForConditionAsync<int>(
                check: async (_, _) => { await Task.CompletedTask; return 0; },
                config: new WaitForConditionConfig<int>
                {
                    InitialState = 0,
                    WaitStrategy = WaitStrategy.Fixed<int>(TimeSpan.FromSeconds(1))
                },
                name: "poll"));

        Assert.Equal(2, ex.AttemptsExhausted);
        Assert.Null(ex.LastState);
    }

    // ── Max attempts exhaustion ─────────────────────────────────────────

    [Fact]
    public async Task MaxAttemptsExhausted_FreshExecution_ThrowsWaitForConditionException()
    {
        var (context, recorder, _, _) = CreateContext();

        // maxAttempts=1 + isDone always false → strategy stops on attempt 1
        // but it's because the counter is saturated, NOT because the
        // condition was met. Operation must throw, not SUCCEED.
        var ex = await Assert.ThrowsAsync<WaitForConditionException>(() =>
            context.WaitForConditionAsync<int>(
                check: async (state, _) => { await Task.CompletedTask; return state + 1; },
                config: new WaitForConditionConfig<int>
                {
                    InitialState = 5,
                    WaitStrategy = WaitStrategy.Fixed<int>(TimeSpan.FromSeconds(1), maxAttempts: 1)
                },
                name: "poll"));

        Assert.Equal(1, ex.AttemptsExhausted);
        Assert.Equal(6, ex.LastState);  // last state observed was 5+1

        await recorder.Batcher.DrainAsync();
        var actions = recorder.Flushed.Select(o => $"{o.Type}:{o.Action}").ToArray();
        Assert.Equal(new[] { "STEP:START", "STEP:FAIL" }, actions);

        var fail = recorder.Flushed.Single(o => o.Action == "FAIL");
        Assert.Equal("WaitForCondition", fail.SubType);
        Assert.NotNull(fail.Error);
        Assert.Equal(typeof(WaitForConditionException).FullName, fail.Error.ErrorType);
        // LastState round-trips through Error.ErrorData (wire protocol forbids
        // a Payload on FAIL). See Replay_Failed_FromMaxAttempts_LastState_MatchesLiveExecution.
        Assert.Null(fail.Payload);
        Assert.Equal("6", fail.Error.ErrorData);
    }

    [Fact]
    public async Task MaxAttemptsExhausted_DistinguishesFromConditionMet()
    {
        var (context, _, _, _) = CreateContext();

        // The same maxAttempts=1 strategy WITH an isDone that's satisfied
        // should SUCCEED, not throw.
        var result = await context.WaitForConditionAsync<int>(
            check: async (_, _) => { await Task.CompletedTask; return 99; },
            config: new WaitForConditionConfig<int>
            {
                InitialState = 0,
                WaitStrategy = WaitStrategy.Fixed<int>(
                    TimeSpan.FromSeconds(1),
                    maxAttempts: 1,
                    isDone: state => state == 99)
            },
            name: "poll");

        Assert.Equal(99, result);
    }

    // ── Check function exception ────────────────────────────────────────

    [Fact]
    public async Task CheckThrows_CheckpointsFailAndThrows()
    {
        var (context, recorder, _, _) = CreateContext();

        var ex = await Assert.ThrowsAsync<StepException>(() =>
            context.WaitForConditionAsync<int>(
                check: async (_, _) => { await Task.CompletedTask; throw new InvalidOperationException("boom"); },
                config: new WaitForConditionConfig<int>
                {
                    InitialState = 0,
                    WaitStrategy = WaitStrategy.Fixed<int>(TimeSpan.FromSeconds(1))
                },
                name: "poll"));

        Assert.Equal("boom", ex.Message);
        Assert.Equal("System.InvalidOperationException", ex.ErrorType);

        await recorder.Batcher.DrainAsync();
        var actions = recorder.Flushed.Select(o => $"{o.Type}:{o.Action}").ToArray();
        Assert.Equal(new[] { "STEP:START", "STEP:FAIL" }, actions);

        var fail = recorder.Flushed.Single(o => o.Action == "FAIL");
        Assert.Equal("WaitForCondition", fail.SubType);
        Assert.Equal("System.InvalidOperationException", fail.Error?.ErrorType);
    }

    // ── Replay determinism: state survives iterations ───────────────────

    [Fact]
    public async Task ReplayDeterminism_StateIsCarriedAcrossIterations()
    {
        // Simulate a multi-iteration history: invocation N had advanced the
        // state to {Count=3}; invocation N+1 should pick that up and
        // continue from there.
        var pastMs = DateTimeOffset.UtcNow.AddSeconds(-1).ToUnixTimeMilliseconds();

        var (context, recorder, _, _) = CreateContext(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = IdAt(1),
                    Type = OperationTypes.Step,
                    SubType = OperationSubTypes.WaitForCondition,
                    Status = OperationStatuses.Ready,
                    Name = "counter",
                    StepDetails = new StepDetails
                    {
                        Result = """{"Count":3}""",
                        Attempt = 3,
                        NextAttemptTimestamp = pastMs
                    }
                }
            }
        });

        CounterState? observed = null;
        int? observedAttempt = null;
        var result = await context.WaitForConditionAsync<CounterState>(
            check: async (state, ctx) =>
            {
                observed = state;
                observedAttempt = ctx.AttemptNumber;
                await Task.CompletedTask;
                return new CounterState { Count = state.Count + 1 };
            },
            config: new WaitForConditionConfig<CounterState>
            {
                InitialState = new CounterState { Count = 0 },  // ignored on replay
                WaitStrategy = WaitStrategy.Fixed<CounterState>(
                    TimeSpan.FromSeconds(1),
                    maxAttempts: 100,
                    isDone: c => c.Count >= 4)  // stop when we hit 4
            },
            name: "counter");

        // Started from the checkpointed counter=3 (NOT InitialState=0),
        // incremented to 4, isDone returned true, returned 4.
        Assert.Equal(3, observed?.Count);
        Assert.Equal(4, observedAttempt);
        Assert.Equal(4, result.Count);

        await recorder.Batcher.DrainAsync();
        var succeed = recorder.Flushed.Single(o => o.Action == "SUCCEED");
        Assert.Equal("""{"Count":4}""", succeed.Payload);
    }

    [Fact]
    public async Task ReplayDeterminism_RoundTripsThroughLambdaSerializer()
    {
        var serializer = new RecordingPersonSerializer();
        var (context, _, _, _) = CreateContext(
            new InitialExecutionState
            {
                Operations = new List<Operation>
                {
                    new()
                    {
                        Id = IdAt(1),
                        Type = OperationTypes.Step,
                        SubType = OperationSubTypes.WaitForCondition,
                        Status = OperationStatuses.Succeeded,
                        Name = "poll",
                        StepDetails = new StepDetails { Result = "<custom>Marie,30</custom>" }
                    }
                }
            },
            serializer: serializer);

        var result = await context.WaitForConditionAsync<TestPerson>(
            check: async (_, _) => { await Task.CompletedTask; return new TestPerson { Name = "ignored", Age = 0 }; },
            config: new WaitForConditionConfig<TestPerson>
            {
                InitialState = new TestPerson { Name = "init", Age = 0 },
                WaitStrategy = WaitStrategy.Fixed<TestPerson>(TimeSpan.FromSeconds(1))
            },
            name: "poll");

        Assert.True(serializer.DeserializeCalled);
        Assert.Equal("Marie", result.Name);
        Assert.Equal(30, result.Age);
    }

    // ── Sync-flush of START before suspending ───────────────────────────

    [Fact]
    public async Task FreshExecution_FlushesStartBeforeSuspending()
    {
        // The START checkpoint MUST be persisted before the workflow
        // suspends — otherwise the service has no record of the polling op
        // and replay can't find it.
        var (context, recorder, tm, _) = CreateContext();

        var task = context.WaitForConditionAsync<int>(
            check: async (state, _) => { await Task.CompletedTask; return state + 1; },
            config: new WaitForConditionConfig<int>
            {
                InitialState = 0,
                WaitStrategy = WaitStrategy.Fixed<int>(TimeSpan.FromSeconds(5), maxAttempts: 10)
            },
            name: "poll");

        await Task.Delay(50);

        Assert.True(tm.IsTerminated);
        Assert.False(task.IsCompleted);

        // At the moment of suspension, both START and RETRY must already be
        // flushed (sync-enqueued ahead of SuspendAndAwait). No drain needed.
        var actions = recorder.Flushed.Select(o => $"{o.Type}:{o.Action}").ToArray();
        Assert.Contains("STEP:START", actions);
        Assert.Contains("STEP:RETRY", actions);
    }

    // ── Replay non-determinism guards ───────────────────────────────────

    [Fact]
    public async Task ReplayUnknownStatus_ThrowsNonDeterministicException()
    {
        var (context, _, _, _) = CreateContext(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = IdAt(1),
                    Type = OperationTypes.Step,
                    SubType = OperationSubTypes.WaitForCondition,
                    Status = "BOGUS",
                    Name = "poll"
                }
            }
        });

        await Assert.ThrowsAsync<NonDeterministicExecutionException>(() =>
            context.WaitForConditionAsync<int>(
                check: async (_, _) => { await Task.CompletedTask; return 0; },
                config: new WaitForConditionConfig<int>
                {
                    InitialState = 0,
                    WaitStrategy = WaitStrategy.Fixed<int>(TimeSpan.FromSeconds(1))
                },
                name: "poll"));
    }

    [Fact]
    public async Task ReplayTypeMismatch_ThrowsNonDeterministicException()
    {
        // Same Id but a different Type — operation order changed between
        // deployments. The base class's ValidateReplayConsistency catches it.
        var (context, _, _, _) = CreateContext(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = IdAt(1),
                    Type = OperationTypes.Wait,
                    Status = OperationStatuses.Succeeded,
                    Name = "poll"
                }
            }
        });

        var ex = await Assert.ThrowsAsync<NonDeterministicExecutionException>(() =>
            context.WaitForConditionAsync<int>(
                check: async (_, _) => { await Task.CompletedTask; return 0; },
                config: new WaitForConditionConfig<int>
                {
                    InitialState = 0,
                    WaitStrategy = WaitStrategy.Fixed<int>(TimeSpan.FromSeconds(1))
                },
                name: "poll"));

        Assert.Contains("expected type 'STEP'", ex.Message);
    }

    // ── Argument validation ─────────────────────────────────────────────

    [Fact]
    public async Task NullCheck_ThrowsArgumentNullException()
    {
        var (context, _, _, _) = CreateContext();
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            context.WaitForConditionAsync<int>(
                check: null!,
                config: new WaitForConditionConfig<int>
                {
                    InitialState = 0,
                    WaitStrategy = WaitStrategy.Fixed<int>(TimeSpan.FromSeconds(1))
                }));
    }

    [Fact]
    public async Task NullConfig_ThrowsArgumentNullException()
    {
        var (context, _, _, _) = CreateContext();
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            context.WaitForConditionAsync<int>(
                check: async (_, _) => { await Task.CompletedTask; return 0; },
                config: null!));
    }

    // ── Observability: warning on payload deserialization failure ──────

    [Fact]
    public async Task DeserializeStateOrInitial_CorruptPayload_LogsWarningAndFallsBack()
    {
        // A READY checkpoint with a payload the serializer cannot read should
        // NOT fail the workflow (Python parity); it should fall back to
        // InitialState. The recovery should be logged at Warning level so
        // corruption / schema-migrations are observable.
        var state = new ExecutionState();
        state.LoadFromCheckpoint(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = IdAt(1),
                    Type = OperationTypes.Step,
                    SubType = OperationSubTypes.WaitForCondition,
                    Status = OperationStatuses.Ready,
                    Name = "poll",
                    StepDetails = new StepDetails { Result = "this-is-not-valid", Attempt = 2 }
                }
            }
        });

        var recorder = new RecordingBatcher();
        var logger = new RecordingLogger();

        var op = new WaitForConditionOperation<int>(
            operationId: IdAt(1),
            name: "poll",
            parentId: null,
            check: async (s, _) => { await Task.CompletedTask; return s; },
            config: new WaitForConditionConfig<int>
            {
                InitialState = 999,
                WaitStrategy = WaitStrategy.Fixed<int>(TimeSpan.FromSeconds(1), isDone: _ => true)
            },
            serializer: new ThrowingLambdaSerializer(),
            logger: logger,
            state: state,
            termination: new TerminationManager(),
            durableExecutionArn: "arn:test",
            batcher: recorder.Batcher);

        var result = await op.ExecuteAsync(CancellationToken.None);

        Assert.Equal(999, result);  // fell back to InitialState
        var warning = Assert.Single(logger.Entries.Where(e => e.Level == LogLevel.Warning));
        Assert.Contains("failed to deserialize prior state", warning.Message);
        Assert.Contains(IdAt(1), warning.Message);
    }

    [Fact]
    public async Task ReplayFailed_CorruptLastStatePayload_LogsWarningAndLastStateNull()
    {
        // FAIL replay's LastState recovery: same observability story — if the
        // FAIL Error.ErrorData can't be deserialized, log a warning and
        // surface LastState=null instead of throwing.
        var state = new ExecutionState();
        state.LoadFromCheckpoint(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = IdAt(1),
                    Type = OperationTypes.Step,
                    SubType = OperationSubTypes.WaitForCondition,
                    Status = OperationStatuses.Failed,
                    Name = "poll",
                    StepDetails = new StepDetails
                    {
                        Attempt = 4,
                        Error = new ErrorObject
                        {
                            ErrorType = typeof(WaitForConditionException).FullName,
                            ErrorMessage = "exhausted",
                            ErrorData = "bogus-payload"
                        }
                    }
                }
            }
        });

        var recorder = new RecordingBatcher();
        var logger = new RecordingLogger();

        var op = new WaitForConditionOperation<int>(
            operationId: IdAt(1),
            name: "poll",
            parentId: null,
            check: async (s, _) => { await Task.CompletedTask; return s; },
            config: new WaitForConditionConfig<int>
            {
                InitialState = 0,
                WaitStrategy = WaitStrategy.Fixed<int>(TimeSpan.FromSeconds(1))
            },
            serializer: new ThrowingLambdaSerializer(),
            logger: logger,
            state: state,
            termination: new TerminationManager(),
            durableExecutionArn: "arn:test",
            batcher: recorder.Batcher);

        var ex = await Assert.ThrowsAsync<WaitForConditionException>(() => op.ExecuteAsync(CancellationToken.None));

        Assert.Equal(4, ex.AttemptsExhausted);
        Assert.Null(ex.LastState);
        var warning = Assert.Single(logger.Entries.Where(e => e.Level == LogLevel.Warning));
        Assert.Contains("failed to deserialize LastState", warning.Message);
    }

    // ── Test helpers ────────────────────────────────────────────────────

    private class CounterState
    {
        public int Count { get; set; }
    }

    private class TestPerson
    {
        public string? Name { get; set; }
        public int Age { get; set; }
    }

    /// <summary>
    /// ILambdaSerializer that round-trips <see cref="TestPerson"/> through a
    /// custom non-JSON wire format so tests can verify the serializer on
    /// ILambdaContext.Serializer is the one used during checkpointing.
    /// </summary>
    private class RecordingPersonSerializer : ILambdaSerializer
    {
        public bool SerializeCalled { get; private set; }
        public bool DeserializeCalled { get; private set; }

        public void Serialize<T>(T response, Stream responseStream)
        {
            SerializeCalled = true;
            var person = (TestPerson)(object)response!;
            using var writer = new StreamWriter(responseStream, leaveOpen: true);
            writer.Write($"<custom>{person.Name},{person.Age}</custom>");
        }

        public T Deserialize<T>(Stream requestStream)
        {
            DeserializeCalled = true;
            using var reader = new StreamReader(requestStream);
            var data = reader.ReadToEnd();
            var inner = data.Replace("<custom>", "").Replace("</custom>", "");
            var parts = inner.Split(',');
            var person = new TestPerson { Name = parts[0], Age = int.Parse(parts[1]) };
            return (T)(object)person;
        }
    }

    /// <summary>Serializer whose Deserialize always throws — exercises the fallback paths.</summary>
    private sealed class ThrowingLambdaSerializer : ILambdaSerializer
    {
        public void Serialize<T>(T response, Stream responseStream)
        {
            using var writer = new StreamWriter(responseStream, leaveOpen: true);
            writer.Write(response?.ToString() ?? string.Empty);
        }

        public T Deserialize<T>(Stream requestStream)
        {
            using var reader = new StreamReader(requestStream);
            var data = reader.ReadToEnd();
            throw new InvalidOperationException($"cannot deserialize '{data}'");
        }
    }

    /// <summary>Captures log calls so tests can assert on level and rendered message.</summary>
    private sealed class RecordingLogger : ILogger
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Entries.Add((logLevel, formatter(state, exception)));
    }
}
