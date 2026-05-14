using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.DurableExecution.Internal;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.Lambda.TestUtilities;
using Xunit;

namespace Amazon.Lambda.DurableExecution.Tests;

public class DurableContextTests
{
    /// <summary>Reproduces the Id that <see cref="OperationIdGenerator"/> emits for the n-th root-level operation.</summary>
    private static string IdAt(int position) => OperationIdGenerator.HashOperationId(position.ToString());

    private static TestLambdaContext CreateLambdaContext() =>
        new() { Serializer = new DefaultLambdaJsonSerializer() };

    private static DurableContext CreateContext(
        InitialExecutionState? initialState = null,
        TerminationManager? terminationManager = null)
    {
        var state = new ExecutionState();
        state.LoadFromCheckpoint(initialState);
        var tm = terminationManager ?? new TerminationManager();
        var idGen = new OperationIdGenerator();
        var lambdaContext = CreateLambdaContext();

        return new DurableContext(state, tm, idGen, "arn:aws:lambda:us-east-1:123:durable-execution:test", lambdaContext);
    }

    #region StepAsync Tests

    [Fact]
    public async Task StepAsync_NewExecution_RunsFunction()
    {
        var context = CreateContext();
        var executed = false;

        var result = await context.StepAsync(async (_) =>
        {
            executed = true;
            await Task.CompletedTask;
            return 42;
        }, name: "my_step");

        Assert.True(executed);
        Assert.Equal(42, result);
    }

    [Fact]
    public async Task StepAsync_Replay_ReturnsCachedResult()
    {
        var context = CreateContext(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = IdAt(1),
                    Type = OperationTypes.Step,
                    Status = OperationStatuses.Succeeded,
                    StepDetails = new StepDetails { Result = "\"cached_value\"" }
                }
            }
        });

        var executed = false;
        var result = await context.StepAsync(async (_) =>
        {
            executed = true;
            await Task.CompletedTask;
            return "fresh_value";
        }, name: "cached_step");

        Assert.False(executed);
        Assert.Equal("cached_value", result);
    }

    [Fact]
    public async Task StepAsync_ReplayFailed_ThrowsStepException()
    {
        var context = CreateContext(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = IdAt(1),
                    Type = OperationTypes.Step,
                    Status = OperationStatuses.Failed,
                    StepDetails = new StepDetails
                    {
                        Error = new ErrorObject
                        {
                            ErrorType = "System.TimeoutException",
                            ErrorMessage = "timed out"
                        }
                    }
                }
            }
        });

        var ex = await Assert.ThrowsAsync<StepException>(() =>
            context.StepAsync(async (_) => { await Task.CompletedTask; return "x"; }, name: "bad_step"));

        Assert.Equal("System.TimeoutException", ex.ErrorType);
        Assert.Equal("timed out", ex.Message);
    }

    [Fact]
    public async Task StepAsync_Throws_FailsWithStepException()
    {
        var context = CreateContext();
        var attempts = 0;

        await Assert.ThrowsAsync<StepException>(() =>
            context.StepAsync<string>(async (_) =>
            {
                attempts++;
                await Task.CompletedTask;
                throw new InvalidOperationException("boom");
            }, name: "fail_step"));

        // No retry support yet — the step runs once.
        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task StepAsync_WithStepContext_ReceivesMetadata()
    {
        var context = CreateContext();
        string? receivedOpId = null;
        int receivedAttempt = 0;
        Microsoft.Extensions.Logging.ILogger? receivedLogger = null;

        await context.StepAsync(async (step) =>
        {
            receivedOpId = step.OperationId;
            receivedAttempt = step.AttemptNumber;
            receivedLogger = step.Logger;
            await Task.CompletedTask;
            return "done";
        }, name: "meta_step");

        Assert.Equal(IdAt(1), receivedOpId);
        Assert.Equal(1, receivedAttempt);
        Assert.NotNull(receivedLogger);
    }

    [Fact]
    public async Task StepAsync_VoidOverload_Works()
    {
        var context = CreateContext();
        var executed = false;

        await context.StepAsync(async (_) =>
        {
            executed = true;
            await Task.CompletedTask;
        }, name: "void_step");

        Assert.True(executed);
    }

    [Fact]
    public async Task StepAsync_MultipleSteps_DeterministicIds()
    {
        var context = CreateContext();

        var r1 = await context.StepAsync(async (_) => { await Task.CompletedTask; return "a"; }, name: "first");
        var r2 = await context.StepAsync(async (_) => { await Task.CompletedTask; return "b"; }, name: "second");
        var r3 = await context.StepAsync(async (_) => { await Task.CompletedTask; return "c"; });

        Assert.Equal("a", r1);
        Assert.Equal("b", r2);
        Assert.Equal("c", r3);
    }

    [Fact]
    public async Task StepAsync_ComplexType_SerializesCorrectly()
    {
        var context = CreateContext(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = IdAt(1),
                    Type = OperationTypes.Step,
                    Status = OperationStatuses.Succeeded,
                    StepDetails = new StepDetails { Result = "{\"Name\":\"Alice\",\"Age\":30}" }
                }
            }
        });

        var result = await context.StepAsync(
            async (_) => { await Task.CompletedTask; return new TestPerson { Name = "Bob", Age = 25 }; },
            name: "fetch");

        Assert.Equal("Alice", result.Name);
        Assert.Equal(30, result.Age);
    }

    [Fact]
    public async Task StepAsync_NoSerializerOnContext_ThrowsInvalidOperation()
    {
        // The serializer comes from ILambdaContext.Serializer — without one,
        // we can't checkpoint anything. The error message points users at the
        // bootstrap registration point.
        var state = new ExecutionState();
        var tm = new TerminationManager();
        var idGen = new OperationIdGenerator();
        var lambdaContext = new TestLambdaContext(); // no Serializer set
        var context = new DurableContext(state, tm, idGen, "arn:test", lambdaContext);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            context.StepAsync(async (_) => { await Task.CompletedTask; return "x"; }, name: "no_serializer"));

        Assert.Contains("ILambdaSerializer", ex.Message);
    }

    [Fact]
    public void Logger_Defaults_ToNullLogger()
    {
        var context = CreateContext();
        Assert.NotNull(context.Logger);
    }

    [Fact]
    public void ExecutionContext_ExposesArn()
    {
        var context = CreateContext();
        Assert.Equal("arn:aws:lambda:us-east-1:123:durable-execution:test", context.ExecutionContext.DurableExecutionArn);
    }

    [Fact]
    public void LambdaContext_IsExposed()
    {
        var context = CreateContext();
        Assert.NotNull(context.LambdaContext);
    }

    [Fact]
    public async Task StepAsync_Replay_NullResult_ReturnsDefault()
    {
        var context = CreateContext(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = IdAt(1),
                    Type = OperationTypes.Step,
                    Status = OperationStatuses.Succeeded,
                    StepDetails = new StepDetails { Result = null }
                }
            }
        });

        var result = await context.StepAsync<string?>(
            async (_) => { await Task.CompletedTask; return "fresh"; },
            name: "no_result");

        Assert.Null(result);
    }

    [Fact]
    public async Task StepAsync_CancelledToken_ThrowsOperationCanceled()
    {
        var context = CreateContext();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            context.StepAsync(
                async (_) =>
                {
                    cts.Token.ThrowIfCancellationRequested();
                    await Task.CompletedTask;
                    return "unreachable";
                },
                name: "cancelled_step",
                cancellationToken: cts.Token));
    }

    #endregion

    #region WaitAsync Tests

    [Fact]
    public async Task WaitAsync_SubSecond_ThrowsArgumentOutOfRange()
    {
        var context = CreateContext();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            context.WaitAsync(TimeSpan.FromMilliseconds(500)));
    }

    [Fact]
    public async Task WaitAsync_AboveOneYear_ThrowsArgumentOutOfRange()
    {
        var context = CreateContext();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            context.WaitAsync(TimeSpan.FromSeconds(31_622_401)));
    }

    [Fact]
    public async Task WaitAsync_NewExecution_SignalsTermination()
    {
        var tm = new TerminationManager();
        var context = CreateContext(terminationManager: tm);

        // WaitAsync should signal termination and return a never-completing task
        var waitTask = context.WaitAsync(TimeSpan.FromSeconds(30), name: "my_wait");

        // Give it a moment to execute
        await Task.Delay(10);

        Assert.True(tm.IsTerminated);
        Assert.False(waitTask.IsCompleted);
    }

    [Fact]
    public async Task WaitAsync_Elapsed_ContinuesImmediately()
    {
        var pastExpirationMs = DateTimeOffset.UtcNow.AddSeconds(-10).ToUnixTimeMilliseconds();
        var context = CreateContext(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = IdAt(1),
                    Type = OperationTypes.Wait,
                    Status = OperationStatuses.Pending,
                    WaitDetails = new WaitDetails { ScheduledEndTimestamp = pastExpirationMs }
                }
            }
        });

        await context.WaitAsync(TimeSpan.FromSeconds(30), name: "cooldown");
        // If we got here, the wait was correctly skipped
    }

    [Fact]
    public async Task WaitAsync_StartedButNotExpired_ResuspendsWithoutNewCheckpoint()
    {
        var futureExpirationMs = DateTimeOffset.UtcNow.AddSeconds(300).ToUnixTimeMilliseconds();
        var tm = new TerminationManager();
        var state = new ExecutionState();
        state.LoadFromCheckpoint(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = IdAt(1),
                    Type = OperationTypes.Wait,
                    Status = OperationStatuses.Pending,
                    WaitDetails = new WaitDetails { ScheduledEndTimestamp = futureExpirationMs }
                }
            }
        });
        var idGen = new OperationIdGenerator();
        var lambdaContext = CreateLambdaContext();
        var recorder = new RecordingBatcher();
        var context = new DurableContext(state, tm, idGen, "arn:test", lambdaContext, recorder.Batcher);

        var waitTask = context.WaitAsync(TimeSpan.FromSeconds(30), name: "pending_wait");

        await Task.Delay(10);

        Assert.True(tm.IsTerminated);
        Assert.False(waitTask.IsCompleted);
        Assert.Empty(recorder.Flushed);
    }

    [Fact]
    public async Task WaitAsync_AlreadySucceeded_ContinuesImmediately()
    {
        var context = CreateContext(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = IdAt(1),
                    Type = OperationTypes.Wait,
                    Status = OperationStatuses.Succeeded
                }
            }
        });

        await context.WaitAsync(TimeSpan.FromSeconds(30), name: "done_wait");
        // Completed without blocking
    }

    [Fact]
    public async Task WaitAsync_UnknownStatus_ThrowsNonDeterministicException()
    {
        // Unrecognized status on a replayed wait checkpoint must surface as
        // NonDeterministicExecutionException — silently re-emitting WAIT START
        // would either fail at the service or duplicate work.
        var context = CreateContext(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = IdAt(1),
                    Type = OperationTypes.Wait,
                    Status = "TOTALLY_BOGUS_STATUS"
                }
            }
        });

        await Assert.ThrowsAsync<NonDeterministicExecutionException>(() =>
            context.WaitAsync(TimeSpan.FromSeconds(30), name: "mystery_wait"));
    }

    #endregion

    #region End-to-end: Step + Wait + Step

    [Fact]
    public async Task EndToEnd_StepWaitStep_FirstInvocation_SuspendsOnWait()
    {
        var tm = new TerminationManager();
        var state = new ExecutionState();
        state.LoadFromCheckpoint(null);
        var idGen = new OperationIdGenerator();
        var lambdaContext = CreateLambdaContext();
        var context = new DurableContext(state, tm, idGen, "arn:test", lambdaContext);

        var result = await DurableExecutionHandler.RunAsync<string>(
            state, tm,
            async () =>
            {
                await context.StepAsync(async (_) => { await Task.CompletedTask; return "fetched"; }, name: "fetch");
                await context.WaitAsync(TimeSpan.FromSeconds(30), name: "delay");
                var final = await context.StepAsync(async (_) => { await Task.CompletedTask; return "processed"; }, name: "process");
                return final;
            });

        Assert.Equal(InvocationStatus.Pending, result.Status);
    }

    [Fact]
    public async Task EndToEnd_StepWaitStep_SecondInvocation_Completes()
    {
        var pastExpirationMs = DateTimeOffset.UtcNow.AddSeconds(-5).ToUnixTimeMilliseconds();
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
                    StepDetails = new StepDetails { Result = "\"fetched\"" }
                },
                new()
                {
                    Id = IdAt(2),
                    Type = OperationTypes.Wait,
                    Status = OperationStatuses.Pending,
                    WaitDetails = new WaitDetails { ScheduledEndTimestamp = pastExpirationMs }
                }
            }
        });

        var idGen = new OperationIdGenerator();
        var lambdaContext = CreateLambdaContext();
        var context = new DurableContext(state, tm, idGen, "arn:test", lambdaContext);
        var processExecuted = false;

        var result = await DurableExecutionHandler.RunAsync<string>(
            state, tm,
            async () =>
            {
                var fetched = await context.StepAsync(async (_) => { await Task.CompletedTask; return "fresh_fetch"; }, name: "fetch");
                Assert.Equal("fetched", fetched); // cached from replay

                await context.WaitAsync(TimeSpan.FromSeconds(30), name: "delay");
                // wait is elapsed, continues

                var final = await context.StepAsync(async (_) =>
                {
                    processExecuted = true;
                    await Task.CompletedTask;
                    return "processed";
                }, name: "process");
                return final;
            });

        Assert.Equal(InvocationStatus.Succeeded, result.Status);
        Assert.Equal("processed", result.Result);
        Assert.True(processExecuted);
    }

    #endregion

    #region Non-Determinism Detection Tests

    [Fact]
    public async Task StepAsync_ReplayTypeMismatch_ThrowsNonDeterministicException()
    {
        var state = new ExecutionState();
        state.LoadFromCheckpoint(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = IdAt(1),
                    Type = OperationTypes.Wait,
                    Status = OperationStatuses.Succeeded
                }
            }
        });
        var tm = new TerminationManager();
        var idGen = new OperationIdGenerator();
        var lambdaContext = CreateLambdaContext();
        var context = new DurableContext(state, tm, idGen, "arn:test", lambdaContext);

        var ex = await Assert.ThrowsAsync<NonDeterministicExecutionException>(async () =>
            await context.StepAsync<string>(
                async (_) => { await Task.CompletedTask; return "should not run"; },
                name: "my_op"));

        Assert.Contains("expected type 'STEP'", ex.Message);
        Assert.Contains("found 'WAIT'", ex.Message);
    }

    [Fact]
    public async Task WaitAsync_ReplayTypeMismatch_ThrowsNonDeterministicException()
    {
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
                    StepDetails = new StepDetails { Result = "\"hello\"" }
                }
            }
        });
        var tm = new TerminationManager();
        var idGen = new OperationIdGenerator();
        var lambdaContext = CreateLambdaContext();
        var context = new DurableContext(state, tm, idGen, "arn:test", lambdaContext);

        var ex = await Assert.ThrowsAsync<NonDeterministicExecutionException>(async () =>
            await context.WaitAsync(TimeSpan.FromSeconds(10), name: "my_op"));

        Assert.Contains("expected type 'WAIT'", ex.Message);
        Assert.Contains("found 'STEP'", ex.Message);
    }

    [Fact]
    public async Task StepAsync_ReplayNameMismatch_ThrowsNonDeterministicException()
    {
        // Simulate a scenario where the operation was stored with a different name
        // than what the current code passes (e.g., service returned stale data).
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
                    Name = "old_name",
                    StepDetails = new StepDetails { Result = "\"old_result\"" }
                }
            }
        });
        var tm = new TerminationManager();
        var idGen = new OperationIdGenerator();
        var lambdaContext = CreateLambdaContext();
        var context = new DurableContext(state, tm, idGen, "arn:test", lambdaContext);

        var ex = await Assert.ThrowsAsync<NonDeterministicExecutionException>(async () =>
            await context.StepAsync<string>(
                async (_) => { await Task.CompletedTask; return "new"; },
                name: "my_step"));

        Assert.Contains("expected name 'my_step'", ex.Message);
        Assert.Contains("found 'old_name'", ex.Message);
    }

    [Fact]
    public async Task StepAsync_NoReplay_SkipsValidation()
    {
        var context = CreateContext();

        var result = await context.StepAsync<string>(
            async (_) => { await Task.CompletedTask; return "ok"; },
            name: "anything");

        Assert.Equal("ok", result);
    }

    #endregion

    private class TestPerson
    {
        public string? Name { get; set; }
        public int Age { get; set; }
    }
}
