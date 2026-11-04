// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using Amazon.Lambda.DurableExecution.Internal;
using Amazon.Lambda.DurableExecution.Services;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.Lambda.TestUtilities;
using Xunit;
using SdkOperationUpdate = Amazon.Lambda.Model.OperationUpdate;

namespace Amazon.Lambda.DurableExecution.Tests;

/// <summary>
/// Tests for running durable functions on Lambda Managed Instances (LMI), where
/// several invocations run in one process at the same time and Lambda does not
/// stop an invocation that times out.
/// </summary>
/// <remarks>
/// These tests drive <see cref="DurableFunction"/> directly with an in-memory
/// service client, running invocations concurrently the way RuntimeSupport does
/// in multi-concurrency mode. LMI mode is selected through
/// <see cref="DurableInvocationOptions.IsMultiConcurrency"/> rather than the
/// process-wide environment variable, so these tests can run in parallel.
/// Ordering is controlled with signals, never with sleeps; the short deadlines
/// used below only need to expire eventually, and no test asserts on elapsed time.
/// </remarks>
public class LambdaManagedInstancesTests
{
    // Guards against a hang turning into a stuck test run. Not a timing assertion.
    private static readonly TimeSpan HangGuard = TimeSpan.FromSeconds(30);

    private static readonly DurableInvocationOptions LmiOptions = new() { IsMultiConcurrency = true };
    private static readonly DurableInvocationOptions OnDemandOptions = new() { IsMultiConcurrency = false };

    private static string IdAt(int position) => OperationIdGenerator.HashOperationId(position.ToString());

    // ── Teardown ordering (all modes) ──────────────────────────────────

    [Fact]
    public void WorkflowCancellation_TerminateThenDispose_TokenIsAlwaysCancelled()
    {
        // Termination delivers its cancel through a threadpool continuation.
        // Disposing right after Terminate used to win that race and drop the
        // cancel. Repeat to make the old race show up reliably.
        for (var i = 0; i < 500; i++)
        {
            var tm = new TerminationManager();
            var wfc = new WorkflowCancellation(tm);
            var token = wfc.Token;

            tm.Terminate(TerminationReason.WaitScheduled);
            wfc.Dispose();

            Assert.True(token.IsCancellationRequested, $"Cancellation lost on iteration {i}.");
        }
    }

    [Fact]
    public void WorkflowCancellation_TokenIsUsableAfterDispose()
    {
        var wfc = new WorkflowCancellation(new TerminationManager());
        wfc.Dispose();

        // Abandoned user code reads the token after the invocation ends. It must
        // get a cancelled token, not an ObjectDisposedException.
        var token = wfc.Token;
        Assert.True(token.IsCancellationRequested);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None, token);
        Assert.True(linked.IsCancellationRequested);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Suspended_AbandonedStepTokenIsCancelledBeforeWrapperReturns(bool isMultiConcurrency)
    {
        // Repeat to cover warm, back-to-back invocations of the same handler.
        for (var i = 0; i < 20; i++)
        {
            var client = new FakeServiceClient();
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            CancellationToken stepToken = default;

            var output = await DurableFunction.WrapAsync<Payload, string>(
                async (input, ctx) =>
                {
                    // Not awaited: this step is still running when the workflow suspends.
                    ctx.StepAsync<int>(async (_, ct) =>
                    {
                        stepToken = ct;
                        entered.TrySetResult();
                        await Task.Delay(Timeout.Infinite, ct);
                        return 0;
                    }, name: "slow").Observe();
                    await entered.Task;

                    await ctx.WaitAsync(TimeSpan.FromSeconds(30), name: "wait");
                    return "unreachable";
                },
                Input($"arn:abandoned-{i}"),
                Context(TimeSpan.FromMinutes(5)),
                client,
                new DurableInvocationOptions { IsMultiConcurrency = isMultiConcurrency });

            Assert.Equal(InvocationStatus.Pending, output.Status);
            // Checked immediately: cancellation must already be delivered.
            Assert.True(stepToken.IsCancellationRequested, $"Abandoned step not cancelled on iteration {i}.");
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AfterTeardown_AbandonedCodeCannotStartNewOperations(bool isMultiConcurrency)
    {
        var client = new FakeServiceClient();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var bodyRan = false;
        Task<int>? leaked = null;

        var output = await DurableFunction.WrapAsync<Payload, string>(
            async (_, ctx) =>
            {
                // Work the workflow started but never awaited; it resumes after the
                // invocation has ended.
                leaked = Task.Run(async () =>
                {
                    await release.Task;
                    return await ctx.StepAsync<int>(async (_, _) =>
                    {
                        bodyRan = true;
                        await Task.CompletedTask;
                        return 1;
                    }, name: "late");
                });

                await ctx.WaitAsync(TimeSpan.FromSeconds(30), name: "wait");
                return "unreachable";
            },
            Input("arn:leaked"),
            Context(TimeSpan.FromMinutes(5)),
            client,
            new DurableInvocationOptions { IsMultiConcurrency = isMultiConcurrency });

        Assert.Equal(InvocationStatus.Pending, output.Status);
        var checkpointsAtReturn = client.Calls.Count;

        release.SetResult();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => leaked!.NoHang());

        Assert.False(bodyRan);
        Assert.Equal(checkpointsAtReturn, client.Calls.Count);
    }

    // ── Deadline: on-demand is unchanged ───────────────────────────────

    [Theory]
    [InlineData(false, 300_000, null)]   // on-demand: never a deadline
    [InlineData(false, 1, null)]
    [InlineData(true, 0, 0)]             // LMI and already expired: time out at once
    [InlineData(true, -5, 0)]
    [InlineData(true, 300_000, 300_000)]
    public void InvocationDeadline_Resolve(bool isMultiConcurrency, long remainingMs, long? expectedMs)
    {
        var deadline = InvocationDeadline.Resolve(TimeSpan.FromMilliseconds(remainingMs), isMultiConcurrency);
        Assert.Equal(expectedMs, deadline is { } d ? (long)d.TotalMilliseconds : null);
    }

    [Fact]
    public void InvocationDeadline_Resolve_IgnoresValuesBeyondTimerRange()
    {
        Assert.Null(InvocationDeadline.Resolve(TimeSpan.FromDays(60), isMultiConcurrency: true));
    }

    [Fact]
    public async Task OnDemand_HydrationIsNotCancellable()
    {
        var client = new FakeServiceClient();

        var output = await DurableFunction.WrapAsync<Payload, string>(
            (input, _) => Task.FromResult(input.Id!),
            Input("arn:hydrate-od", nextMarker: "page-2"),
            Context(TimeSpan.FromMilliseconds(1)),
            client,
            OnDemandOptions);

        Assert.Equal(InvocationStatus.Succeeded, output.Status);
        Assert.False(Assert.Single(client.HydrationTokens).CanBeCanceled);
    }

    [Fact]
    public async Task Lmi_HydrationReceivesDeadlineToken()
    {
        var client = new FakeServiceClient();

        var output = await DurableFunction.WrapAsync<Payload, string>(
            (input, _) => Task.FromResult(input.Id!),
            Input("arn:hydrate-lmi", nextMarker: "page-2"),
            Context(TimeSpan.FromMinutes(5)),
            client,
            LmiOptions);

        Assert.Equal(InvocationStatus.Succeeded, output.Status);
        Assert.True(Assert.Single(client.HydrationTokens).CanBeCanceled);
    }

    // ── Deadline: LMI ──────────────────────────────────────────────────

    [Fact]
    public async Task Lmi_DeadlinePassesDuringStep_ThrowsTimeout_WithoutFailCheckpoint()
    {
        var client = new FakeServiceClient();
        CancellationToken stepToken = default;

        var ex = await Assert.ThrowsAsync<TimeoutException>(() =>
            DurableFunction.WrapAsync<Payload, string>(
                async (_, ctx) => await ctx.StepAsync<string>(async (_, ct) =>
                {
                    stepToken = ct;
                    await Task.Delay(Timeout.Infinite, ct);
                    return "unreachable";
                }, name: "slow"),
                Input("arn:deadline-step"),
                Context(TimeSpan.FromMilliseconds(100)),
                client,
                LmiOptions).NoHang());

        Assert.Contains("arn:deadline-step", ex.Message);
        Assert.True(stepToken.IsCancellationRequested);
        // A timeout must not be recorded as a durable failure; replay retries it.
        Assert.DoesNotContain(client.AllUpdates, u => u.Action == OperationAction.FAIL);
    }

    [Fact]
    public async Task Lmi_WorkflowConvertsCancellationToOwnError_StillReportsTimeout()
    {
        // User code that catches the cancellation and throws its own exception
        // must not turn a timeout into a permanently failed execution.
        var client = new FakeServiceClient();

        await Assert.ThrowsAsync<TimeoutException>(() =>
            DurableFunction.WrapAsync<Payload, string>(
                async (_, ctx) =>
                {
                    try
                    {
                        return await ctx.StepAsync<string>(async (_, ct) =>
                        {
                            await Task.Delay(Timeout.Infinite, ct);
                            return "unreachable";
                        }, name: "slow");
                    }
                    catch (OperationCanceledException)
                    {
                        throw new InvalidOperationException("user code gave up");
                    }
                },
                Input("arn:deadline-user-catch"),
                Context(TimeSpan.FromMilliseconds(100)),
                client,
                LmiOptions).NoHang());
    }

    [Fact]
    public async Task RunAsync_WorkflowFaultsFromDeadlineBeforeDeadlineTaskCompletes_ReportsDeadline()
    {
        // Forces the race where the workflow task faults before DeadlineTask
        // completes: the fault is raised synchronously inside the token's
        // cancellation callback, which runs before the deadline task is
        // signalled. The fault must still be reported as a deadline, not as a
        // durable failure.
        var tm = new TerminationManager();
        using var wfc = new WorkflowCancellation(tm, TimeSpan.FromMilliseconds(50));

        var result = await DurableExecutionHandler.RunAsync<string>(
            new ExecutionState(),
            tm,
            async () =>
            {
                // No RunContinuationsAsynchronously: the await below resumes
                // inline inside the cancellation callback.
                var faulted = new TaskCompletionSource<string>();
                wfc.Token.Register(() => faulted.TrySetException(new InvalidOperationException("user gave up")));
                return await faulted.Task;
            },
            wfc).NoHang();

        Assert.True(result.DeadlineExceeded);
        Assert.Null(result.Exception);
    }

    // ── Copilot review round 1 ─────────────────────────────────────────

    [Fact]
    public async Task Lmi_AlreadyExpiredInvocation_TimesOutWithoutRunningWorkflow()
    {
        var client = new FakeServiceClient();
        var ran = false;

        await Assert.ThrowsAsync<TimeoutException>(() =>
            DurableFunction.WrapAsync<Payload, string>(
                (input, _) => { ran = true; return Task.FromResult(input.Id!); },
                Input("arn:expired"),
                Context(TimeSpan.FromMilliseconds(-1)),
                client,
                LmiOptions).NoHang());

        Assert.False(ran);
        Assert.Empty(client.Calls);
    }

    [Fact]
    public void WorkflowCancellation_DeadlineIsReported_EvenWhenTerminationCancelledFirst()
    {
        var tm = new TerminationManager();
        using var wfc = new WorkflowCancellation(tm, TimeSpan.FromMilliseconds(50));
        var deadlineHit = new ManualResetEventSlim();
        wfc.DeadlineToken.Register(deadlineHit.Set);

        // A suspension sets the first cancellation reason before the deadline.
        wfc.BeginTeardown();
        Assert.True(deadlineHit.Wait(HangGuard));

        Assert.True(wfc.IsDeadlineExceeded);
    }

    [Fact]
    public async Task WorkflowCancellation_ThrowingCallback_DoesNotEscapeDeadlineOrTeardown()
    {
        var tm = new TerminationManager();
        var wfc = new WorkflowCancellation(tm, TimeSpan.FromMilliseconds(50));
        // Callbacks run in reverse registration order: the throwing one runs
        // first, then this one. It still running proves Cancel delivered to
        // every callback despite the throw.
        var otherCallbackRan = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        wfc.Token.Register(() => otherCallbackRan.TrySetResult());
        wfc.Token.Register(() => throw new InvalidOperationException("user callback threw"));

        // The deadline fires on a timer thread: the throwing callback must not
        // escape there (that would crash the test host) or block the signal.
        await wfc.DeadlineTask.NoHang();
        await otherCallbackRan.Task.NoHang();
        Assert.True(wfc.Token.IsCancellationRequested);

        // Teardown and dispose must not throw either.
        wfc.BeginTeardown();
        wfc.Dispose();
    }

    [Fact]
    public async Task WorkflowCancellation_BlockingCallback_DoesNotDelayDeadlineSignal()
    {
        var tm = new TerminationManager();
        using var release = new ManualResetEventSlim();
        var wfc = new WorkflowCancellation(tm, TimeSpan.FromMilliseconds(50));
        wfc.Token.Register(() => release.Wait());

        try
        {
            // DeadlineTask completes even though a cancellation callback blocks
            // the thread that is delivering the cancel.
            await wfc.DeadlineTask.NoHang();
            Assert.True(wfc.IsDeadlineExceeded);
        }
        finally
        {
            release.Set();
            wfc.Dispose();
        }
    }

    [Fact]
    public async Task RunAsync_WorkflowReturnsFallbackAfterDeadline_ReportsDeadline()
    {
        // The workflow observes cancellation and returns a value. It can win the
        // race against DeadlineTask; the deadline must still decide the outcome.
        var tm = new TerminationManager();
        using var wfc = new WorkflowCancellation(tm, TimeSpan.FromMilliseconds(50));

        var result = await DurableExecutionHandler.RunAsync<string>(
            new ExecutionState(),
            tm,
            async () =>
            {
                // Completes inline in a deadline-token callback registered after
                // WorkflowCancellation's own, so it runs first (callbacks run in
                // reverse order): the workflow finishes while the deadline has
                // passed but DeadlineTask is not yet signalled. The workflow wins
                // the race; only the deadline check can catch it.
                var done = new TaskCompletionSource<string>();
                wfc.DeadlineToken.Register(() => done.TrySetResult("fallback"));
                return await done.Task;
            },
            wfc).NoHang();

        Assert.True(result.DeadlineExceeded);
        Assert.Null(result.Result);
    }

    [Fact]
    public async Task RunAsync_WorkflowStartedAfterTeardown_RunsNoWorkflowCode()
    {
        var tm = new TerminationManager();
        using var wfc = new WorkflowCancellation(tm);
        wfc.BeginTeardown();
        var ran = false;

        await DurableExecutionHandler.RunAsync<string>(
            new ExecutionState(), tm,
            () => { ran = true; return Task.FromResult("x"); },
            wfc).NoHang();

        Assert.False(ran);
    }

    [Theory]
    [InlineData("step")]
    [InlineData("child")]
    [InlineData("condition")]
    public async Task Lmi_BodyConvertsDeadlineCancellation_NoFailOrRetryCheckpoint(string kind)
    {
        // The user body catches its cancelled token and throws a different
        // exception. That must not be recorded as a real failure.
        var client = new FakeServiceClient();
        var retry = new StepConfig { RetryStrategy = RetryStrategy.Exponential(maxAttempts: 3) };

        async Task<string> Body(CancellationToken ct)
        {
            try { await Task.Delay(Timeout.Infinite, ct); }
            catch (OperationCanceledException) { throw new InvalidOperationException("gave up"); }
            return "unreachable";
        }

        Func<Payload, IDurableContext, Task<string>> workflow = kind switch
        {
            "step" => async (_, ctx) => await ctx.StepAsync<string>((_, ct) => Body(ct), name: "s", config: retry),
            "child" => async (_, ctx) => await ctx.RunInChildContextAsync<string>((_, ct) => Body(ct), name: "c"),
            _ => async (_, ctx) => (await ctx.WaitForConditionAsync<int>(
                async (state, _, ct) => { await Body(ct); return state; },
                new WaitForConditionConfig<int>
                {
                    InitialState = 0,
                    WaitStrategy = WaitStrategy.Fixed<int>(TimeSpan.FromSeconds(1)),
                },
                name: "w")).ToString(),
        };

        await Assert.ThrowsAsync<TimeoutException>(() =>
            DurableFunction.WrapAsync(workflow, Input($"arn:convert-{kind}"),
                Context(TimeSpan.FromMilliseconds(100)), client, LmiOptions).NoHang());

        Assert.DoesNotContain(client.AllUpdates,
            u => u.Action == OperationAction.FAIL || u.Action == OperationAction.RETRY);
    }

    [Theory]
    [InlineData("step")]
    [InlineData("child")]
    [InlineData("condition")]
    public async Task Operation_BodyConvertsDeadlineCancellation_EnqueuesNoFailOrRetry(string kind)
    {
        // Drives the operation directly, with nothing tearing the batcher down,
        // so a FAIL/RETRY the operation enqueued would definitely be flushed.
        var tm = new TerminationManager();
        using var wfc = new WorkflowCancellation(tm, TimeSpan.FromMilliseconds(50));
        var recorder = new RecordingBatcher();
        var ctx = new DurableContext(new ExecutionState(), tm, wfc, new OperationIdGenerator(),
            "arn:test", Context(TimeSpan.FromMinutes(5)), recorder.Batcher);
        var retry = new StepConfig { RetryStrategy = RetryStrategy.Exponential(maxAttempts: 3) };

        async Task<int> Body(CancellationToken ct)
        {
            try { await Task.Delay(Timeout.Infinite, ct); }
            catch (OperationCanceledException) { throw new InvalidOperationException("gave up"); }
            return 0;
        }

        Task op = kind switch
        {
            "step" => ctx.StepAsync<int>((_, ct) => Body(ct), name: "s", config: retry),
            "child" => ctx.RunInChildContextAsync<int>((_, ct) => Body(ct), name: "c"),
            _ => ctx.WaitForConditionAsync<int>(
                async (state, _, ct) => { await Body(ct); return state; },
                new WaitForConditionConfig<int>
                {
                    InitialState = 0,
                    WaitStrategy = WaitStrategy.Fixed<int>(TimeSpan.FromSeconds(1)),
                },
                name: "w"),
        };

        // The operation must surface the error without recording a failure. A
        // RETRY would suspend instead, so only wait for completion or suspension.
        await Task.WhenAny(op, tm.TerminationTask).NoHang();
        await recorder.Batcher.DrainAsync();

        Assert.True(op.IsFaulted, "The operation should rethrow the body's exception.");
        // Observe the fault: other tests count process-wide unobserved exceptions.
        Assert.NotNull(op.Exception);
        Assert.DoesNotContain(recorder.Flushed,
            u => u.Action == OperationAction.FAIL || u.Action == OperationAction.RETRY);
    }

    [Fact]
    public async Task StepAsync_TornDownWhileStartCheckpointIsAwaited_BodyDoesNotRun()
    {
        // AtMostOncePerRetry awaits its START checkpoint before the body. The
        // workflow is torn down while that flush is in progress.
        var flushEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFlush = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var tm = new TerminationManager();
        using var wfc = new WorkflowCancellation(tm);
        await using var batcher = new CheckpointBatcher("t0", (_, _, _) =>
        {
            flushEntered.TrySetResult();
            return releaseFlush.Task;
        });
        var ctx = new DurableContext(new ExecutionState(), tm, wfc, new OperationIdGenerator(),
            "arn:test", Context(TimeSpan.FromMinutes(5)), batcher);
        var bodyRan = false;

        var step = ctx.StepAsync<int>(
            async (_, _) => { bodyRan = true; await Task.CompletedTask; return 1; },
            name: "s",
            config: new StepConfig { Semantics = StepSemantics.AtMostOncePerRetry });

        await flushEntered.Task.NoHang();
        wfc.BeginTeardown();
        releaseFlush.SetResult("t1");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => step.NoHang());
        Assert.False(bodyRan);
    }

    [Fact]
    public async Task Lmi_DeadlinePassesDuringFinalDrain_ThrowsTimeout()
    {
        // The workflow finishes in time; its final checkpoint is still in flight
        // when the deadline passes and completes inside the cleanup budget.
        var client = new FakeServiceClient
        {
            OnCheckpoint = _ => Task.Delay(400),
        };

        await Assert.ThrowsAsync<TimeoutException>(() =>
            DurableFunction.WrapAsync<Payload, string>(
                async (input, ctx) =>
                {
                    // Fire-and-forget START plus a SUCCEED queued at the very end.
                    ctx.StepAsync<string>(async (_, _) => { await Task.CompletedTask; return "a"; }, name: "a").Observe();
                    await Task.Yield();
                    return "done";
                },
                Input("arn:late-drain"),
                Context(TimeSpan.FromMilliseconds(100)),
                client,
                new DurableInvocationOptions { IsMultiConcurrency = true, CleanupTimeout = TimeSpan.FromSeconds(5) })
            .NoHang());
    }

    [Fact]
    public async Task Lmi_HydrationIgnoringCancellation_IsStillBounded()
    {
        var never = new TaskCompletionSource();
        var client = new FakeServiceClient { OnGetExecutionState = _ => never.Task };

        await Assert.ThrowsAsync<TimeoutException>(() =>
            DurableFunction.WrapAsync<Payload, string>(
                (input, _) => Task.FromResult(input.Id!),
                Input("arn:hydrate-ignores-token", nextMarker: "page-2"),
                Context(TimeSpan.FromMilliseconds(100)),
                client,
                LmiOptions).NoHang());
    }

    [Fact]
    public async Task CheckpointBatcher_AfterAbandon_SendsNoFurtherBatches()
    {
        // The worker must send two batches in one pass: with one operation per
        // batch, reading "a" and "b" together flushes [a] mid-pass, then [b].
        // The [a] flush ignores cancellation and stalls; the drain gives up and
        // abandons the worker. Releasing [a] afterwards must not send [b].
        var calls = 0;
        var warmupEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseWarmup = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var stalledEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseStalled = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var batcher = new CheckpointBatcher("t0", (_, _, _) =>
        {
            switch (Interlocked.Increment(ref calls))
            {
                case 1: warmupEntered.TrySetResult(); return releaseWarmup.Task;
                case 2: stalledEntered.TrySetResult(); return releaseStalled.Task;
                default: return Task.FromResult<string?>("t-next");
            }
        }, new CheckpointBatcherConfig { MaxBatchOperations = 1 });

        // Hold the worker in a warm-up flush so "a" and "b" queue up together.
        batcher.EnqueueAsync(Update("w")).Observe();
        await warmupEntered.Task.NoHang();
        batcher.EnqueueAsync(Update("a")).Observe();
        var second = batcher.EnqueueAsync(Update("b"));
        releaseWarmup.SetResult("t1");

        await stalledEntered.Task.NoHang();
        Assert.False(await batcher.DrainAsync(TimeSpan.FromMilliseconds(50)));
        releaseStalled.SetResult("t2");

        await Assert.ThrowsAnyAsync<Exception>(() => second.NoHang());
        Assert.Equal(2, Volatile.Read(ref calls));
        await batcher.DisposeAsync();
    }

    private static SdkOperationUpdate Update(string id) => new()
    {
        Id = id,
        Type = OperationTypes.Step,
        Action = OperationAction.START,
    };

    [Fact]
    public async Task Lmi_DeadlinePassesDuringHydration_ThrowsTimeout()
    {
        var client = new FakeServiceClient
        {
            OnGetExecutionState = ct => Task.Delay(Timeout.Infinite, ct),
        };
        var workflowRan = false;

        var ex = await Assert.ThrowsAsync<TimeoutException>(() =>
            DurableFunction.WrapAsync<Payload, string>(
                (input, _) => { workflowRan = true; return Task.FromResult(input.Id!); },
                Input("arn:deadline-hydrate", nextMarker: "page-2"),
                Context(TimeSpan.FromMilliseconds(100)),
                client,
                LmiOptions).NoHang());

        Assert.IsAssignableFrom<OperationCanceledException>(ex.InnerException);
        Assert.False(workflowRan);
    }

    [Fact]
    public async Task Lmi_CheckpointCallStallsPastDeadline_CleanupIsBounded()
    {
        // The checkpoint call ignores its cancellation token and never returns.
        // The invocation must still end instead of holding a processing loop.
        var neverCompletes = new TaskCompletionSource();
        var client = new FakeServiceClient { OnCheckpoint = _ => neverCompletes.Task };

        await Assert.ThrowsAsync<TimeoutException>(() =>
            DurableFunction.WrapAsync<Payload, string>(
                async (_, ctx) => await ctx.StepAsync<string>(
                    async (_, _) => { await Task.CompletedTask; return "done"; },
                    name: "step"),
                Input("arn:stalled-checkpoint"),
                Context(TimeSpan.FromMilliseconds(100)),
                client,
                new DurableInvocationOptions { IsMultiConcurrency = true, CleanupTimeout = TimeSpan.FromMilliseconds(100) })
            .NoHang());
    }

    [Fact]
    public async Task Lmi_CompletesBeforeDeadline_Succeeds()
    {
        var client = new FakeServiceClient();

        var output = await DurableFunction.WrapAsync<Payload, string>(
            async (input, ctx) => await ctx.StepAsync<string>(
                async (_, _) => { await Task.CompletedTask; return input.Id!; }, name: "step"),
            Input("arn:in-time", id: "abc"),
            Context(TimeSpan.FromMinutes(5)),
            client,
            LmiOptions);

        Assert.Equal(InvocationStatus.Succeeded, output.Status);
        Assert.Equal("\"abc\"", output.Result);
    }

    // ── Concurrent invocations ─────────────────────────────────────────

    [Fact]
    public async Task Lmi_ConcurrentInvocations_AreIsolated()
    {
        const int count = 8;
        var client = new FakeServiceClient();
        var handler = new SharedHandler(count);

        // All invocations go through one handler instance, as they would in one
        // LMI execution environment. Even-numbered ones suspend, odd ones finish.
        var tasks = Enumerable.Range(0, count).Select(i => Task.Run(() =>
            DurableFunction.WrapAsync<Payload, string>(
                handler.RunAsync,
                Input($"arn:exec-{i}", id: $"id-{i}", checkpointToken: $"initial-{i}"),
                Context(TimeSpan.FromMinutes(5), requestId: $"req-{i}"),
                client,
                LmiOptions))).ToArray();

        var outputs = await Task.WhenAll(tasks).NoHang();

        for (var i = 0; i < count; i++)
        {
            var arn = $"arn:exec-{i}";

            if (i % 2 == 0)
            {
                Assert.Equal(InvocationStatus.Pending, outputs[i].Status);
            }
            else
            {
                Assert.Equal(InvocationStatus.Succeeded, outputs[i].Status);
                Assert.Equal($"\"id-{i}:id-{i}\"", outputs[i].Result);
            }

            // Each step saw only its own invocation's context.
            Assert.Equal(arn, handler.ObservedArns[$"id-{i}"]);
            Assert.Equal($"req-{i}", handler.ObservedRequestIds[$"id-{i}"]);

            // Each execution's checkpoints carry only its own ids, payloads and
            // tokens, and its token chain starts from its own initial token.
            var calls = client.CallsFor(arn);
            Assert.NotEmpty(calls);
            Assert.Equal($"initial-{i}", calls[0].TokenIn);
            for (var c = 1; c < calls.Count; c++)
                Assert.Equal(calls[c - 1].TokenOut, calls[c].TokenIn);

            var updates = calls.SelectMany(c => c.Updates).ToList();
            Assert.All(updates, u => Assert.Contains(u.Id, new[] { IdAt(1), IdAt(2), IdAt(3) }));
            Assert.All(updates.Where(u => u.Payload != null), u => Assert.Contains($"id-{i}", u.Payload));
            Assert.Contains(updates, u => u.Id == IdAt(1) && u.Action == OperationAction.SUCCEED);
        }
    }

    [Fact]
    public async Task Lmi_PeerTimesOut_HealthyPeerIsUnaffected()
    {
        var client = new FakeServiceClient();
        var peerFinished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var healthyEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken healthyToken = default;

        var healthy = Task.Run(() => DurableFunction.WrapAsync<Payload, string>(
            async (input, ctx) => await ctx.StepAsync<string>(async (_, ct) =>
            {
                healthyToken = ct;
                healthyEntered.TrySetResult();
                // Keep running until the peer has timed out and been cleaned up.
                await peerFinished.Task;
                return input.Id!;
            }, name: "healthy"),
            Input("arn:healthy", id: "ok"),
            Context(TimeSpan.FromMinutes(5)),
            client,
            LmiOptions));

        await healthyEntered.Task.NoHang();

        var timingOut = DurableFunction.WrapAsync<Payload, string>(
            async (_, ctx) => await ctx.StepAsync<string>(async (_, ct) =>
            {
                await Task.Delay(Timeout.Infinite, ct);
                return "unreachable";
            }, name: "slow"),
            Input("arn:timing-out"),
            Context(TimeSpan.FromMilliseconds(100)),
            client,
            LmiOptions);

        await Assert.ThrowsAsync<TimeoutException>(() => timingOut.NoHang());
        Assert.False(healthyToken.IsCancellationRequested);

        peerFinished.SetResult();
        var output = await healthy.NoHang();

        Assert.Equal(InvocationStatus.Succeeded, output.Status);
        Assert.Equal("\"ok\"", output.Result);
        Assert.Contains(client.CallsFor("arn:healthy").SelectMany(c => c.Updates),
            u => u.Action == OperationAction.SUCCEED);
    }

    [Fact]
    public async Task Lmi_ReplayWhilePeerIsLive_SkipsCompletedSteps()
    {
        var client = new FakeServiceClient();
        var bothInside = new AsyncBarrier(2);
        var replayBodyRuns = 0;
        var freshBodyRuns = 0;

        // The replaying execution already completed step 1; only step 2 runs.
        var replay = Task.Run(() => DurableFunction.WrapAsync<Payload, string>(
            async (_, ctx) =>
            {
                var first = await ctx.StepAsync<string>(async (_, _) =>
                {
                    Interlocked.Increment(ref replayBodyRuns);
                    await Task.CompletedTask;
                    return "recomputed";
                }, name: "first");
                var second = await ctx.StepAsync<string>(async (_, _) =>
                {
                    await bothInside.SignalAndWaitAsync();
                    return "second";
                }, name: "second");
                return $"{first}+{second}";
            },
            Input("arn:replay", extraOperations: new[]
            {
                new Operation
                {
                    Id = IdAt(1), Type = OperationTypes.Step, Status = OperationStatuses.Succeeded,
                    Name = "first", StepDetails = new StepDetails { Result = "\"cached\"" },
                },
            }),
            Context(TimeSpan.FromMinutes(5)),
            client,
            LmiOptions));

        var fresh = Task.Run(() => DurableFunction.WrapAsync<Payload, string>(
            async (_, ctx) => await ctx.StepAsync<string>(async (_, _) =>
            {
                Interlocked.Increment(ref freshBodyRuns);
                await bothInside.SignalAndWaitAsync();
                return "fresh";
            }, name: "first"),
            Input("arn:fresh"),
            Context(TimeSpan.FromMinutes(5)),
            client,
            LmiOptions));

        var outputs = await Task.WhenAll(replay, fresh).NoHang();

        Assert.Equal("\"cached+second\"", outputs[0].Result);
        Assert.Equal("\"fresh\"", outputs[1].Result);
        Assert.Equal(0, replayBodyRuns);
        Assert.Equal(1, freshBodyRuns);
        Assert.DoesNotContain(client.CallsFor("arn:replay").SelectMany(c => c.Updates), u => u.Id == IdAt(1));
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    /// <summary>The configured hang guard, for <see cref="HangGuardExtensions"/>.</summary>
    internal static TimeSpan HangGuardTimeout => HangGuard;

    /// <summary>
    /// One handler instance shared by every concurrent invocation. Every step
    /// waits until all invocations are inside a step, so they truly overlap.
    /// </summary>
    private sealed class SharedHandler
    {
        private readonly AsyncBarrier _allInside;
        public ConcurrentDictionary<string, string> ObservedArns { get; } = new();
        public ConcurrentDictionary<string, string> ObservedRequestIds { get; } = new();

        public SharedHandler(int count) => _allInside = new AsyncBarrier(count);

        public async Task<string> RunAsync(Payload input, IDurableContext ctx)
        {
            var first = await ctx.StepAsync<string>(async (_, _) =>
            {
                ObservedArns[input.Id!] = ctx.ExecutionContext.DurableExecutionArn;
                ObservedRequestIds[input.Id!] = ctx.LambdaContext.AwsRequestId;
                await _allInside.SignalAndWaitAsync();
                return input.Id!;
            }, name: "first");

            var second = await ctx.StepAsync<string>(
                async (_, _) => { await Task.Yield(); return input.Id!; }, name: "second");

            var index = int.Parse(input.Id!.Substring("id-".Length));
            if (index % 2 == 0)
                await ctx.WaitAsync(TimeSpan.FromSeconds(30), name: "wait");

            return $"{first}:{second}";
        }
    }

    private sealed class AsyncBarrier
    {
        private int _remaining;
        private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public AsyncBarrier(int participants) => _remaining = participants;

        public Task SignalAndWaitAsync()
        {
            if (Interlocked.Decrement(ref _remaining) == 0)
                _released.TrySetResult();
            return _released.Task;
        }
    }

    public class Payload
    {
        public string? Id { get; set; }
    }

    private static TestLambdaContext Context(TimeSpan remainingTime, string requestId = "req") => new()
    {
        Serializer = new DefaultLambdaJsonSerializer(),
        RemainingTime = remainingTime,
        AwsRequestId = requestId,
    };

    private static DurableExecutionInvocationInput Input(
        string arn,
        string id = "id",
        string checkpointToken = "initial",
        string? nextMarker = null,
        IEnumerable<Operation>? extraOperations = null)
    {
        var operations = new List<Operation>
        {
            new()
            {
                Id = "exec-0",
                Type = OperationTypes.Execution,
                Status = OperationStatuses.Started,
                ExecutionDetails = new ExecutionDetails { InputPayload = $"{{\"Id\":\"{id}\"}}" },
            },
        };
        if (extraOperations != null) operations.AddRange(extraOperations);

        return new DurableExecutionInvocationInput
        {
            DurableExecutionArn = arn,
            CheckpointToken = checkpointToken,
            InitialExecutionState = new InitialExecutionState { Operations = operations, NextMarker = nextMarker },
        };
    }

    private sealed record CheckpointCall(string Arn, string? TokenIn, string TokenOut, IReadOnlyList<SdkOperationUpdate> Updates);

    /// <summary>
    /// In-memory durable service shared by concurrent invocations. Issues a new
    /// checkpoint token per call, scoped to the execution ARN.
    /// </summary>
    private sealed class FakeServiceClient : IDurableServiceClient
    {
        private readonly ConcurrentQueue<CheckpointCall> _calls = new();
        private readonly ConcurrentQueue<CancellationToken> _hydrationTokens = new();
        private int _tokenCounter;

        public Func<CancellationToken, Task>? OnCheckpoint { get; init; }
        public Func<CancellationToken, Task>? OnGetExecutionState { get; init; }

        public IReadOnlyList<CheckpointCall> Calls => _calls.ToArray();
        public IReadOnlyList<CancellationToken> HydrationTokens => _hydrationTokens.ToArray();
        public IEnumerable<SdkOperationUpdate> AllUpdates => Calls.SelectMany(c => c.Updates);
        public List<CheckpointCall> CallsFor(string arn) => Calls.Where(c => c.Arn == arn).ToList();

        public async Task<string?> CheckpointAsync(
            string durableExecutionArn,
            string? checkpointToken,
            IReadOnlyList<SdkOperationUpdate> pendingOperations,
            Action<IReadOnlyList<Operation>>? onNewOperations = null,
            CancellationToken cancellationToken = default)
        {
            if (OnCheckpoint != null) await OnCheckpoint(cancellationToken);

            var tokenOut = $"{durableExecutionArn}#{Interlocked.Increment(ref _tokenCounter)}";
            _calls.Enqueue(new CheckpointCall(durableExecutionArn, checkpointToken, tokenOut, pendingOperations.ToArray()));
            return tokenOut;
        }

        public async Task<(List<Operation> Operations, string? NextMarker)> GetExecutionStateAsync(
            string durableExecutionArn,
            string? checkpointToken,
            string marker,
            CancellationToken cancellationToken = default)
        {
            _hydrationTokens.Enqueue(cancellationToken);
            if (OnGetExecutionState != null) await OnGetExecutionState(cancellationToken);
            return (new List<Operation>(), null);
        }
    }
}

/// <summary>
/// Guards a test against hanging. Fails with an xUnit exception rather than
/// <see cref="TimeoutException"/>: the deadline tests assert on
/// <see cref="TimeoutException"/>, so <c>Task.WaitAsync(TimeSpan)</c> would turn
/// a hang into a false pass.
/// </summary>
internal static class HangGuardExtensions
{
    /// <summary>
    /// Observes a task the test deliberately does not await, so its fault never
    /// reaches TaskScheduler.UnobservedTaskException. Other tests in this
    /// assembly count that process-wide event.
    /// </summary>
    public static void Observe(this Task task) =>
        task.ContinueWith(static t => _ = t.Exception, CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

    public static async Task NoHang(this Task task)
    {
        if (await Task.WhenAny(task, Task.Delay(LambdaManagedInstancesTests.HangGuardTimeout)) != task)
            throw new Xunit.Sdk.XunitException("The operation did not complete within the hang guard; it appears to hang.");
        await task;
    }

    public static async Task<T> NoHang<T>(this Task<T> task)
    {
        await ((Task)task).NoHang();
        return await task;
    }
}
