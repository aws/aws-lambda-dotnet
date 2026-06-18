using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.DurableExecution.Internal;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.Lambda.TestUtilities;
using Xunit;

namespace Amazon.Lambda.DurableExecution.Tests;

public class MapOperationTests
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
    public async Task MapAsync_FreshExecution_AllItemsSucceed()
    {
        var (context, recorder, tm, _) = CreateContext();

        var items = new[] { 10, 20, 30 };

        var result = await context.MapAsync(
            items,
            async (ctx, item, index, all, _) => { await Task.Yield(); return item * 2; },
            name: "double_all");

        Assert.False(tm.IsTerminated);
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(3, result.SuccessCount);
        Assert.Equal(0, result.FailureCount);
        Assert.Equal(0, result.StartedCount);
        Assert.False(result.HasFailure);
        Assert.Equal(CompletionReason.AllCompleted, result.CompletionReason);
        Assert.Equal(new[] { 20, 40, 60 }, result.GetResults());

        await recorder.Batcher.DrainAsync();

        // Parent CONTEXT START + 3 item CONTEXT STARTs + 3 item CONTEXT SUCCEEDs + Parent CONTEXT SUCCEED
        var contextActions = recorder.Flushed.Where(o => o.Type == "CONTEXT")
            .Select(o => $"{o.SubType}:{o.Action}").ToArray();
        Assert.Equal(8, contextActions.Length);
        Assert.Equal("Map:START", contextActions[0]);
        Assert.Equal("Map:SUCCEED", contextActions[^1]);
    }

    [Fact]
    public async Task MapAsync_PassesItemIndexAndFullList_ToCallback()
    {
        var (context, _, _, _) = CreateContext();

        var items = new[] { "a", "b", "c" };

        var result = await context.MapAsync(
            items,
            async (ctx, item, index, all, _) =>
            {
                await Task.Yield();
                // Confirm the callback sees the item, its index, and the whole list.
                Assert.Same(items, all);
                Assert.Equal(items[index], item);
                return $"{index}:{item}:{all.Count}";
            });

        Assert.Equal(new[] { "0:a:3", "1:b:3", "2:c:3" }, result.GetResults());
    }

    [Fact]
    public async Task MapAsync_PreservesIndexOrder_EvenWhenItemsCompleteOutOfOrder()
    {
        var (context, _, _, _) = CreateContext();

        var result = await context.MapAsync(
            new[] { 40, 10, 20 },
            async (ctx, delay, index, all, _) => { await Task.Delay(delay); return index + 1; });

        Assert.Equal(new[] { 1, 2, 3 }, result.GetResults());
        for (var i = 0; i < result.All.Count; i++)
        {
            Assert.Equal(i, result.All[i].Index);
        }
    }

    [Fact]
    public async Task MapAsync_ItemOperationIds_AreDeterministic()
    {
        var (context, recorder, _, _) = CreateContext();

        await context.MapAsync(
            new[] { "a", "b" },
            async (ctx, item, index, all, _) => { await Task.Yield(); return item; });

        await recorder.Batcher.DrainAsync();

        var parentOpId = IdAt(1);
        var firstItemId = ChildIdAt(parentOpId, 1);
        var secondItemId = ChildIdAt(parentOpId, 2);

        var itemStarts = recorder.Flushed
            .Where(o => o.Type == "CONTEXT" && o.SubType == "MapItem" && o.Action == "START")
            .ToArray();
        Assert.Equal(2, itemStarts.Length);
        Assert.Contains(itemStarts, o => o.Id == firstItemId);
        Assert.Contains(itemStarts, o => o.Id == secondItemId);
    }

    [Fact]
    public async Task MapAsync_DefaultNaming_UsesIndexAsName()
    {
        var (context, _, _, _) = CreateContext();

        var result = await context.MapAsync(
            new[] { 1, 2 },
            async (ctx, item, index, all, _) => { await Task.Yield(); return item; });

        Assert.Equal("0", result.All[0].Name);
        Assert.Equal("1", result.All[1].Name);
    }

    [Fact]
    public async Task MapAsync_ItemNamer_PropagatesNameToCheckpointAndItem()
    {
        var (context, recorder, _, _) = CreateContext();

        var result = await context.MapAsync(
            new[] { "order-1", "order-2" },
            async (ctx, item, index, all, _) => { await Task.Yield(); return item.Length; },
            name: "process_orders",
            config: new MapConfig { ItemNamer = (item, index) => $"Order-{item}" });

        Assert.Equal("Order-order-1", result.All[0].Name);
        Assert.Equal("Order-order-2", result.All[1].Name);

        await recorder.Batcher.DrainAsync();

        var itemSucceeds = recorder.Flushed
            .Where(o => o.Type == "CONTEXT" && o.SubType == "MapItem" && o.Action == "SUCCEED")
            .ToArray();
        Assert.Contains(itemSucceeds, o => o.Name == "Order-order-1");
        Assert.Contains(itemSucceeds, o => o.Name == "Order-order-2");
    }

    [Fact]
    public async Task MapAsync_EmptyCollection_ReturnsEmptyResultWithAllCompleted()
    {
        var (context, recorder, _, _) = CreateContext();

        var result = await context.MapAsync(
            Array.Empty<int>(),
            async (ctx, item, index, all, _) => { await Task.Yield(); return item; });

        Assert.Equal(0, result.TotalCount);
        Assert.Equal(CompletionReason.AllCompleted, result.CompletionReason);

        await recorder.Batcher.DrainAsync();

        // Even the empty case still flushes parent START + parent SUCCEED.
        var contextActions = recorder.Flushed.Where(o => o.Type == "CONTEXT")
            .Select(o => $"{o.SubType}:{o.Action}").ToArray();
        Assert.Equal(new[] { "Map:START", "Map:SUCCEED" }, contextActions);
    }

    // ──────────────────────────────────────────────────────────────────────
    // CompletionConfig — Map's permissive default vs fail-fast opt-in
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task MapAsync_AllCompletedDefault_PartialFailureDoesNotThrow()
    {
        // Map's default CompletionConfig is AllCompleted() (permissive), unlike
        // Parallel's AllSuccessful(). A single item failure is captured rather
        // than thrown.
        var (context, _, _, _) = CreateContext();

        var result = await context.MapAsync(
            new[] { 1, 2, 3 },
            async (ctx, item, index, all, _) =>
            {
                await Task.Yield();
                if (item == 2) throw new InvalidOperationException("oops");
                return item;
            });

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
    public async Task MapAsync_AllSuccessfulOptIn_OneFailureThrowsMapException()
    {
        var (context, _, _, _) = CreateContext();

        var ex = await Assert.ThrowsAsync<MapException>(() =>
            context.MapAsync(
                new[] { 1, 2, 3 },
                async (ctx, item, index, all, _) =>
                {
                    await Task.Yield();
                    if (item == 2) throw new InvalidOperationException("item boom");
                    return item;
                },
                config: new MapConfig { CompletionConfig = CompletionConfig.AllSuccessful() }));

        Assert.Equal(CompletionReason.FailureToleranceExceeded, ex.CompletionReason);
        Assert.NotNull(ex.Result);
        var typed = Assert.IsAssignableFrom<IBatchResult<int>>(ex.Result);
        Assert.Equal(1, typed.FailureCount);
        Assert.Equal(2, typed.SuccessCount);
    }

    [Fact]
    public async Task MapAsync_ThrowIfError_ThrowsUnderPermissiveDefault()
    {
        // The permissive default does not auto-throw; ThrowIfError is the
        // explicit strict-success check.
        var (context, _, _, _) = CreateContext();

        var result = await context.MapAsync(
            new[] { 1, 2 },
            async (ctx, item, index, all, _) =>
            {
                await Task.Yield();
                if (item == 2) throw new InvalidOperationException("boom");
                return item;
            });

        Assert.True(result.HasFailure);
        var thrown = Assert.ThrowsAny<DurableExecutionException>(() => result.ThrowIfError());
        Assert.Contains("boom", thrown.Message);
    }

    [Fact]
    public async Task MapAsync_ToleratedFailureCount_ExceededThrows()
    {
        var (context, _, _, _) = CreateContext();

        var ex = await Assert.ThrowsAsync<MapException>(() =>
            context.MapAsync(
                new[] { 1, 2, 3 },
                async (ctx, item, index, all, _) =>
                {
                    await Task.Yield();
                    if (item != 3) throw new InvalidOperationException($"fail-{item}");
                    return item;
                },
                config: new MapConfig
                {
                    CompletionConfig = new CompletionConfig { ToleratedFailureCount = 1 }
                }));

        Assert.Equal(CompletionReason.FailureToleranceExceeded, ex.CompletionReason);
    }

    // ──────────────────────────────────────────────────────────────────────
    // CompletionConfig — first/min-successful short-circuit
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task MapAsync_FirstSuccessful_ResolvesAfterFirstSuccess()
    {
        var (context, _, _, _) = CreateContext();

        // MaxConcurrency = 1 so dispatch order is deterministic: item 0 fires
        // first and succeeds; items 1 and 2 are never dispatched and remain
        // BatchItemStatus.Started.
        var result = await context.MapAsync(
            new[] { 1, 2, 3 },
            async (ctx, item, index, all, _) => { await Task.Yield(); return item; },
            config: new MapConfig
            {
                MaxConcurrency = 1,
                CompletionConfig = CompletionConfig.FirstSuccessful()
            });

        Assert.Equal(CompletionReason.MinSuccessfulReached, result.CompletionReason);
        Assert.Equal(1, result.SuccessCount);
        Assert.Equal(2, result.StartedCount);
        Assert.Equal(0, result.FailureCount);
        Assert.Equal(3, result.TotalCount);

        Assert.Equal(BatchItemStatus.Succeeded, result.All[0].Status);
        Assert.Equal(BatchItemStatus.Started,   result.All[1].Status);
        Assert.Equal(BatchItemStatus.Started,   result.All[2].Status);
    }

    // ──────────────────────────────────────────────────────────────────────
    // MaxConcurrency
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task MapAsync_MaxConcurrency_LimitsInFlight()
    {
        var (context, _, _, _) = CreateContext();

        var inFlight = 0;
        var maxObserved = 0;
        var lockObj = new object();

        var result = await context.MapAsync(
            new[] { 1, 2, 3, 4, 5 },
            async (ctx, item, index, all, _) =>
            {
                lock (lockObj)
                {
                    inFlight++;
                    if (inFlight > maxObserved) maxObserved = inFlight;
                }
                await Task.Delay(20);
                lock (lockObj) inFlight--;
                return item;
            },
            config: new MapConfig { MaxConcurrency = 2 });

        Assert.Equal(5, result.SuccessCount);
        Assert.True(maxObserved <= 2, $"Observed concurrency {maxObserved} exceeded MaxConcurrency = 2");
    }

    [Fact]
    public async Task MapAsync_MaxConcurrencyAtLeastItemCount_RunsWithoutSemaphore()
    {
        // MaxConcurrency >= item count exercises the no-semaphore optimization
        // path; behavior must be identical (all items still run).
        var (context, _, _, _) = CreateContext();

        var result = await context.MapAsync(
            new[] { 1, 2, 3 },
            async (ctx, item, index, all, _) => { await Task.Yield(); return item; },
            config: new MapConfig { MaxConcurrency = 10 });

        Assert.Equal(3, result.SuccessCount);
        Assert.Equal(new[] { 1, 2, 3 }, result.GetResults());
    }

    [Fact]
    public void MapConfig_MaxConcurrency_OutOfRange_Throws()
    {
        var config = new MapConfig();
        Assert.Throws<ArgumentOutOfRangeException>(() => config.MaxConcurrency = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => config.MaxConcurrency = -1);
        config.MaxConcurrency = 1;
        config.MaxConcurrency = null;
    }

    [Fact]
    public void MapConfig_DefaultCompletionConfig_IsAllCompleted()
    {
        // Guards the intentional divergence from ParallelConfig (AllSuccessful).
        var config = new MapConfig();
        // AllCompleted() == empty CompletionConfig (no failure thresholds).
        Assert.Null(config.CompletionConfig.ToleratedFailureCount);
        Assert.Null(config.CompletionConfig.MinSuccessful);
        Assert.Null(config.CompletionConfig.ToleratedFailurePercentage);
    }

    // ──────────────────────────────────────────────────────────────────────
    // NestingType
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task MapAsync_NestingTypeFlat_ThrowsNotSupported()
    {
        var (context, _, _, _) = CreateContext();

        await Assert.ThrowsAsync<NotSupportedException>(() =>
            context.MapAsync(
                new[] { 1 },
                async (ctx, item, index, all, _) => { await Task.Yield(); return item; },
                config: new MapConfig { NestingType = NestingType.Flat }));
    }

    // ──────────────────────────────────────────────────────────────────────
    // Argument validation
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task MapAsync_NullItems_Throws()
    {
        var (context, _, _, _) = CreateContext();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            context.MapAsync<int, int>(
                null!,
                async (ctx, item, index, all, _) => { await Task.Yield(); return item; }));
    }

    [Fact]
    public async Task MapAsync_NullFunc_Throws()
    {
        var (context, _, _, _) = CreateContext();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            context.MapAsync(new[] { 1 }, (Func<IDurableContext, int, int, IReadOnlyList<int>, CancellationToken, Task<int>>)null!));
    }

    // ──────────────────────────────────────────────────────────────────────
    // Replay
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task MapAsync_ReplaySucceeded_RebuildsResultFromCheckpoints()
    {
        var parentOpId = IdAt(1);
        var i0 = ChildIdAt(parentOpId, 1);
        var i1 = ChildIdAt(parentOpId, 2);

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
                    SubType = OperationSubTypes.Map,
                    Name = "double_all",
                    ContextDetails = new ContextDetails { Result = summaryJson }
                },
                new()
                {
                    Id = i0,
                    Type = OperationTypes.Context,
                    Status = OperationStatuses.Succeeded,
                    SubType = OperationSubTypes.MapItem,
                    Name = "0",
                    ContextDetails = new ContextDetails { Result = "100" }
                },
                new()
                {
                    Id = i1,
                    Type = OperationTypes.Context,
                    Status = OperationStatuses.Succeeded,
                    SubType = OperationSubTypes.MapItem,
                    Name = "1",
                    ContextDetails = new ContextDetails { Result = "200" }
                }
            }
        });

        var calls = 0;
        var result = await context.MapAsync(
            new[] { 1, 2 },
            async (ctx, item, index, all, _) => { calls++; await Task.Yield(); return 999; },
            name: "double_all");

        // Cached results returned without re-executing the callback.
        Assert.Equal(0, calls);
        Assert.Equal(2, result.SuccessCount);
        Assert.Equal(new[] { 100, 200 }, result.GetResults());

        await recorder.Batcher.DrainAsync();
        Assert.Empty(recorder.Flushed);
    }

    [Fact]
    public async Task MapAsync_ReplayMixedStatus_PreservesStartedShortCircuited()
    {
        var parentOpId = IdAt(1);
        var i0 = ChildIdAt(parentOpId, 1);
        var i1 = ChildIdAt(parentOpId, 2);

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
                    SubType = OperationSubTypes.Map,
                    Name = "m",
                    ContextDetails = new ContextDetails { Result = summaryJson }
                },
                new()
                {
                    Id = i0,
                    Type = OperationTypes.Context,
                    Status = OperationStatuses.Succeeded,
                    SubType = OperationSubTypes.MapItem,
                    Name = "0",
                    ContextDetails = new ContextDetails { Result = "10" }
                },
                new()
                {
                    Id = i1,
                    Type = OperationTypes.Context,
                    Status = OperationStatuses.Succeeded,
                    SubType = OperationSubTypes.MapItem,
                    Name = "1",
                    ContextDetails = new ContextDetails { Result = "20" }
                }
                // Item 2 has no checkpoint at all — it was never dispatched.
            }
        });

        var calls = 0;
        var result = await context.MapAsync(
            new[] { 1, 2, 3 },
            async (ctx, item, index, all, _) => { calls++; await Task.Yield(); return 999; },
            name: "m");

        Assert.Equal(0, calls);
        Assert.Equal(CompletionReason.MinSuccessfulReached, result.CompletionReason);
        Assert.Equal(2, result.SuccessCount);
        Assert.Equal(1, result.StartedCount);
        Assert.Equal(BatchItemStatus.Succeeded, result.All[0].Status);
        Assert.Equal(BatchItemStatus.Succeeded, result.All[1].Status);
        Assert.Equal(BatchItemStatus.Started, result.All[2].Status);
        Assert.Equal(new[] { 10, 20 }, result.GetResults());

        await recorder.Batcher.DrainAsync();
        Assert.Empty(recorder.Flushed);
    }

    [Fact]
    public async Task MapAsync_ReplayFailed_RebuildsResultAndThrows()
    {
        var parentOpId = IdAt(1);
        var i0 = ChildIdAt(parentOpId, 1);

        var summaryJson = """
            {"CompletionReason":"FAILURE_TOLERANCE_EXCEEDED","Units":[
                {"Index":0,"Name":"0","Status":"FAILED"}
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
                    SubType = OperationSubTypes.Map,
                    Name = "m",
                    ContextDetails = new ContextDetails { Result = summaryJson }
                },
                new()
                {
                    Id = i0,
                    Type = OperationTypes.Context,
                    Status = OperationStatuses.Failed,
                    SubType = OperationSubTypes.MapItem,
                    Name = "0",
                    ContextDetails = new ContextDetails
                    {
                        Error = new ErrorObject { ErrorMessage = "stored failure", ErrorType = "System.InvalidOperationException" }
                    }
                }
            }
        });

        var ex = await Assert.ThrowsAsync<MapException>(() =>
            context.MapAsync(
                new[] { 1 },
                async (ctx, item, index, all, _) => { await Task.Yield(); return 999; },
                name: "m"));

        Assert.Equal(CompletionReason.FailureToleranceExceeded, ex.CompletionReason);
        var typed = Assert.IsAssignableFrom<IBatchResult<int>>(ex.Result);
        Assert.Equal(1, typed.FailureCount);
    }

    [Fact]
    public async Task MapAsync_ReplayWithDriftedItemName_ThrowsNonDeterministic()
    {
        // A checkpointed item name that differs from the current ItemNamer output
        // indicates the item set was reordered/renamed between deployments.
        var parentOpId = IdAt(1);
        var i0 = ChildIdAt(parentOpId, 1);

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
                    SubType = OperationSubTypes.Map,
                    Name = "m",
                    ContextDetails = new ContextDetails { Result = summaryJson }
                },
                new()
                {
                    Id = i0,
                    Type = OperationTypes.Context,
                    Status = OperationStatuses.Succeeded,
                    SubType = OperationSubTypes.MapItem,
                    Name = "alpha",
                    ContextDetails = new ContextDetails { Result = "10" }
                }
            }
        });

        await Assert.ThrowsAsync<NonDeterministicExecutionException>(() =>
            context.MapAsync(
                new[] { 1 },
                async (ctx, item, index, all, _) => { await Task.Yield(); return 999; },
                name: "m",
                // Namer now yields "renamed" instead of the checkpointed "alpha".
                config: new MapConfig { ItemNamer = (item, index) => "renamed" }));
    }

    // ──────────────────────────────────────────────────────────────────────
    // Replay determinism
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task MapAsync_TwoFreshRuns_ProduceIdenticalItemOperationIds()
    {
        // Item operation IDs are derived from the parent op ID + index, so two
        // independent fresh runs of the same workflow shape must emit the same
        // child IDs (the foundation of replay correctness).
        string[] IdsFromRun()
        {
            var (context, recorder, _, _) = CreateContext();
            context.MapAsync(
                new[] { 1, 2, 3 },
                async (ctx, item, index, all, _) => { await Task.Yield(); return item; }).GetAwaiter().GetResult();
            recorder.Batcher.DrainAsync().GetAwaiter().GetResult();
            return recorder.Flushed
                .Where(o => o.Type == "CONTEXT" && o.SubType == "MapItem" && o.Action == "START")
                .Select(o => o.Id)
                .OrderBy(id => id)
                .ToArray();
        }

        var first = IdsFromRun();
        var second = IdsFromRun();

        Assert.Equal(3, first.Length);
        Assert.Equal(first, second);
    }
}
