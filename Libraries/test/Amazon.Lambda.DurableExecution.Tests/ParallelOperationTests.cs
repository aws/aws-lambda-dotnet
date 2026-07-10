// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.DurableExecution.Internal;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.Lambda.TestUtilities;
using Xunit;

namespace Amazon.Lambda.DurableExecution.Tests;

public class ParallelOperationTests
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

    // ──────────────────────────────────────────────────────────────────────
    // Public surface — basic happy paths
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ParallelAsync_FreshExecution_AllBranchesSucceed()
    {
        var (context, recorder, tm, _) = CreateContext();

        var branches = new Func<IDurableContext, CancellationToken, Task<int>>[]
        {
            async (ctx, _) => { await Task.Yield(); return 10; },
            async (ctx, _) => { await Task.Yield(); return 20; },
            async (ctx, _) => { await Task.Yield(); return 30; },
        };

        var result = await context.ParallelAsync(branches, name: "fanout");

        Assert.False(tm.IsTerminated);
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(3, result.SuccessCount);
        Assert.Equal(0, result.FailureCount);
        Assert.Equal(0, result.StartedCount);
        Assert.False(result.HasFailure);
        Assert.Equal(CompletionReason.AllCompleted, result.CompletionReason);
        Assert.Equal(new[] { 10, 20, 30 }, result.GetResults());

        await recorder.Batcher.DrainAsync();

        // Parent CONTEXT START + 3 child CONTEXT STARTs + 3 child CONTEXT SUCCEEDs + Parent CONTEXT SUCCEED
        var contextActions = recorder.Flushed.Where(o => o.Type == "CONTEXT")
            .Select(o => $"{o.SubType}:{o.Action}").ToArray();
        Assert.Equal(8, contextActions.Length);
        Assert.Equal("Parallel:START", contextActions[0]);
        Assert.Equal("Parallel:SUCCEED", contextActions[^1]);
    }

    [Fact]
    public async Task ParallelAsync_PreservesIndexOrder_EvenWhenBranchesCompleteOutOfOrder()
    {
        var (context, _, _, _) = CreateContext();

        var branches = new Func<IDurableContext, CancellationToken, Task<int>>[]
        {
            async (ctx, _) => { await Task.Delay(40); return 1; },
            async (ctx, _) => { await Task.Delay(10); return 2; },
            async (ctx, _) => { await Task.Delay(20); return 3; },
        };

        var result = await context.ParallelAsync(branches);

        Assert.Equal(new[] { 1, 2, 3 }, result.GetResults());
        for (var i = 0; i < result.All.Count; i++)
        {
            Assert.Equal(i, result.All[i].Index);
        }
    }

    [Fact]
    public async Task ParallelAsync_BranchOperationIds_AreDeterministic()
    {
        var (context, recorder, _, _) = CreateContext();

        await context.ParallelAsync(new Func<IDurableContext, CancellationToken, Task<string>>[]
        {
            async (_, _) => { await Task.Yield(); return "a"; },
            async (_, _) => { await Task.Yield(); return "b"; },
        });

        await recorder.Batcher.DrainAsync();

        var parentOpId = IdAt(1);
        var firstBranchId = ChildIdAt(parentOpId, 1);
        var secondBranchId = ChildIdAt(parentOpId, 2);

        // Each branch's CONTEXT START should hit the deterministic child ID.
        var branchStarts = recorder.Flushed
            .Where(o => o.Type == "CONTEXT" && o.SubType == "ParallelBranch" && o.Action == "START")
            .ToArray();
        Assert.Equal(2, branchStarts.Length);
        Assert.Contains(branchStarts, o => o.Id == firstBranchId);
        Assert.Contains(branchStarts, o => o.Id == secondBranchId);
    }

    [Fact]
    public async Task ParallelAsync_NamedBranches_PropagateNameToCheckpointAndItem()
    {
        var (context, recorder, _, _) = CreateContext();

        var branches = new[]
        {
            new DurableBranch<int>("alpha", async (_, _) => { await Task.Yield(); return 1; }),
            new DurableBranch<int>("beta",  async (_, _) => { await Task.Yield(); return 2; }),
        };

        var result = await context.ParallelAsync(branches, name: "fanout");

        Assert.Equal("alpha", result.All[0].Name);
        Assert.Equal("beta",  result.All[1].Name);

        await recorder.Batcher.DrainAsync();

        var branchSucceeds = recorder.Flushed
            .Where(o => o.Type == "CONTEXT" && o.SubType == "ParallelBranch" && o.Action == "SUCCEED")
            .ToArray();
        Assert.Contains(branchSucceeds, o => o.Name == "alpha");
        Assert.Contains(branchSucceeds, o => o.Name == "beta");
    }

    [Fact]
    public async Task ParallelAsync_UnnamedOverload_DefaultsToIndexAsName()
    {
        var (context, _, _, _) = CreateContext();

        var result = await context.ParallelAsync(new Func<IDurableContext, CancellationToken, Task<int>>[]
        {
            async (_, _) => { await Task.Yield(); return 1; },
            async (_, _) => { await Task.Yield(); return 2; },
        });

        Assert.Equal("0", result.All[0].Name);
        Assert.Equal("1", result.All[1].Name);
    }

    [Fact]
    public async Task ParallelAsync_EmptyBranches_ReturnsEmptyResultWithAllCompleted()
    {
        var (context, recorder, _, _) = CreateContext();

        var result = await context.ParallelAsync(Array.Empty<Func<IDurableContext, CancellationToken, Task<int>>>());

        Assert.Equal(0, result.TotalCount);
        Assert.Equal(CompletionReason.AllCompleted, result.CompletionReason);

        await recorder.Batcher.DrainAsync();

        // Even the empty case still flushes parent START + parent SUCCEED.
        var contextActions = recorder.Flushed.Where(o => o.Type == "CONTEXT")
            .Select(o => $"{o.SubType}:{o.Action}").ToArray();
        Assert.Equal(new[] { "Parallel:START", "Parallel:SUCCEED" }, contextActions);
    }

    // ──────────────────────────────────────────────────────────────────────
    // CompletionConfig — failure tolerance
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ParallelAsync_AllSuccessfulDefault_OneFailureResolvesFailureTolerance()
    {
        var (context, _, _, _) = CreateContext();

        // Gate the failing branch's throw on both successful branches having
        // completed. With unlimited concurrency the dispatch loop re-checks
        // ShouldStopDispatchingNow() at the top of every iteration; if the
        // single failure (tolerance 0) registered before branch 2 was
        // dispatched, branch 2 would stay Started and SuccessCount would be 1.
        // Sequencing success-before-failure makes all three branches dispatch
        // and both successes count deterministically, regardless of scheduling.
        var branch0Done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var branch2Done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // The default is fail-fast, but the operation NEVER throws (JS parity) —
        // the failure surfaces on the returned result.
        var result = await context.ParallelAsync(new Func<IDurableContext, CancellationToken, Task<int>>[]
        {
            async (_, _) => { await Task.Yield(); branch0Done.SetResult(); return 1; },
            async (_, _) =>
            {
                await Task.WhenAll(branch0Done.Task, branch2Done.Task);
                throw new InvalidOperationException("branch boom");
            },
            async (_, _) => { await Task.Yield(); branch2Done.SetResult(); return 3; },
        });

        Assert.Equal(CompletionReason.FailureToleranceExceeded, result.CompletionReason);
        Assert.True(result.HasFailure);
        Assert.Equal(1, result.FailureCount);
        Assert.Equal(2, result.SuccessCount);
    }

    [Fact]
    public async Task ParallelAsync_AllCompleted_PartialFailureDoesNotThrow()
    {
        var (context, _, _, _) = CreateContext();

        var result = await context.ParallelAsync(
            new Func<IDurableContext, CancellationToken, Task<int>>[]
            {
                async (_, _) => { await Task.Yield(); return 1; },
                async (_, _) => { await Task.Yield(); throw new InvalidOperationException("oops"); },
                async (_, _) => { await Task.Yield(); return 3; },
            },
            config: new ParallelConfig { CompletionConfig = CompletionConfig.AllCompleted() });

        Assert.True(result.HasFailure);
        Assert.Equal(2, result.SuccessCount);
        Assert.Equal(1, result.FailureCount);
        Assert.Equal(CompletionReason.AllCompleted, result.CompletionReason);
        Assert.Equal(new[] { 1, 3 }, result.GetResults());

        var errors = result.GetErrors();
        Assert.Single(errors);
        Assert.Contains("oops", errors[0].Message);
    }

    [Fact]
    public async Task ParallelAsync_ToleratedFailureCount_AllowsUpToThreshold()
    {
        var (context, _, _, _) = CreateContext();

        // 4 branches, 2 fail; tolerated = 2 (>= failures), so resolves without
        // throwing.
        var result = await context.ParallelAsync(
            new Func<IDurableContext, CancellationToken, Task<int>>[]
            {
                async (_, _) => { await Task.Yield(); return 1; },
                async (_, _) => { await Task.Yield(); throw new InvalidOperationException("fail-1"); },
                async (_, _) => { await Task.Yield(); return 3; },
                async (_, _) => { await Task.Yield(); throw new InvalidOperationException("fail-2"); },
            },
            config: new ParallelConfig
            {
                CompletionConfig = new CompletionConfig { ToleratedFailureCount = 2 }
            });

        Assert.Equal(2, result.FailureCount);
        Assert.Equal(2, result.SuccessCount);
        Assert.Equal(CompletionReason.AllCompleted, result.CompletionReason);
    }

    [Fact]
    public async Task ParallelAsync_ToleratedFailureCount_ExceededResolvesFailureTolerance()
    {
        var (context, _, _, _) = CreateContext();

        var result = await context.ParallelAsync(
            new Func<IDurableContext, CancellationToken, Task<int>>[]
            {
                async (_, _) => { await Task.Yield(); throw new InvalidOperationException("fail-1"); },
                async (_, _) => { await Task.Yield(); throw new InvalidOperationException("fail-2"); },
                async (_, _) => { await Task.Yield(); return 3; },
            },
            config: new ParallelConfig
            {
                CompletionConfig = new CompletionConfig { ToleratedFailureCount = 1 }
            });

        Assert.Equal(CompletionReason.FailureToleranceExceeded, result.CompletionReason);
        Assert.True(result.HasFailure);
    }

    [Fact]
    public async Task ParallelAsync_ToleratedFailurePercentage_ExceededResolvesFailureTolerance()
    {
        var (context, _, _, _) = CreateContext();

        // 4 branches, 3 fail (75%) > 0.5 (50%) → exceeded.
        var result = await context.ParallelAsync(
            new Func<IDurableContext, CancellationToken, Task<int>>[]
            {
                async (_, _) => { await Task.Yield(); throw new InvalidOperationException("f1"); },
                async (_, _) => { await Task.Yield(); throw new InvalidOperationException("f2"); },
                async (_, _) => { await Task.Yield(); throw new InvalidOperationException("f3"); },
                async (_, _) => { await Task.Yield(); return 4; },
            },
            config: new ParallelConfig
            {
                CompletionConfig = new CompletionConfig { ToleratedFailurePercentage = 0.5 }
            });

        Assert.Equal(CompletionReason.FailureToleranceExceeded, result.CompletionReason);
        Assert.True(result.HasFailure);
    }

    [Fact]
    public void CompletionConfig_ToleratedFailurePercentage_OutOfRange_Throws()
    {
        var config = new CompletionConfig();
        Assert.Throws<ArgumentOutOfRangeException>(() => config.ToleratedFailurePercentage = 1.5);
        Assert.Throws<ArgumentOutOfRangeException>(() => config.ToleratedFailurePercentage = -0.1);
        // boundary values are accepted
        config.ToleratedFailurePercentage = 0.0;
        config.ToleratedFailurePercentage = 1.0;
        config.ToleratedFailurePercentage = null;
    }

    [Fact]
    public void CompletionConfig_MinSuccessful_OutOfRange_Throws()
    {
        var config = new CompletionConfig();
        Assert.Throws<ArgumentOutOfRangeException>(() => config.MinSuccessful = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => config.MinSuccessful = -1);
        // 1 is the minimum meaningful value; null clears the criterion.
        config.MinSuccessful = 1;
        config.MinSuccessful = null;
    }

    [Fact]
    public void CompletionConfig_ToleratedFailureCount_Negative_Throws()
    {
        var config = new CompletionConfig();
        Assert.Throws<ArgumentOutOfRangeException>(() => config.ToleratedFailureCount = -1);
        // zero (fail-fast) and positive counts are valid; null clears the criterion.
        config.ToleratedFailureCount = 0;
        config.ToleratedFailureCount = 5;
        config.ToleratedFailureCount = null;
    }

    // ──────────────────────────────────────────────────────────────────────
    // CompletionConfig — first-successful short-circuit
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ParallelAsync_FirstSuccessful_ResolvesAfterFirstSuccess()
    {
        var (context, _, _, _) = CreateContext();

        // MaxConcurrency = 1 so we know the dispatch order is deterministic:
        // branch 0 fires first and succeeds; branches 1 and 2 are never
        // dispatched at all, so they are EXCLUDED from the user-facing All list
        // entirely (they are not Started, don't count in TotalCount/StartedCount).
        var result = await context.ParallelAsync(
            new Func<IDurableContext, CancellationToken, Task<int>>[]
            {
                async (_, _) => { await Task.Yield(); return 1; },
                async (_, _) => { await Task.Yield(); return 2; },
                async (_, _) => { await Task.Yield(); return 3; },
            },
            config: new ParallelConfig
            {
                MaxConcurrency = 1,
                CompletionConfig = CompletionConfig.FirstSuccessful()
            });

        Assert.Equal(CompletionReason.MinSuccessfulReached, result.CompletionReason);
        Assert.Equal(1, result.SuccessCount);
        Assert.Equal(0, result.StartedCount);
        Assert.Equal(0, result.FailureCount);
        Assert.Equal(1, result.TotalCount);

        Assert.Single(result.All);
        Assert.Equal(BatchItemStatus.Succeeded, result.All[0].Status);
    }

    [Fact]
    public async Task ParallelAsync_MinSuccessful_ResolvesWhenTargetReached()
    {
        var (context, _, _, _) = CreateContext();

        var result = await context.ParallelAsync(
            new Func<IDurableContext, CancellationToken, Task<int>>[]
            {
                async (_, _) => { await Task.Yield(); return 1; },
                async (_, _) => { await Task.Yield(); return 2; },
                async (_, _) => { await Task.Yield(); return 3; },
                async (_, _) => { await Task.Yield(); return 4; },
            },
            config: new ParallelConfig
            {
                MaxConcurrency = 1,
                CompletionConfig = new CompletionConfig { MinSuccessful = 2 }
            });

        Assert.Equal(CompletionReason.MinSuccessfulReached, result.CompletionReason);
        Assert.Equal(2, result.SuccessCount);
        // MaxConcurrency = 1: branches 2 and 3 are never dispatched once
        // MinSuccessful = 2 is reached, so they are excluded from All.
        Assert.Equal(0, result.StartedCount);
        Assert.Equal(2, result.TotalCount);
    }

    // ──────────────────────────────────────────────────────────────────────
    // CompletionConfig — short-circuit signals in-flight branches to bail
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ParallelAsync_ShortCircuit_SignalsInFlightBranchesToBail()
    {
        // FirstSuccessful with unlimited concurrency: all three branches are
        // dispatched at once. Branch 0 succeeds only after branches 1 and 2
        // are confirmed in-flight and parked on their cancellation token.
        // Branch 0's success satisfies MinSuccessful=1 and short-circuits the
        // run. The two in-flight branches honor their token, so they must be
        // SIGNALLED to bail — observing OperationCanceledException — and be
        // recorded as Started (they never reached a terminal checkpoint).
        //
        // Before the change nothing signals a dispatched-but-running branch on
        // short-circuit: branches 1 and 2 stay parked on Timeout.Infinite and
        // the run never settles (the 5s WaitAsync guard trips).
        var (context, _, _, _) = CreateContext();

        var branch1Started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var branch2Started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var branch1Cancelled = false;
        var branch2Cancelled = false;

        var branches = new Func<IDurableContext, CancellationToken, Task<int>>[]
        {
            async (_, _) =>
            {
                // Gate success on the siblings being parked, so the
                // short-circuit reliably races against in-flight branches.
                await Task.WhenAll(branch1Started.Task, branch2Started.Task);
                return 1;
            },
            async (_, token) =>
            {
                branch1Started.TrySetResult();
                try { await Task.Delay(Timeout.InfiniteTimeSpan, token); }
                catch (OperationCanceledException) { branch1Cancelled = true; throw; }
                return 2;
            },
            async (_, token) =>
            {
                branch2Started.TrySetResult();
                try { await Task.Delay(Timeout.InfiniteTimeSpan, token); }
                catch (OperationCanceledException) { branch2Cancelled = true; throw; }
                return 3;
            },
        };

        var result = await context.ParallelAsync(
                branches,
                config: new ParallelConfig { CompletionConfig = CompletionConfig.FirstSuccessful() })
            .WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(CompletionReason.MinSuccessfulReached, result.CompletionReason);
        Assert.Equal(1, result.SuccessCount);
        Assert.Equal(0, result.FailureCount);
        Assert.Equal(2, result.StartedCount);

        Assert.Equal(BatchItemStatus.Succeeded, result.All[0].Status);
        Assert.Equal(BatchItemStatus.Started, result.All[1].Status);
        Assert.Equal(BatchItemStatus.Started, result.All[2].Status);

        // The signal actually reached the running branches' tokens.
        Assert.True(branch1Cancelled, "branch 1 was not signalled to bail on short-circuit");
        Assert.True(branch2Cancelled, "branch 2 was not signalled to bail on short-circuit");
    }

    [Fact]
    public async Task ParallelAsync_ShortCircuit_BailedBranchIsNotCountedAsFailure()
    {
        // A branch that bails on the short-circuit signal must NOT be recorded
        // as Failed — otherwise it could spuriously trip a failure-tolerance
        // threshold. Here MinSuccessful=1 with ToleratedFailureCount=0: branch
        // 0 succeeds, the bailed branch must land in Started (not Failed) so
        // the run resolves as MinSuccessfulReached rather than throwing.
        var (context, _, _, _) = CreateContext();

        var branchStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var branches = new Func<IDurableContext, CancellationToken, Task<int>>[]
        {
            async (_, _) => { await branchStarted.Task; return 1; },
            async (_, token) =>
            {
                branchStarted.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return 2;
            },
        };

        var result = await context.ParallelAsync(
                branches,
                config: new ParallelConfig
                {
                    CompletionConfig = new CompletionConfig { MinSuccessful = 1, ToleratedFailureCount = 0 }
                })
            .WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(CompletionReason.MinSuccessfulReached, result.CompletionReason);
        Assert.Equal(1, result.SuccessCount);
        Assert.Equal(0, result.FailureCount);
        Assert.Equal(1, result.StartedCount);
        Assert.Equal(BatchItemStatus.Started, result.All[1].Status);
    }

    // ──────────────────────────────────────────────────────────────────────
    // MaxConcurrency
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ParallelAsync_MaxConcurrency_LimitsInFlight()
    {
        var (context, _, _, _) = CreateContext();

        var inFlight = 0;
        var maxObserved = 0;
        var lockObj = new object();

        var branches = new Func<IDurableContext, CancellationToken, Task<int>>[]
        {
            MakeBranch(),
            MakeBranch(),
            MakeBranch(),
            MakeBranch(),
            MakeBranch(),
        };

        var result = await context.ParallelAsync(branches, config: new ParallelConfig { MaxConcurrency = 2 });

        Assert.Equal(5, result.SuccessCount);
        Assert.True(maxObserved <= 2, $"Observed concurrency {maxObserved} exceeded MaxConcurrency = 2");

        Func<IDurableContext, CancellationToken, Task<int>> MakeBranch()
        {
            return async (_, _) =>
            {
                lock (lockObj)
                {
                    inFlight++;
                    if (inFlight > maxObserved) maxObserved = inFlight;
                }
                await Task.Delay(20);
                lock (lockObj) inFlight--;
                return 1;
            };
        }
    }

    [Fact]
    public void ParallelConfig_MaxConcurrency_OutOfRange_Throws()
    {
        var config = new ParallelConfig();
        Assert.Throws<ArgumentOutOfRangeException>(() => config.MaxConcurrency = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => config.MaxConcurrency = -1);
        config.MaxConcurrency = 1;
        config.MaxConcurrency = null;
    }

    // ──────────────────────────────────────────────────────────────────────
    // NestingType
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ParallelAsync_NestingTypeFlat_SuppressesPerBranchContextOps()
    {
        var (context, recorder, _, _) = CreateContext();

        var result = await context.ParallelAsync(
            new Func<IDurableContext, CancellationToken, Task<int>>[]
            {
                async (_, _) => { await Task.Yield(); return 10; },
                async (_, _) => { await Task.Yield(); return 20; },
                async (_, _) => { await Task.Yield(); return 30; },
            },
            name: "fanout",
            config: new ParallelConfig { NestingType = NestingType.Flat });

        Assert.Equal(new[] { 10, 20, 30 }, result.GetResults());
        Assert.Equal(CompletionReason.AllCompleted, result.CompletionReason);

        await recorder.Batcher.DrainAsync();

        // Parent Parallel CONTEXT ops are still emitted (the parent is never
        // virtual)...
        var parentActions = recorder.Flushed
            .Where(o => o.Type == "CONTEXT" && o.SubType == "Parallel")
            .Select(o => $"{o.Action}").ToArray();
        Assert.Equal(new[] { "START", "SUCCEED" }, parentActions);

        // ...but NO per-branch CONTEXT ops are emitted under Flat.
        var branchOps = recorder.Flushed
            .Where(o => o.Type == "CONTEXT" && o.SubType == "ParallelBranch")
            .ToArray();
        Assert.Empty(branchOps);
    }

    [Fact]
    public async Task ParallelAsync_NestingTypeFlat_InnerOpsReparentToParallelOp()
    {
        var (context, recorder, _, _) = CreateContext();

        await context.ParallelAsync(
            new Func<IDurableContext, CancellationToken, Task<int>>[]
            {
                async (ctx, _) => await ctx.StepAsync(async (_, _) => { await Task.Yield(); return 1; }),
                async (ctx, _) => await ctx.StepAsync(async (_, _) => { await Task.Yield(); return 2; }),
            },
            name: "fanout",
            config: new ParallelConfig { NestingType = NestingType.Flat });

        await recorder.Batcher.DrainAsync();

        var parentOpId = IdAt(1);
        var branch0Id = ChildIdAt(parentOpId, 1);
        var branch1Id = ChildIdAt(parentOpId, 2);

        // Each branch's inner STEP is ID-derived from the branch op id (so the
        // two branches' first steps don't collide)...
        var step0Id = ChildIdAt(branch0Id, 1);
        var step1Id = ChildIdAt(branch1Id, 1);

        // A step emits both START and SUCCEED under the same Id; scope to START
        // so we assert on exactly one record per step.
        var steps = recorder.Flushed
            .Where(o => o.Type == "STEP" && $"{o.Action}" == "START").ToArray();
        var step0 = Assert.Single(steps, o => o.Id == step0Id);
        var step1 = Assert.Single(steps, o => o.Id == step1Id);

        // ...but each inner step re-parents to the PARALLEL op (the nearest
        // non-virtual ancestor), NOT to the virtual branch (which emitted no
        // checkpoint to reference).
        Assert.Equal(parentOpId, step0.ParentId);
        Assert.Equal(parentOpId, step1.ParentId);
    }

    [Fact]
    public async Task ParallelAsync_NestingTypeFlat_PartialFailure_SurfacesInlineErrors()
    {
        var (context, recorder, _, _) = CreateContext();

        var result = await context.ParallelAsync(
            new Func<IDurableContext, CancellationToken, Task<int>>[]
            {
                async (_, _) => { await Task.Yield(); return 1; },
                async (_, _) => { await Task.Yield(); throw new InvalidOperationException("flat boom"); },
                async (_, _) => { await Task.Yield(); return 3; },
            },
            name: "fanout",
            config: new ParallelConfig
            {
                NestingType = NestingType.Flat,
                CompletionConfig = CompletionConfig.AllCompleted()
            });

        Assert.True(result.HasFailure);
        Assert.Equal(2, result.SuccessCount);
        Assert.Equal(1, result.FailureCount);
        Assert.Equal(new[] { 1, 3 }, result.GetResults());
        Assert.Contains("flat boom", result.GetErrors()[0].Message);

        await recorder.Batcher.DrainAsync();

        // The parent SUCCEED payload carries the inline per-unit results/errors;
        // no per-branch FAIL op was emitted.
        Assert.Empty(recorder.Flushed.Where(o =>
            o.Type == "CONTEXT" && o.SubType == "ParallelBranch"));
    }

    [Fact]
    public async Task ParallelAsync_Flat_ResultOverThreshold_StripsInlineResultsAndSetsReplayChildren()
    {
        var (context, recorder, _, _) = CreateContext();

        // Each branch returns a ~200 KB string; the summary with both inline
        // exceeds the 256 KB checkpoint threshold.
        var big = new string('x', 200 * 1024);
        var result = await context.ParallelAsync(
            new Func<IDurableContext, CancellationToken, Task<string>>[]
            {
                async (_, _) => { await Task.Yield(); return big; },
                async (_, _) => { await Task.Yield(); return big; },
            },
            name: "fanout",
            config: new ParallelConfig { NestingType = NestingType.Flat });

        // In-memory result for the current invoke still carries the full values.
        Assert.Equal(2, result.SuccessCount);
        Assert.All(result.GetResults(), r => Assert.Equal(big, r));

        await recorder.Batcher.DrainAsync();

        var parentSucceed = recorder.Flushed.Single(o =>
            o.Type == "CONTEXT" && o.SubType == "Parallel" && o.Action == "SUCCEED");

        // Overflow: ReplayChildren flag set, payload stripped under the threshold.
        Assert.NotNull(parentSucceed.ContextOptions);
        Assert.True(parentSucceed.ContextOptions.ReplayChildren);
        Assert.True(System.Text.Encoding.UTF8.GetByteCount(parentSucceed.Payload)
            <= Amazon.Lambda.DurableExecution.Internal.DurableConstants.MaxOperationCheckpointBytes);
        // Stripped summary keeps statuses but not the big inline results.
        Assert.DoesNotContain(big, parentSucceed.Payload);
        Assert.Contains("SUCCEEDED", parentSucceed.Payload);
    }

    [Fact]
    public async Task ParallelAsync_Flat_ReplayChildren_ReExecutesBodiesWithoutRecheckpointing()
    {
        var parentOpId = IdAt(1);

        // Stripped summary: statuses present, NO inline Result values.
        var summaryJson = """
            {"CompletionReason":"ALL_COMPLETED","Units":[
                {"Index":0,"Name":"0","Status":"SUCCEEDED"},
                {"Index":1,"Name":"1","Status":"SUCCEEDED"}
            ]}
            """;

        var (context, recorder, _, _) = CreateContext(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = parentOpId,
                    Type = OperationTypes.Context,
                    Status = OperationStatuses.Succeeded,
                    SubType = OperationSubTypes.Parallel,
                    Name = "fanout",
                    ContextDetails = new ContextDetails
                    {
                        Result = summaryJson,
                        ReplayChildren = true
                    }
                }
            }
        });

        var executions = 0;
        var result = await context.ParallelAsync(
            new Func<IDurableContext, CancellationToken, Task<int>>[]
            {
                async (_, _) => { Interlocked.Increment(ref executions); await Task.Yield(); return 100; },
                async (_, _) => { Interlocked.Increment(ref executions); await Task.Yield(); return 200; },
            },
            name: "fanout",
            config: new ParallelConfig { NestingType = NestingType.Flat });

        // Bodies re-executed (values recovered), statuses/reason from frozen summary.
        Assert.Equal(2, executions);
        Assert.Equal(new[] { 100, 200 }, result.GetResults());
        Assert.Equal(CompletionReason.AllCompleted, result.CompletionReason);

        await recorder.Batcher.DrainAsync();

        // The parent is already terminal in state — replay must NOT re-emit a
        // parent CONTEXT SUCCEED/FAIL.
        Assert.DoesNotContain(recorder.Flushed, o =>
            o.Type == "CONTEXT" && o.SubType == "Parallel");
    }

    [Fact]
    public async Task ParallelAsync_Flat_ReplayChildren_SkipsStartedUnits_ReExecutesCompletedOnly()
    {
        var parentOpId = IdAt(1);

        // Stripped summary: two units short-circuited the run with MinSuccessful=2
        // (SUCCEEDED, SUCCEEDED), the third was never dispatched (STARTED). On
        // overflow replay only the two completed units re-execute; the started
        // unit's body must NOT run.
        var summaryJson = """
            {"CompletionReason":"MIN_SUCCESSFUL_REACHED","Units":[
                {"Index":0,"Name":"0","Status":"SUCCEEDED"},
                {"Index":1,"Name":"1","Status":"SUCCEEDED"},
                {"Index":2,"Name":"2","Status":"STARTED"}
            ]}
            """;

        var (context, recorder, _, _) = CreateContext(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = parentOpId,
                    Type = OperationTypes.Context,
                    Status = OperationStatuses.Succeeded,
                    SubType = OperationSubTypes.Parallel,
                    Name = "fanout",
                    ContextDetails = new ContextDetails
                    {
                        Result = summaryJson,
                        ReplayChildren = true
                    }
                }
            }
        });

        var executions = 0;
        var startedBodyRan = false;
        var result = await context.ParallelAsync(
            new Func<IDurableContext, CancellationToken, Task<int>>[]
            {
                async (_, _) => { Interlocked.Increment(ref executions); await Task.Yield(); return 100; },
                async (_, _) => { Interlocked.Increment(ref executions); await Task.Yield(); return 200; },
                async (_, _) => { startedBodyRan = true; Interlocked.Increment(ref executions); await Task.Yield(); return 300; },
            },
            name: "fanout",
            config: new ParallelConfig
            {
                NestingType = NestingType.Flat,
                CompletionConfig = new CompletionConfig { MinSuccessful = 2 }
            });

        // Only the two SUCCEEDED unit bodies re-execute; the STARTED unit is skipped.
        Assert.Equal(2, executions);
        Assert.False(startedBodyRan);

        // Per-item statuses come from the frozen summary. The never-dispatched
        // STARTED unit (index 2, no child checkpoint) is excluded from All.
        Assert.Equal(2, result.All.Count);
        Assert.Equal(BatchItemStatus.Succeeded, result.All[0].Status);
        Assert.Equal(BatchItemStatus.Succeeded, result.All[1].Status);

        // Recovered values for the two succeeded units.
        Assert.Equal(new[] { 100, 200 }, result.GetResults());
        Assert.Equal(CompletionReason.MinSuccessfulReached, result.CompletionReason);

        await recorder.Batcher.DrainAsync();
        Assert.DoesNotContain(recorder.Flushed, o =>
            o.Type == "CONTEXT" && o.SubType == "Parallel");
    }

    [Fact]
    public async Task ParallelAsync_Flat_ReplayChildren_ReExecutesFailedUnit_RecoversError()
    {
        var parentOpId = IdAt(1);

        // Stripped summary: one SUCCEEDED, one FAILED. Errors were stripped on
        // overflow, so re-execution recovers them. Tolerated-failure config keeps
        // the run from throwing.
        var summaryJson = """
            {"CompletionReason":"ALL_COMPLETED","Units":[
                {"Index":0,"Name":"0","Status":"SUCCEEDED"},
                {"Index":1,"Name":"1","Status":"FAILED"}
            ]}
            """;

        var (context, recorder, _, _) = CreateContext(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = parentOpId,
                    Type = OperationTypes.Context,
                    Status = OperationStatuses.Succeeded,
                    SubType = OperationSubTypes.Parallel,
                    Name = "fanout",
                    ContextDetails = new ContextDetails
                    {
                        Result = summaryJson,
                        ReplayChildren = true
                    }
                }
            }
        });

        var executions = 0;
        var result = await context.ParallelAsync(
            new Func<IDurableContext, CancellationToken, Task<int>>[]
            {
                async (_, _) => { Interlocked.Increment(ref executions); await Task.Yield(); return 100; },
                async (_, _) => { Interlocked.Increment(ref executions); await Task.Yield(); throw new InvalidOperationException("flat boom"); },
            },
            name: "fanout",
            config: new ParallelConfig
            {
                NestingType = NestingType.Flat,
                CompletionConfig = new CompletionConfig { ToleratedFailureCount = 1 }
            });

        // Both bodies re-execute to recover the value and the error.
        Assert.Equal(2, executions);
        Assert.Equal(BatchItemStatus.Succeeded, result.All[0].Status);
        Assert.Equal(BatchItemStatus.Failed, result.All[1].Status);
        Assert.Equal(100, result.All[0].Result);
        Assert.NotNull(result.All[1].Error);
        Assert.Contains("flat boom", result.All[1].Error!.Message);
        Assert.Equal(CompletionReason.AllCompleted, result.CompletionReason);

        await recorder.Batcher.DrainAsync();
        Assert.DoesNotContain(recorder.Flushed, o =>
            o.Type == "CONTEXT" && o.SubType == "Parallel");
    }

    [Fact]
    public async Task ParallelAsync_NestingTypeFlat_ReplaySucceeded_RebuildsFromInlinePayload()
    {
        var parentOpId = IdAt(1);

        // Flat replay reads per-unit results from the inline summary payload —
        // there are NO per-branch child CONTEXT ops in state.
        var summaryJson = """
            {"CompletionReason":"ALL_COMPLETED","Units":[
                {"Index":0,"Name":"0","Status":"SUCCEEDED","Result":"100"},
                {"Index":1,"Name":"1","Status":"SUCCEEDED","Result":"200"}
            ]}
            """;

        var (context, recorder, _, _) = CreateContext(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = parentOpId,
                    Type = OperationTypes.Context,
                    Status = OperationStatuses.Succeeded,
                    SubType = OperationSubTypes.Parallel,
                    Name = "fanout",
                    ContextDetails = new ContextDetails { Result = summaryJson }
                }
            }
        });

        var executed = false;
        var result = await context.ParallelAsync(
            new Func<IDurableContext, CancellationToken, Task<int>>[]
            {
                async (_, _) => { executed = true; await Task.Yield(); return 999; },
                async (_, _) => { executed = true; await Task.Yield(); return 999; },
            },
            name: "fanout",
            config: new ParallelConfig { NestingType = NestingType.Flat });

        Assert.False(executed);
        Assert.Equal(new[] { 100, 200 }, result.GetResults());
        Assert.Equal(CompletionReason.AllCompleted, result.CompletionReason);

        await recorder.Batcher.DrainAsync();
        Assert.Empty(recorder.Flushed);
    }

    [Fact]
    public async Task ParallelAsync_NestingTypeFlat_ReplayFailed_ResolvesWithInlineError()
    {
        var parentOpId = IdAt(1);

        var summaryJson = """
            {"CompletionReason":"FAILURE_TOLERANCE_EXCEEDED","Units":[
                {"Index":0,"Name":"0","Status":"FAILED","Error":{"ErrorType":"System.InvalidOperationException","ErrorMessage":"flat branch 0 failed"}},
                {"Index":1,"Name":"1","Status":"SUCCEEDED","Result":"200"}
            ]}
            """;

        var (context, _, _, _) = CreateContext(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = parentOpId,
                    Type = OperationTypes.Context,
                    // The parent always checkpoints SUCCEED even on a tolerance
                    // failure — the FAILURE_TOLERANCE_EXCEEDED reason lives in the
                    // payload, and replay throws based on that, not the op status.
                    Status = OperationStatuses.Succeeded,
                    SubType = OperationSubTypes.Parallel,
                    Name = "fanout",
                    ContextDetails = new ContextDetails { Result = summaryJson }
                }
            }
        });

        var result = await context.ParallelAsync(
            new Func<IDurableContext, CancellationToken, Task<int>>[]
            {
                async (_, _) => { await Task.Yield(); return 1; },
                async (_, _) => { await Task.Yield(); return 2; },
            },
            name: "fanout",
            config: new ParallelConfig { NestingType = NestingType.Flat });

        // Replay reconstructs the frozen FailureToleranceExceeded result from the
        // inline payload and returns it — no throw (JS parity).
        Assert.Equal(CompletionReason.FailureToleranceExceeded, result.CompletionReason);
        Assert.Equal(1, result.FailureCount);
        Assert.Contains("flat branch 0 failed", result.GetErrors()[0].Message);
    }

    // ──────────────────────────────────────────────────────────────────────
    // Replay
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ParallelAsync_ReplaySucceeded_RebuildsResultFromCheckpoints()
    {
        var parentOpId = IdAt(1);
        var b0 = ChildIdAt(parentOpId, 1);
        var b1 = ChildIdAt(parentOpId, 2);

        var summaryJson = """
            {"CompletionReason":"ALL_COMPLETED","Units":[
                {"Index":0,"Name":"0","Status":"SUCCEEDED","OperationId":"placeholder0"},
                {"Index":1,"Name":"1","Status":"SUCCEEDED","OperationId":"placeholder1"}
            ]}
            """;

        var (context, recorder, _, _) = CreateContext(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = parentOpId,
                    Type = OperationTypes.Context,
                    Status = OperationStatuses.Succeeded,
                    SubType = OperationSubTypes.Parallel,
                    Name = "fanout",
                    ContextDetails = new ContextDetails { Result = summaryJson }
                },
                new()
                {
                    Id = b0,
                    Type = OperationTypes.Context,
                    Status = OperationStatuses.Succeeded,
                    SubType = OperationSubTypes.ParallelBranch,
                    Name = "0",
                    ContextDetails = new ContextDetails { Result = "100" }
                },
                new()
                {
                    Id = b1,
                    Type = OperationTypes.Context,
                    Status = OperationStatuses.Succeeded,
                    SubType = OperationSubTypes.ParallelBranch,
                    Name = "1",
                    ContextDetails = new ContextDetails { Result = "200" }
                }
            }
        });

        var executed = false;
        var result = await context.ParallelAsync(
            new Func<IDurableContext, CancellationToken, Task<int>>[]
            {
                async (_, _) => { executed = true; await Task.Yield(); return 999; },
                async (_, _) => { executed = true; await Task.Yield(); return 999; },
            },
            name: "fanout");

        Assert.False(executed);
        Assert.Equal(new[] { 100, 200 }, result.GetResults());
        Assert.Equal(CompletionReason.AllCompleted, result.CompletionReason);

        await recorder.Batcher.DrainAsync();
        Assert.Empty(recorder.Flushed);
    }

    [Fact]
    public async Task ParallelAsync_ReplayFailed_ResolvesFailureTolerance()
    {
        var parentOpId = IdAt(1);
        var b0 = ChildIdAt(parentOpId, 1);
        var b1 = ChildIdAt(parentOpId, 2);

        var summaryJson = """
            {"CompletionReason":"FAILURE_TOLERANCE_EXCEEDED","Units":[
                {"Index":0,"Name":"0","Status":"FAILED","OperationId":"placeholder0"},
                {"Index":1,"Name":"1","Status":"FAILED","OperationId":"placeholder1"}
            ]}
            """;

        var (context, _, _, _) = CreateContext(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = parentOpId,
                    Type = OperationTypes.Context,
                    Status = OperationStatuses.Succeeded,
                    SubType = OperationSubTypes.Parallel,
                    Name = "fanout",
                    ContextDetails = new ContextDetails { Result = summaryJson }
                },
                new()
                {
                    Id = b0,
                    Type = OperationTypes.Context,
                    Status = OperationStatuses.Failed,
                    SubType = OperationSubTypes.ParallelBranch,
                    Name = "0",
                    ContextDetails = new ContextDetails
                    {
                        Error = new ErrorObject
                        {
                            ErrorType = "System.InvalidOperationException",
                            ErrorMessage = "branch 0 failed"
                        }
                    }
                },
                new()
                {
                    Id = b1,
                    Type = OperationTypes.Context,
                    Status = OperationStatuses.Failed,
                    SubType = OperationSubTypes.ParallelBranch,
                    Name = "1",
                    ContextDetails = new ContextDetails
                    {
                        Error = new ErrorObject
                        {
                            ErrorType = "System.InvalidOperationException",
                            ErrorMessage = "branch 1 failed"
                        }
                    }
                }
            }
        });

        var result = await context.ParallelAsync(
            new Func<IDurableContext, CancellationToken, Task<int>>[]
            {
                async (_, _) => { await Task.Yield(); return 1; },
                async (_, _) => { await Task.Yield(); return 2; },
            },
            name: "fanout");

        // Replay reconstructs the frozen FailureToleranceExceeded result and
        // returns it — no throw (JS parity).
        Assert.Equal(CompletionReason.FailureToleranceExceeded, result.CompletionReason);
        Assert.Equal(2, result.FailureCount);
        Assert.Contains("branch 0 failed", result.GetErrors()[0].Message);
    }

    [Fact]
    public async Task ParallelAsync_ReplayStarted_ReExecutesBranches()
    {
        var parentOpId = IdAt(1);
        var b0 = ChildIdAt(parentOpId, 1);

        var (context, recorder, _, _) = CreateContext(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = parentOpId,
                    Type = OperationTypes.Context,
                    Status = OperationStatuses.Started,
                    SubType = OperationSubTypes.Parallel,
                    Name = "fanout"
                },
                new()
                {
                    Id = b0,
                    Type = OperationTypes.Context,
                    Status = OperationStatuses.Succeeded,
                    SubType = OperationSubTypes.ParallelBranch,
                    Name = "0",
                    ContextDetails = new ContextDetails { Result = "11" }
                }
            }
        });

        var calls = new int[2];
        var result = await context.ParallelAsync(
            new Func<IDurableContext, CancellationToken, Task<int>>[]
            {
                async (_, _) => { calls[0]++; await Task.Yield(); return 99; },
                async (_, _) => { calls[1]++; await Task.Yield(); return 22; },
            },
            name: "fanout");

        // Branch 0 replays cached value (not re-executed); branch 1 runs fresh.
        Assert.Equal(0, calls[0]);
        Assert.Equal(1, calls[1]);
        Assert.Equal(new[] { 11, 22 }, result.GetResults());

        await recorder.Batcher.DrainAsync();

        // Critical: do NOT re-checkpoint parent CONTEXT START (the original
        // STARTED record is still authoritative).
        var parentStarts = recorder.Flushed.Where(o =>
            o.Type == "CONTEXT" && o.SubType == "Parallel" && o.Action == "START").ToArray();
        Assert.Empty(parentStarts);
    }

    [Fact]
    public async Task ParallelAsync_ReplayUnknownStatus_ThrowsNonDeterministic()
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
                    SubType = OperationSubTypes.Parallel,
                    Name = "fanout"
                }
            }
        });

        await Assert.ThrowsAsync<NonDeterministicExecutionException>(() =>
            context.ParallelAsync(
                new Func<IDurableContext, CancellationToken, Task<int>>[] { async (_, _) => { await Task.Yield(); return 1; } },
                name: "fanout"));
    }

    // ──────────────────────────────────────────────────────────────────────
    // IBatchResult helpers
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task BatchResult_ThrowIfError_ThrowsFirstError()
    {
        var (context, _, _, _) = CreateContext();

        var result = await context.ParallelAsync(
            new Func<IDurableContext, CancellationToken, Task<int>>[]
            {
                async (_, _) => { await Task.Yield(); return 1; },
                async (_, _) => { await Task.Yield(); throw new InvalidOperationException("kaboom"); },
            },
            config: new ParallelConfig { CompletionConfig = CompletionConfig.AllCompleted() });

        var ex = Assert.Throws<ChildContextException>(() => result.ThrowIfError());
        Assert.Contains("kaboom", ex.Message);
    }

    [Fact]
    public async Task BatchResult_GetResults_SkipsFailedAndStartedItems()
    {
        var (context, _, _, _) = CreateContext();

        var result = await context.ParallelAsync(
            new Func<IDurableContext, CancellationToken, Task<int>>[]
            {
                async (_, _) => { await Task.Yield(); return 10; },
                async (_, _) => { await Task.Yield(); throw new InvalidOperationException("ouch"); },
                async (_, _) => { await Task.Yield(); return 30; },
            },
            config: new ParallelConfig { CompletionConfig = CompletionConfig.AllCompleted() });

        Assert.Equal(new[] { 10, 30 }, result.GetResults());
    }

    [Fact]
    public async Task BatchResult_AllSucceededFailedStarted_AreInOriginalIndexOrder()
    {
        var (context, _, _, _) = CreateContext();

        var result = await context.ParallelAsync(
            new Func<IDurableContext, CancellationToken, Task<int>>[]
            {
                async (_, _) => { await Task.Yield(); return 1; },                                       // index 0 succeed
                async (_, _) => { await Task.Yield(); throw new InvalidOperationException("bad-1"); },   // index 1 fail
                async (_, _) => { await Task.Yield(); return 3; },                                       // index 2 succeed
                async (_, _) => { await Task.Yield(); throw new InvalidOperationException("bad-3"); },   // index 3 fail
            },
            config: new ParallelConfig { CompletionConfig = CompletionConfig.AllCompleted() });

        Assert.Equal(new[] { 0, 2 }, result.Succeeded.Select(i => i.Index).ToArray());
        Assert.Equal(new[] { 1, 3 }, result.Failed.Select(i => i.Index).ToArray());
        Assert.Empty(result.Started);
    }

    // ──────────────────────────────────────────────────────────────────────
    // Argument validation
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ParallelAsync_NullBranches_Throws()
    {
        var (context, _, _, _) = CreateContext();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            context.ParallelAsync((IReadOnlyList<Func<IDurableContext, CancellationToken, Task<int>>>)null!));
    }

    [Fact]
    public async Task ParallelAsync_NullBranchInList_Throws()
    {
        var (context, _, _, _) = CreateContext();

        var branches = new Func<IDurableContext, CancellationToken, Task<int>>[]
        {
            async (_, _) => { await Task.Yield(); return 1; },
            null!,
        };

        await Assert.ThrowsAsync<ArgumentException>(() => context.ParallelAsync(branches));
    }

    // ──────────────────────────────────────────────────────────────────────
    // Concurrency / cancellation regressions (Critical 1, Critical 2)
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ParallelAsync_CancelMidDispatch_AllBranchesSettleAndNoObjectDisposed()
    {
        // Regression for orphan-branch bug: dispatch 5 branches with
        // MaxConcurrency=2; cancel parent CancellationToken right after the
        // first batch starts so the dispatcher's semaphore.WaitAsync trips
        // OperationCanceledException mid-loop. With the old code branches in
        // flight at cancellation time would Release on a disposed semaphore
        // and fault as ObjectDisposedException. With the fix the semaphore
        // dispose is gated on Task.WhenAll over inFlight, so every dispatched
        // task settles cleanly first.
        var (context, _, _, _) = CreateContext();

        using var cts = new CancellationTokenSource();
        var dispatchedReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var dispatchedCount = 0;
        var lockObj = new object();
        var capturedExceptions = new List<Exception>();
        var unobservedCount = 0;

        EventHandler<UnobservedTaskExceptionEventArgs> handler = (_, args) =>
        {
            lock (lockObj)
            {
                Interlocked.Increment(ref unobservedCount);
                capturedExceptions.Add(args.Exception);
            }
        };
        TaskScheduler.UnobservedTaskException += handler;

        try
        {
            var branches = new Func<IDurableContext, CancellationToken, Task<int>>[5];
            for (var i = 0; i < 5; i++)
            {
                branches[i] = async (_, _) =>
                {
                    int n;
                    lock (lockObj) n = ++dispatchedCount;
                    if (n == 2) dispatchedReady.TrySetResult();
                    // Hold the branch long enough that cancellation arrives
                    // while we're in flight.
                    try { await Task.Delay(200, cts.Token).ConfigureAwait(false); }
                    catch (OperationCanceledException) { /* cooperatively stop */ }
                    return n;
                };
            }

            var run = context.ParallelAsync(
                branches,
                config: new ParallelConfig
                {
                    MaxConcurrency = 2,
                    CompletionConfig = CompletionConfig.AllCompleted()
                },
                cancellationToken: cts.Token);

            // Wait until 2 branches are running, then cancel — this trips
            // the dispatcher on its next semaphore.WaitAsync call.
            await dispatchedReady.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cts.Cancel();

            // The orchestrator should surface OperationCanceledException
            // cleanly (NOT ObjectDisposedException) once the in-flight
            // branches settle.
            var ex = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
            Assert.IsNotType<ObjectDisposedException>(ex);

            // Force GC + finalizers so any unobserved exceptions surface.
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            Assert.Equal(0, Volatile.Read(ref unobservedCount));
            foreach (var captured in capturedExceptions)
            {
                Assert.IsNotType<ObjectDisposedException>(captured);
            }
        }
        finally
        {
            TaskScheduler.UnobservedTaskException -= handler;
        }
    }

    [Fact]
    public void ExecutionState_ConcurrentTrackReplayAndValidate_NoExceptionsAndConsistent()
    {
        // Regression for ExecutionState race: 16 tasks call TrackReplay /
        // ValidateReplayConsistency / GetOperation concurrently. With the
        // unguarded Dictionary/HashSet collections this would either throw
        // InvalidOperationException (concurrent enumeration) or produce
        // torn reads. Under the lock the ops are serialized and consistent.
        var state = new ExecutionState();
        var ops = new List<Operation>();
        var ids = new List<string>();
        for (var i = 0; i < 50; i++)
        {
            var id = $"op-{i}";
            ids.Add(id);
            ops.Add(new Operation
            {
                Id = id,
                Type = OperationTypes.Context,
                Status = OperationStatuses.Succeeded,
                Name = $"name-{i}"
            });
        }
        state.LoadFromCheckpoint(new InitialExecutionState { Operations = ops });

        var caught = new List<Exception>();
        var caughtLock = new object();
        var tasks = new Task[16];
        for (var t = 0; t < 16; t++)
        {
            var seed = t;
            tasks[t] = Task.Run(() =>
            {
                try
                {
                    var rng = new Random(seed);
                    for (var iter = 0; iter < 200; iter++)
                    {
                        var id = ids[rng.Next(ids.Count)];
                        state.TrackReplay(id);
                        state.ValidateReplayConsistency(id, OperationTypes.Context, $"name-{id.Substring(3)}");
                        _ = state.GetOperation(id);
                        _ = state.HasOperation(id);
                        _ = state.IsReplaying;
                    }
                }
                catch (Exception ex)
                {
                    lock (caughtLock) caught.Add(ex);
                }
            });
        }

        Task.WaitAll(tasks, TimeSpan.FromSeconds(30));
        Assert.Empty(caught);

        // Once every terminal op has been visited, IsReplaying must be false.
        Assert.False(state.IsReplaying);
    }

    // ──────────────────────────────────────────────────────────────────────
    // Replay determinism / failure modes / mixed-status replay
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ParallelAsync_ReplayDeterminism_SameWorkflowProducesSameBranchIds()
    {
        // Run the same workflow shape twice from scratch and assert the
        // branch CONTEXT START IDs are byte-identical. This pins the
        // determinism contract: the n-th branch's hashed ID is a pure
        // function of (root counter position, branch index).
        async Task<string[]> RunOnce()
        {
            var (context, recorder, _, _) = CreateContext();
            await context.ParallelAsync(
                new Func<IDurableContext, CancellationToken, Task<int>>[]
                {
                    async (_, _) => { await Task.Yield(); return 1; },
                    async (_, _) => { await Task.Yield(); return 2; },
                    async (_, _) => { await Task.Yield(); return 3; },
                },
                name: "fanout");
            await recorder.Batcher.DrainAsync();
            return recorder.Flushed
                .Where(o => o.Type == "CONTEXT" && o.SubType == "ParallelBranch" && o.Action == "START")
                .Select(o => o.Id!)
                .OrderBy(s => s)
                .ToArray();
        }

        var run1Ids = await RunOnce();
        var run2Ids = await RunOnce();

        Assert.Equal(3, run1Ids.Length);
        Assert.Equal(run1Ids, run2Ids);
    }

    [Fact]
    public async Task ParallelAsync_FirstSuccessful_AllFail_ResolvesAllCompleted()
    {
        // FirstSuccessful() aliases MinSuccessful=1. Setting any completion
        // criterion opts out of the fail-fast default, so there is no
        // failure-tolerance threshold here. When every branch fails,
        // MinSuccessful is unreachable, so the run completes as AllCompleted with
        // HasFailure=true. The parallel never throws on its own (JS parity);
        // ThrowIfError surfaces the first failure only when the caller asks.
        var (context, _, _, _) = CreateContext();

        var result = await context.ParallelAsync(
            new Func<IDurableContext, CancellationToken, Task<int>>[]
            {
                async (_, _) => { await Task.Yield(); throw new InvalidOperationException("a"); },
                async (_, _) => { await Task.Yield(); throw new InvalidOperationException("b"); },
                async (_, _) => { await Task.Yield(); throw new InvalidOperationException("c"); },
            },
            config: new ParallelConfig { CompletionConfig = CompletionConfig.FirstSuccessful() });

        Assert.Equal(CompletionReason.AllCompleted, result.CompletionReason);
        Assert.Equal(0, result.SuccessCount);
        Assert.Equal(3, result.FailureCount);
        Assert.True(result.HasFailure);

        // Caller-driven aggregation: ThrowIfError surfaces the first failure.
        var ex = Assert.Throws<ChildContextException>(() => result.ThrowIfError());
        Assert.Contains("a", ex.Message);
    }

    [Fact]
    public async Task ParallelAsync_ReplayMixedStatus_PreservesStartedShortCircuited()
    {
        // Parent SUCCEEDED with MinSuccessful short-circuit: branch 0
        // SUCCEEDED, branch 1 SUCCEEDED, branch 2 was never dispatched
        // (still STARTED in the summary, but has NO child checkpoint). Replay
        // must reconstruct the user-facing BatchResult WITHOUT re-executing any
        // branch and must EXCLUDE the never-dispatched STARTED entry (no child
        // op) from All — matching a fresh run.
        var parentOpId = IdAt(1);
        var b0 = ChildIdAt(parentOpId, 1);
        var b1 = ChildIdAt(parentOpId, 2);

        var summaryJson = """
            {"CompletionReason":"MIN_SUCCESSFUL_REACHED","Units":[
                {"Index":0,"Name":"0","Status":"SUCCEEDED"},
                {"Index":1,"Name":"1","Status":"SUCCEEDED"},
                {"Index":2,"Name":"2","Status":"STARTED"}
            ]}
            """;

        var (context, recorder, _, _) = CreateContext(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = parentOpId,
                    Type = OperationTypes.Context,
                    Status = OperationStatuses.Succeeded,
                    SubType = OperationSubTypes.Parallel,
                    Name = "fanout",
                    ContextDetails = new ContextDetails { Result = summaryJson }
                },
                new()
                {
                    Id = b0,
                    Type = OperationTypes.Context,
                    Status = OperationStatuses.Succeeded,
                    SubType = OperationSubTypes.ParallelBranch,
                    Name = "0",
                    ContextDetails = new ContextDetails { Result = "10" }
                },
                new()
                {
                    Id = b1,
                    Type = OperationTypes.Context,
                    Status = OperationStatuses.Succeeded,
                    SubType = OperationSubTypes.ParallelBranch,
                    Name = "1",
                    ContextDetails = new ContextDetails { Result = "20" }
                }
                // Branch 2 has no checkpoint at all — it was never dispatched.
            }
        });

        var calls = 0;
        var result = await context.ParallelAsync(
            new Func<IDurableContext, CancellationToken, Task<int>>[]
            {
                async (_, _) => { calls++; await Task.Yield(); return 999; },
                async (_, _) => { calls++; await Task.Yield(); return 999; },
                async (_, _) => { calls++; await Task.Yield(); return 999; },
            },
            name: "fanout");

        Assert.Equal(0, calls);
        Assert.Equal(CompletionReason.MinSuccessfulReached, result.CompletionReason);
        Assert.Equal(2, result.SuccessCount);
        Assert.Equal(0, result.StartedCount);
        Assert.Equal(2, result.All.Count);
        Assert.Equal(BatchItemStatus.Succeeded, result.All[0].Status);
        Assert.Equal(BatchItemStatus.Succeeded, result.All[1].Status);
        Assert.Equal(new[] { 10, 20 }, result.GetResults());

        await recorder.Batcher.DrainAsync();
        Assert.Empty(recorder.Flushed);
    }

    [Fact]
    public async Task ParallelAsync_ReplayBailedBranch_ReconstructsAsStartedWithoutReExecuting()
    {
        // Determinism contract for the cooperative-bail path: a branch that was
        // SIGNALLED to bail on a live short-circuit dispatched (so it wrote a
        // CONTEXT START checkpoint, status STARTED) but never reached a terminal
        // record. On replay the parent is SUCCEEDED, so the branch must be
        // reconstructed as Started from its START-only checkpoint — NOT
        // re-executed — exactly as it resolved on the original run. This is why
        // signaling (vs. abandoning the task) preserves determinism.
        var parentOpId = IdAt(1);
        var b0 = ChildIdAt(parentOpId, 1);
        var b1 = ChildIdAt(parentOpId, 2);

        var summaryJson = """
            {"CompletionReason":"MIN_SUCCESSFUL_REACHED","Branches":[
                {"Index":0,"Name":"0","Status":"SUCCEEDED"},
                {"Index":1,"Name":"1","Status":"STARTED"}
            ]}
            """;

        var (context, recorder, _, _) = CreateContext(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = parentOpId,
                    Type = OperationTypes.Context,
                    Status = OperationStatuses.Succeeded,
                    SubType = OperationSubTypes.Parallel,
                    Name = "fanout",
                    ContextDetails = new ContextDetails { Result = summaryJson }
                },
                new()
                {
                    Id = b0,
                    Type = OperationTypes.Context,
                    Status = OperationStatuses.Succeeded,
                    SubType = OperationSubTypes.ParallelBranch,
                    Name = "0",
                    ContextDetails = new ContextDetails { Result = "10" }
                },
                new()
                {
                    // Bailed branch: dispatched (START flushed) but no terminal
                    // record — it unwound on the short-circuit signal.
                    Id = b1,
                    Type = OperationTypes.Context,
                    Status = OperationStatuses.Started,
                    SubType = OperationSubTypes.ParallelBranch,
                    Name = "1"
                }
            }
        });

        var calls = 0;
        var result = await context.ParallelAsync(
            new Func<IDurableContext, CancellationToken, Task<int>>[]
            {
                async (_, _) => { calls++; await Task.Yield(); return 999; },
                async (_, _) => { calls++; await Task.Yield(); return 999; },
            },
            name: "fanout");

        Assert.Equal(0, calls);
        Assert.Equal(CompletionReason.MinSuccessfulReached, result.CompletionReason);
        Assert.Equal(1, result.SuccessCount);
        Assert.Equal(0, result.FailureCount);
        Assert.Equal(1, result.StartedCount);
        Assert.Equal(BatchItemStatus.Succeeded, result.All[0].Status);
        Assert.Equal(BatchItemStatus.Started, result.All[1].Status);
        Assert.Equal(new[] { 10 }, result.GetResults());

        await recorder.Batcher.DrainAsync();
        Assert.Empty(recorder.Flushed);
    }

    [Fact]
    public async Task ParallelAsync_ReplayUsesCheckpointedBranchName_NotCurrentName()
    {
        // The checkpointed name is authoritative on replay. For a
        // dispatched-then-bailed branch (STARTED with a per-branch START
        // checkpoint), the reconstructed item is kept in All and its name must
        // come from the parent summary, not from current code.
        var parentOpId = IdAt(1);
        var b0 = ChildIdAt(parentOpId, 1);
        var b1 = ChildIdAt(parentOpId, 2);

        var summaryJson = """
            {"CompletionReason":"MIN_SUCCESSFUL_REACHED","Units":[
                {"Index":0,"Name":"alpha","Status":"SUCCEEDED"},
                {"Index":1,"Name":"beta","Status":"STARTED"}
            ]}
            """;

        var (context, _, _, _) = CreateContext(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = parentOpId,
                    Type = OperationTypes.Context,
                    Status = OperationStatuses.Succeeded,
                    SubType = OperationSubTypes.Parallel,
                    Name = "fanout",
                    ContextDetails = new ContextDetails { Result = summaryJson }
                },
                new()
                {
                    Id = b0,
                    Type = OperationTypes.Context,
                    Status = OperationStatuses.Succeeded,
                    SubType = OperationSubTypes.ParallelBranch,
                    Name = "alpha",
                    ContextDetails = new ContextDetails { Result = "10" }
                },
                new()
                {
                    // Dispatched-then-bailed: has a START checkpoint (STARTED)
                    // so the reconstructed item stays in All.
                    Id = b1,
                    Type = OperationTypes.Context,
                    Status = OperationStatuses.Started,
                    SubType = OperationSubTypes.ParallelBranch,
                    Name = "beta"
                }
            }
        });

        var result = await context.ParallelAsync(
            new[]
            {
                new DurableBranch<int>("alpha", async (_, _) => { await Task.Yield(); return 999; }),
                new DurableBranch<int>("beta",  async (_, _) => { await Task.Yield(); return 999; }),
            },
            name: "fanout");

        Assert.Equal("alpha", result.All[0].Name);
        Assert.Equal("beta", result.All[1].Name);
        Assert.Equal(BatchItemStatus.Started, result.All[1].Status);
    }

    [Fact]
    public async Task ParallelAsync_ReplayWithDriftedBranchName_ThrowsNonDeterministic()
    {
        // A branch name that differs between the checkpoint and the current
        // code indicates the branch set was reordered/renamed between
        // deployments — surface it rather than silently reconstructing.
        var parentOpId = IdAt(1);
        var b0 = ChildIdAt(parentOpId, 1);

        var summaryJson = """
            {"CompletionReason":"ALL_COMPLETED","Units":[
                {"Index":0,"Name":"alpha","Status":"SUCCEEDED"}
            ]}
            """;

        var (context, _, _, _) = CreateContext(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = parentOpId,
                    Type = OperationTypes.Context,
                    Status = OperationStatuses.Succeeded,
                    SubType = OperationSubTypes.Parallel,
                    Name = "fanout",
                    ContextDetails = new ContextDetails { Result = summaryJson }
                },
                new()
                {
                    Id = b0,
                    Type = OperationTypes.Context,
                    Status = OperationStatuses.Succeeded,
                    SubType = OperationSubTypes.ParallelBranch,
                    Name = "alpha",
                    ContextDetails = new ContextDetails { Result = "10" }
                }
            }
        });

        await Assert.ThrowsAsync<NonDeterministicExecutionException>(() =>
            context.ParallelAsync(
                new[]
                {
                    // Renamed from "alpha" → "renamed" since the checkpoint.
                    new DurableBranch<int>("renamed", async (_, _) => { await Task.Yield(); return 999; }),
                },
                name: "fanout"));
    }

}
