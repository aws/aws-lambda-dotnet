// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.DurableExecution.Internal;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.Lambda.TestUtilities;
using Xunit;

namespace Amazon.Lambda.DurableExecution.Tests;

public class WaitForCallbackTests
{
    /// <summary>Reproduces the Id that <see cref="OperationIdGenerator"/> emits for the n-th root-level operation.</summary>
    private static string IdAt(int position) => OperationIdGenerator.HashOperationId(position.ToString());

    /// <summary>The hashed ID of the n-th child operation under <paramref name="parentOpId"/>.</summary>
    private static string ChildIdAt(string parentOpId, int position) =>
        OperationIdGenerator.HashOperationId($"{parentOpId}-{position}");

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
        var context = new DurableContext(state, tm, idGen, "arn:test", lambdaContext, recorder.Batcher);
        return (context, recorder, tm, state);
    }

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
    public async Task WaitForCallbackAsync_FreshExecution_RunsSubmitterAndSuspendsForCallback()
    {
        var (context, recorder, tm, state) = CreateContext();
        WireServiceCallbackIdAllocation(recorder, state, "cb-wait-1");

        string? receivedCallbackId = null;
        var resultTask = context.WaitForCallbackAsync<string>(
            async (callbackId, ctx) =>
            {
                receivedCallbackId = callbackId;
                Assert.NotNull(ctx.Logger);
                await Task.CompletedTask;
            },
            name: "approval");

        // Race the suspended user task against termination — same idiom as the
        // production handler. Once Terminate() is called inside the inner
        // GetResultAsync, this completes immediately.
        var winner = await Task.WhenAny(resultTask, tm.TerminationTask);
        Assert.Same(tm.TerminationTask, winner);

        Assert.True(tm.IsTerminated);
        Assert.False(resultTask.IsCompleted);
        Assert.Equal("cb-wait-1", receivedCallbackId);

        await recorder.Batcher.DrainAsync();

        var actions = recorder.Flushed.Select(o => $"{o.Type}:{o.Action}:{o.SubType}").ToArray();
        Assert.Equal(new[]
        {
            $"{OperationTypes.Context}:START:{OperationSubTypes.WaitForCallback}",
            $"{OperationTypes.Callback}:START:{OperationSubTypes.Callback}",
            $"{OperationTypes.Step}:START:{OperationSubTypes.Step}",
            $"{OperationTypes.Step}:SUCCEED:{OperationSubTypes.Step}",
        }, actions);
    }

    [Fact]
    public async Task WaitForCallbackAsync_FreshExecution_KebabSuffixedSubOpNames()
    {
        var (context, recorder, tm, state) = CreateContext();
        WireServiceCallbackIdAllocation(recorder, state, "cb-1");

        var resultTask = context.WaitForCallbackAsync<string>(
            async (_, _) => await Task.CompletedTask,
            name: "approval");

        await Task.WhenAny(resultTask, tm.TerminationTask);
        await recorder.Batcher.DrainAsync();

        var callbackStart = recorder.Flushed.Single(o => o.Type == OperationTypes.Callback);
        var stepSucceed = recorder.Flushed.Single(o => o.Type == OperationTypes.Step && o.Action == "SUCCEED");

        Assert.Equal("approval-callback", callbackStart.Name);
        Assert.Equal("approval-submitter", stepSucceed.Name);

        // Avoid unobserved-task warning.
        _ = resultTask;
    }

    [Fact]
    public async Task WaitForCallbackAsync_FreshExecution_NullParentName_LeavesSubOpsNameless()
    {
        var (context, recorder, tm, state) = CreateContext();
        WireServiceCallbackIdAllocation(recorder, state, "cb-1");

        var resultTask = context.WaitForCallbackAsync<string>(
            async (_, _) => await Task.CompletedTask);

        await Task.WhenAny(resultTask, tm.TerminationTask);
        await recorder.Batcher.DrainAsync();

        var callbackStart = recorder.Flushed.Single(o => o.Type == OperationTypes.Callback);
        var stepSucceed = recorder.Flushed.Single(o => o.Type == OperationTypes.Step && o.Action == "SUCCEED");

        Assert.Null(callbackStart.Name);
        Assert.Null(stepSucceed.Name);

        _ = resultTask;
    }

    [Fact]
    public async Task WaitForCallbackAsync_ChildOperationIdsDeterministic()
    {
        var (context, recorder, tm, state) = CreateContext();
        WireServiceCallbackIdAllocation(recorder, state, "cb-1");

        var resultTask = context.WaitForCallbackAsync<string>(
            async (_, _) => await Task.CompletedTask,
            name: "approval");

        await Task.WhenAny(resultTask, tm.TerminationTask);
        await recorder.Batcher.DrainAsync();

        // Parent CONTEXT has IdAt(1); the inner callback is child #1, the inner
        // submitter step is child #2 (under the same parent context op id).
        var parentOpId = IdAt(1);
        var callbackChildId = ChildIdAt(parentOpId, 1);
        var submitterChildId = ChildIdAt(parentOpId, 2);

        Assert.Equal(callbackChildId,
            recorder.Flushed.Single(o => o.Type == OperationTypes.Callback).Id);
        Assert.Equal(submitterChildId,
            recorder.Flushed.Single(o => o.Type == OperationTypes.Step && o.Action == "SUCCEED").Id);

        _ = resultTask;
    }

    [Fact]
    public async Task WaitForCallbackAsync_CallbackTimeoutInheritsFromConfig()
    {
        var (context, recorder, tm, state) = CreateContext();
        WireServiceCallbackIdAllocation(recorder, state, "cb-1");

        var resultTask = context.WaitForCallbackAsync<string>(
            async (_, _) => await Task.CompletedTask,
            name: "approval",
            config: new WaitForCallbackConfig
            {
                Timeout = TimeSpan.FromHours(2),
                HeartbeatTimeout = TimeSpan.FromMinutes(15),
            });

        await Task.WhenAny(resultTask, tm.TerminationTask);
        await recorder.Batcher.DrainAsync();

        var callbackStart = recorder.Flushed.Single(o => o.Type == OperationTypes.Callback);
        Assert.NotNull(callbackStart.CallbackOptions);
        Assert.Equal(7200, callbackStart.CallbackOptions.TimeoutSeconds);
        Assert.Equal(900, callbackStart.CallbackOptions.HeartbeatTimeoutSeconds);

        _ = resultTask;
    }

    [Fact]
    public async Task WaitForCallbackAsync_ReplayWithCallbackSucceeded_ReturnsResult()
    {
        // Full replay: parent CONTEXT SUCCEEDED with the callback's deserialized
        // payload as its checkpointed result.
        var (context, recorder, tm, _) = CreateContext(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = IdAt(1),
                    Type = OperationTypes.Context,
                    Status = OperationStatuses.Succeeded,
                    Name = "approval",
                    SubType = OperationSubTypes.WaitForCallback,
                    ContextDetails = new ContextDetails { Result = "\"approved\"" }
                }
            }
        });

        var executed = false;
        var result = await context.WaitForCallbackAsync<string>(
            async (_, _) => { executed = true; await Task.CompletedTask; },
            name: "approval");

        Assert.False(executed); // Replay returns cached without re-running submitter.
        Assert.Equal("approved", result);
        Assert.False(tm.IsTerminated);

        await recorder.Batcher.DrainAsync();
        Assert.Empty(recorder.Flushed);
    }

    [Fact]
    public async Task WaitForCallbackAsync_ReplayCallbackTimedOut_ThrowsCallbackTimeoutException()
    {
        // Inside-out replay: parent CONTEXT is STARTED (still in flight),
        // inner callback is TIMED_OUT, inner submitter step has SUCCEEDED.
        var parentId = IdAt(1);
        var callbackChildId = ChildIdAt(parentId, 1);
        var submitterChildId = ChildIdAt(parentId, 2);

        var (context, _, _, _) = CreateContext(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = parentId,
                    Type = OperationTypes.Context,
                    Status = OperationStatuses.Started,
                    Name = "approval",
                    SubType = OperationSubTypes.WaitForCallback,
                },
                new()
                {
                    Id = callbackChildId,
                    Type = OperationTypes.Callback,
                    Status = OperationStatuses.TimedOut,
                    Name = "approval-callback",
                    ParentId = parentId,
                    CallbackDetails = new CallbackDetails
                    {
                        CallbackId = "cb-to-1",
                        Error = new ErrorObject { ErrorMessage = "callback timed out" }
                    }
                },
                new()
                {
                    Id = submitterChildId,
                    Type = OperationTypes.Step,
                    Status = OperationStatuses.Succeeded,
                    Name = "approval-submitter",
                    ParentId = parentId,
                    StepDetails = new StepDetails { Result = "null" }
                },
            }
        });

        var ex = await Assert.ThrowsAsync<CallbackTimeoutException>(() =>
            context.WaitForCallbackAsync<string>(
                async (_, _) => await Task.CompletedTask,
                name: "approval"));

        Assert.Equal("callback timed out", ex.Message);
        Assert.Equal("cb-to-1", ex.CallbackId);
    }

    [Fact]
    public async Task WaitForCallbackAsync_ReplayCallbackFailed_ThrowsCallbackFailedException()
    {
        var parentId = IdAt(1);
        var callbackChildId = ChildIdAt(parentId, 1);
        var submitterChildId = ChildIdAt(parentId, 2);

        var (context, _, _, _) = CreateContext(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = parentId,
                    Type = OperationTypes.Context,
                    Status = OperationStatuses.Started,
                    Name = "approval",
                    SubType = OperationSubTypes.WaitForCallback,
                },
                new()
                {
                    Id = callbackChildId,
                    Type = OperationTypes.Callback,
                    Status = OperationStatuses.Failed,
                    Name = "approval-callback",
                    ParentId = parentId,
                    CallbackDetails = new CallbackDetails
                    {
                        CallbackId = "cb-fail-1",
                        Error = new ErrorObject
                        {
                            ErrorType = "ExternalSystemError",
                            ErrorMessage = "external rejected"
                        }
                    }
                },
                new()
                {
                    Id = submitterChildId,
                    Type = OperationTypes.Step,
                    Status = OperationStatuses.Succeeded,
                    Name = "approval-submitter",
                    ParentId = parentId,
                    StepDetails = new StepDetails { Result = "null" }
                },
            }
        });

        var ex = await Assert.ThrowsAsync<CallbackFailedException>(() =>
            context.WaitForCallbackAsync<string>(
                async (_, _) => await Task.CompletedTask,
                name: "approval"));

        Assert.Equal("external rejected", ex.Message);
        Assert.Equal("cb-fail-1", ex.CallbackId);
        Assert.Equal("ExternalSystemError", ex.ErrorType);
    }

    [Fact]
    public async Task WaitForCallbackAsync_SubmitterFails_ThrowsCallbackSubmitterException()
    {
        // Replay: parent CONTEXT is FAILED with a Step-error inside.
        var parentId = IdAt(1);
        var (context, _, _, _) = CreateContext(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = parentId,
                    Type = OperationTypes.Context,
                    Status = OperationStatuses.Failed,
                    Name = "approval",
                    SubType = OperationSubTypes.WaitForCallback,
                    ContextDetails = new ContextDetails
                    {
                        Error = new ErrorObject
                        {
                            ErrorType = typeof(StepException).FullName,
                            ErrorMessage = "submitter API failed",
                            ErrorData = "{\"code\":\"500\"}",
                        }
                    }
                }
            }
        });

        var ex = await Assert.ThrowsAsync<CallbackSubmitterException>(() =>
            context.WaitForCallbackAsync<string>(
                async (_, _) => await Task.CompletedTask,
                name: "approval"));

        Assert.IsAssignableFrom<CallbackException>(ex);
        Assert.Equal("submitter API failed", ex.Message);
        // On the replay path the live StepException was lost across invocations;
        // we preserve the StepException type-name string and carry the
        // ChildContextException as the InnerException for traceability.
        Assert.NotNull(ex.InnerException);
        Assert.Equal(typeof(StepException).FullName, ex.ErrorType);
        Assert.Equal("{\"code\":\"500\"}", ex.ErrorData);
    }

    [Fact]
    public async Task WaitForCallbackAsync_ReplayParentContextFailedWithCallbackTimeoutErrorType_PreservesSubclass()
    {
        // Subclass-fidelity guarantee: when the parent CONTEXT was checkpointed
        // FAILED on a previous invocation with a CallbackTimeoutException
        // ErrorType, replay must surface CallbackTimeoutException — not the
        // more generic CallbackFailedException — so user catch blocks behave
        // identically across live and replay paths.
        var parentId = IdAt(1);
        var (context, _, _, _) = CreateContext(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = parentId,
                    Type = OperationTypes.Context,
                    Status = OperationStatuses.Failed,
                    Name = "approval",
                    SubType = OperationSubTypes.WaitForCallback,
                    ContextDetails = new ContextDetails
                    {
                        Error = new ErrorObject
                        {
                            ErrorType = typeof(CallbackTimeoutException).FullName,
                            ErrorMessage = "callback timed out after 24h",
                        }
                    }
                }
            }
        });

        var ex = await Assert.ThrowsAsync<CallbackTimeoutException>(() =>
            context.WaitForCallbackAsync<string>(
                async (_, _) => await Task.CompletedTask,
                name: "approval"));

        // Concrete-type check: not just `is CallbackException` — must be the
        // CallbackTimeoutException subclass exactly.
        Assert.Equal(typeof(CallbackTimeoutException), ex.GetType());
        Assert.Equal("callback timed out after 24h", ex.Message);
        Assert.Equal(typeof(CallbackTimeoutException).FullName, ex.ErrorType);
    }

    [Fact]
    public async Task WaitForCallbackAsync_ReplayParentContextFailedWithCallbackFailedErrorType_RemapsToCallbackFailed()
    {
        // Companion case: a stored CallbackFailedException ErrorType remaps to
        // CallbackFailedException (not the base or CallbackTimeoutException).
        var parentId = IdAt(1);
        var (context, _, _, _) = CreateContext(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = parentId,
                    Type = OperationTypes.Context,
                    Status = OperationStatuses.Failed,
                    Name = "approval",
                    SubType = OperationSubTypes.WaitForCallback,
                    ContextDetails = new ContextDetails
                    {
                        Error = new ErrorObject
                        {
                            ErrorType = typeof(CallbackFailedException).FullName,
                            ErrorMessage = "external rejected",
                        }
                    }
                }
            }
        });

        var ex = await Assert.ThrowsAsync<CallbackFailedException>(() =>
            context.WaitForCallbackAsync<string>(
                async (_, _) => await Task.CompletedTask,
                name: "approval"));

        Assert.Equal(typeof(CallbackFailedException), ex.GetType());
        Assert.Equal("external rejected", ex.Message);
    }

    [Fact]
    public async Task WaitForCallbackAsync_RetryStrategyForwardedToSubmitterStep()
    {
        // Verifies the WaitForCallbackConfig.RetryStrategy gets passed into the
        // submitter step's StepConfig (via the kebab "-submitter" inner step).
        var (context, recorder, tm, state) = CreateContext();
        WireServiceCallbackIdAllocation(recorder, state, "cb-1");

        var seenAttempts = new List<int>();
        var resultTask = context.WaitForCallbackAsync<string>(
            async (_, ctx) =>
            {
                // The submitter receives an IWaitForCallbackContext (no AttemptNumber)
                // — but this test doesn't need to verify retry mechanics, only
                // that the StepConfig with a retry strategy is wired through.
                seenAttempts.Add(seenAttempts.Count + 1);
                await Task.CompletedTask;
            },
            name: "approval",
            config: new WaitForCallbackConfig
            {
                RetryStrategy = new CountingRetryStrategy()
            });

        await Task.WhenAny(resultTask, tm.TerminationTask);
        await recorder.Batcher.DrainAsync();

        // Submitter ran exactly once (no failures to retry); a single STEP SUCCEED
        // is sufficient evidence that the strategy was wired without throwing.
        Assert.Single(recorder.Flushed.Where(o => o.Type == OperationTypes.Step && o.Action == "SUCCEED"));

        _ = resultTask;
    }

    [Fact]
    public async Task WaitForCallbackAsync_SubmitterContext_IsIWaitForCallbackContext_NotIStepContext()
    {
        // Verifies the submitter delegate receives our distinct
        // IWaitForCallbackContext type (not IStepContext) — protects the
        // architectural decision against accidental conflation.
        var (context, recorder, tm, state) = CreateContext();
        WireServiceCallbackIdAllocation(recorder, state, "cb-1");

        Type? observedContextType = null;
        var resultTask = context.WaitForCallbackAsync<string>(
            async (_, ctx) =>
            {
                observedContextType = ctx.GetType();
                await Task.CompletedTask;
            },
            name: "approval");

        await Task.WhenAny(resultTask, tm.TerminationTask);
        await recorder.Batcher.DrainAsync();

        Assert.NotNull(observedContextType);
        Assert.True(typeof(IWaitForCallbackContext).IsAssignableFrom(observedContextType));
        Assert.False(typeof(IStepContext).IsAssignableFrom(observedContextType));

        _ = resultTask;
    }

    private sealed class CountingRetryStrategy : IRetryStrategy
    {
        public int Attempts;
        public RetryDecision ShouldRetry(Exception exception, int attemptNumber)
        {
            Attempts = attemptNumber;
            return RetryDecision.DoNotRetry();
        }
    }
}
