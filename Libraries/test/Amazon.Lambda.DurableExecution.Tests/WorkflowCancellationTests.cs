// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.DurableExecution.Internal;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.Lambda.TestUtilities;
using Xunit;

namespace Amazon.Lambda.DurableExecution.Tests;

/// <summary>
/// Cancellation-flow tests for <see cref="WorkflowCancellation"/> and the
/// linked-token contract surfaced through <see cref="IDurableContext"/>.
/// Companion to
/// <c>Libraries/src/Amazon.Lambda.DurableExecution/docs/design/cancellation-design.md</c>.
/// </summary>
public class WorkflowCancellationTests
{
    private static TestLambdaContext CreateLambdaContext() =>
        new() { Serializer = new DefaultLambdaJsonSerializer() };

    private sealed record Harness(
        DurableContext Context,
        TerminationManager Termination,
        WorkflowCancellation WorkflowCancellation,
        RecordingBatcher Recorder);

    private static Harness CreateHarness()
    {
        var state = new ExecutionState();
        var tm = new TerminationManager();
        var wfc = new WorkflowCancellation(tm);
        var idGen = new OperationIdGenerator();
        var recorder = new RecordingBatcher();
        var ctx = new DurableContext(state, tm, wfc, idGen, "arn:test", CreateLambdaContext(), recorder.Batcher);
        return new Harness(ctx, tm, wfc, recorder);
    }

    // ── WorkflowCancellation primitive ──────────────────────────────────

    [Fact]
    public void Token_NotCancelled_BeforeTermination()
    {
        var tm = new TerminationManager();
        using var wfc = new WorkflowCancellation(tm);

        Assert.False(wfc.Token.IsCancellationRequested);
    }

    [Fact]
    public async Task Token_CancelledWhenTerminationFires()
    {
        var tm = new TerminationManager();
        using var wfc = new WorkflowCancellation(tm);
        var observed = new TaskCompletionSource();
        wfc.Token.Register(() => observed.TrySetResult());

        tm.Terminate(TerminationReason.WaitScheduled);

        await observed.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(wfc.Token.IsCancellationRequested);
    }

    [Fact]
    public void Dispose_AfterTermination_DoesNotThrow()
    {
        var tm = new TerminationManager();
        var wfc = new WorkflowCancellation(tm);
        tm.Terminate(TerminationReason.WaitScheduled);
        wfc.Dispose();
    }

    // ── StepAsync token plumbing ────────────────────────────────────────

    [Fact]
    public async Task StepAsync_CallerToken_PropagatesIntoFunc()
    {
        // Cancel AFTER the func has started — pre-cancellation would short-circuit
        // in StepOperation.ExecuteFunc's ThrowIfCancellationRequested before the
        // user body runs and we'd never observe the propagation.
        var harness = CreateHarness();
        using var caller = new CancellationTokenSource();
        var entered = new TaskCompletionSource();

        var task = harness.Context.StepAsync<int>(async (_, ct) =>
        {
            entered.TrySetResult();
            // Block on the linked token; if the caller's cancel propagates into
            // ct via the linked CTS, this throws.
            await Task.Delay(Timeout.Infinite, ct);
            return 0;
        }, name: "step", cancellationToken: caller.Token);

        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        caller.Cancel();

        await Assert.ThrowsAsync<TaskCanceledException>(() => task);
    }

    [Fact]
    public async Task StepAsync_LinkedToken_FiresWhenWorkflowCancels()
    {
        var harness = CreateHarness();
        var enteredFunc = new TaskCompletionSource();
        CancellationToken stepToken = default;

        var task = harness.Context.StepAsync<int>(async (_, ct) =>
        {
            stepToken = ct;
            enteredFunc.TrySetResult();
            await Task.Delay(Timeout.Infinite, ct);
            return 0;
        }, name: "step");

        await enteredFunc.Task.WaitAsync(TimeSpan.FromSeconds(2));
        harness.Termination.Terminate(TerminationReason.WaitScheduled);

        await Assert.ThrowsAsync<TaskCanceledException>(() => task);
        Assert.True(stepToken.IsCancellationRequested);
    }

    [Fact]
    public async Task StepAsync_UserThrownOCE_IsTreatedAsFailureAndRetried()
    {
        // A user-thrown OperationCanceledException unrelated to our linked token
        // falls through the cancellation when-clause and is funneled through
        // the retry strategy like any other exception.
        var harness = CreateHarness();
        var attempts = 0;

        var ex = await Assert.ThrowsAsync<StepException>(() =>
            harness.Context.StepAsync<int>(async (_, _) =>
            {
                attempts++;
                await Task.CompletedTask;
                throw new OperationCanceledException("user-thrown, unrelated to SDK token");
            }, name: "step"));

        Assert.Equal(1, attempts);
        Assert.Equal(typeof(OperationCanceledException).FullName, ex.ErrorType);
        Assert.Contains(harness.Recorder.Flushed,
            u => u.Action == OperationAction.FAIL && u.Type == OperationTypes.Step);
    }

    [Fact]
    public async Task StepAsync_CancellationViaLinkedToken_DoesNotCheckpointFailOrSucceed()
    {
        var harness = CreateHarness();
        var entered = new TaskCompletionSource();

        var task = harness.Context.StepAsync<int>(async (_, ct) =>
        {
            entered.TrySetResult();
            await Task.Delay(Timeout.Infinite, ct);
            return 0;
        }, name: "step");

        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        harness.Termination.Terminate(TerminationReason.WaitScheduled);

        await Assert.ThrowsAsync<TaskCanceledException>(() => task);

        // No FAIL/SUCCEED checkpoint emitted (only any START fire-and-forget that
        // may have flushed under AtLeastOncePerRetry semantics).
        Assert.DoesNotContain(harness.Recorder.Flushed, u => u.Action == OperationAction.FAIL);
        Assert.DoesNotContain(harness.Recorder.Flushed, u => u.Action == OperationAction.SUCCEED);
    }

    // ── Child context propagation ───────────────────────────────────────

    [Fact]
    public async Task RunInChildContextAsync_LinkedToken_CancelsInnerStep()
    {
        var harness = CreateHarness();
        var entered = new TaskCompletionSource();
        CancellationToken childToken = default;
        CancellationToken stepToken = default;

        var task = harness.Context.RunInChildContextAsync<int>(async (childCtx, ct) =>
        {
            childToken = ct;
            return await childCtx.StepAsync<int>(async (_, stepCt) =>
            {
                stepToken = stepCt;
                entered.TrySetResult();
                await Task.Delay(Timeout.Infinite, stepCt);
                return 0;
            }, name: "inner");
        }, name: "outer");

        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        harness.Termination.Terminate(TerminationReason.WaitScheduled);

        await Assert.ThrowsAsync<TaskCanceledException>(() => task);
        Assert.True(childToken.IsCancellationRequested);
        Assert.True(stepToken.IsCancellationRequested);
    }

    // ── ParallelAsync propagation ───────────────────────────────────────

    [Fact]
    public async Task ParallelAsync_BranchesReceiveLinkedToken_FireOnWorkflowCancel()
    {
        // Each parallel branch runs inside a ChildContextOperation, which links
        // the caller token with WorkflowCancellation.Token. When the workflow
        // terminates, every in-flight branch's token must transition to
        // cancelled so cancellation-aware work inside the branch unwinds.
        var harness = CreateHarness();
        var allEntered = new CountdownEvent(3);
        var tokens = new CancellationToken[3];

        var branches = new Func<IDurableContext, CancellationToken, Task<int>>[3];
        for (var i = 0; i < 3; i++)
        {
            var index = i;
            branches[i] = async (_, ct) =>
            {
                tokens[index] = ct;
                allEntered.Signal();
                await Task.Delay(Timeout.Infinite, ct);
                return index;
            };
        }

        var run = harness.Context.ParallelAsync(branches, name: "fanout");

        Assert.True(allEntered.Wait(TimeSpan.FromSeconds(5)));
        harness.Termination.Terminate(TerminationReason.WaitScheduled);

        // The parallel itself surfaces cancellation (see companion test); here
        // we only care that the per-branch tokens fired.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
        Assert.All(tokens, t => Assert.True(t.IsCancellationRequested));
    }

    [Fact]
    public async Task ParallelAsync_WorkflowCancel_PropagatesAsCancellation_NotBranchFailure()
    {
        // A branch unwinding because the workflow is being torn down is NOT a
        // graceful per-branch failure. Per cancellation.md the OCE propagates
        // and NO parent CONTEXT FAIL is checkpointed — otherwise teardown would
        // freeze a spurious failure into history and diverge on replay.
        var harness = CreateHarness();
        var allEntered = new CountdownEvent(3);

        var branches = new Func<IDurableContext, CancellationToken, Task<int>>[3];
        for (var i = 0; i < 3; i++)
        {
            var index = i;
            branches[i] = async (_, ct) =>
            {
                allEntered.Signal();
                await Task.Delay(Timeout.Infinite, ct);
                return index;
            };
        }

        var run = harness.Context.ParallelAsync(branches, name: "fanout");

        Assert.True(allEntered.Wait(TimeSpan.FromSeconds(5)));
        harness.Termination.Terminate(TerminationReason.WaitScheduled);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);

        // No parent CONTEXT FAIL / SUCCEED — the workflow-shutdown signal owns
        // the outcome, not a synthesized failure-tolerance verdict.
        Assert.DoesNotContain(harness.Recorder.Flushed,
            u => u.Type == OperationTypes.Context
                 && u.SubType == OperationSubTypes.Parallel
                 && (u.Action == OperationAction.FAIL || u.Action == OperationAction.SUCCEED));
    }

    // ── WaitForConditionAsync ───────────────────────────────────────────

    [Fact]
    public async Task WaitForConditionAsync_CheckReceivesLinkedToken()
    {
        var harness = CreateHarness();
        var entered = new TaskCompletionSource();
        CancellationToken seen = default;

        var task = harness.Context.WaitForConditionAsync<int>(async (state, _, ct) =>
        {
            seen = ct;
            entered.TrySetResult();
            await Task.Delay(Timeout.Infinite, ct);
            return state;
        },
        new WaitForConditionConfig<int>
        {
            InitialState = 0,
            WaitStrategy = WaitStrategy.Fixed<int>(TimeSpan.FromSeconds(1)),
        },
        name: "poll");

        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        harness.Termination.Terminate(TerminationReason.WaitScheduled);

        await Assert.ThrowsAsync<TaskCanceledException>(() => task);
        Assert.True(seen.IsCancellationRequested);
    }

    // ── Replay short-circuit ────────────────────────────────────────────

    [Fact]
    public async Task StepAsync_Replay_DoesNotInvokeFunc_EvenWithCancelledToken()
    {
        // Cached SUCCESS replay must short-circuit without calling the user
        // Func, regardless of token state — replay determinism is structural.
        var operationId = OperationIdGenerator.HashOperationId("1");
        var state = new ExecutionState();
        state.LoadFromCheckpoint(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = operationId,
                    Type = OperationTypes.Step,
                    Status = OperationStatuses.Succeeded,
                    Name = "step",
                    StepDetails = new StepDetails { Result = "42" }
                }
            }
        });

        var tm = new TerminationManager();
        var wfc = new WorkflowCancellation(tm);

        // WorkflowCancellation trips its token via an async continuation on
        // TerminationTask (a RunContinuationsAsynchronously TCS), so a single
        // Task.Yield after Terminate() is not a reliable barrier — under load
        // the continuation may not have run yet. Register a callback (fires
        // immediately if the token is already cancelled, so it's race-free) and
        // wait on it deterministically instead of guessing at a yield/delay.
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using (wfc.Token.Register(() => cancelled.TrySetResult()))
        {
            tm.Terminate(TerminationReason.WaitScheduled);  // cancel before invocation
            await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        Assert.True(wfc.Token.IsCancellationRequested);

        var idGen = new OperationIdGenerator();
        var ctx = new DurableContext(state, tm, wfc, idGen, "arn:test", CreateLambdaContext());
        var invoked = false;

        var result = await ctx.StepAsync<int>(async (_, _) =>
        {
            invoked = true;
            await Task.CompletedTask;
            return 99;
        }, name: "step");

        Assert.False(invoked);
        Assert.Equal(42, result);
    }
}
