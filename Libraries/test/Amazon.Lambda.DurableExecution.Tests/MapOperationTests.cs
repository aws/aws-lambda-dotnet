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
        var lambdaContext = new TestLambdaContext { Serializer = new DefaultLambdaJsonSerializer() };
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
            .Where(o => o.Type == "CONTEXT" && o.SubType == "MapIteration" && o.Action == "START")
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
            config: new MapConfig<string> { ItemNamer = (item, index) => $"Order-{item}" });

        Assert.Equal("Order-order-1", result.All[0].Name);
        Assert.Equal("Order-order-2", result.All[1].Name);

        await recorder.Batcher.DrainAsync();

        var itemSucceeds = recorder.Flushed
            .Where(o => o.Type == "CONTEXT" && o.SubType == "MapIteration" && o.Action == "SUCCEED")
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
    // CompletionConfig — fail-fast default (JS parity); operations never throw
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task MapAsync_DefaultFailFast_PartialFailureResolvesFailureTolerance()
    {
        // Map's default CompletionConfig is AllSuccessful() (fail-fast), matching
        // Parallel and the JS/Python SDKs. A single item failure resolves the map
        // with FailureToleranceExceeded, but the map NEVER throws — the failure is
        // captured on the result. With unlimited concurrency all three items are
        // dispatched before any completes, so two still succeed.
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
        Assert.Equal(CompletionReason.FailureToleranceExceeded, result.CompletionReason);
        Assert.Equal(new[] { 1, 3 }, result.GetResults());

        var errors = result.GetErrors();
        Assert.Single(errors);
        Assert.Contains("oops", errors[0].Message);
    }

    [Fact]
    public async Task MapAsync_AllCompletedOptIn_PartialFailureIsTolerated()
    {
        // AllCompleted() opts out of fail-fast: every item runs and the batch
        // resolves AllCompleted despite the failure. Still no throw.
        var (context, _, _, _) = CreateContext();

        var result = await context.MapAsync(
            new[] { 1, 2, 3 },
            async (ctx, item, index, all, _) =>
            {
                await Task.Yield();
                if (item == 2) throw new InvalidOperationException("oops");
                return item;
            },
            config: new MapConfig<int> { CompletionConfig = CompletionConfig.AllCompleted() });

        Assert.True(result.HasFailure);
        Assert.Equal(2, result.SuccessCount);
        Assert.Equal(1, result.FailureCount);
        Assert.Equal(CompletionReason.AllCompleted, result.CompletionReason);
        Assert.Equal(new[] { 1, 3 }, result.GetResults());
    }

    [Fact]
    public async Task MapAsync_ThrowIfError_ThrowsAfterFailure()
    {
        // The operation never auto-throws; ThrowIfError is the explicit
        // strict-success check the caller opts into.
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
    public async Task MapAsync_ToleratedFailureCount_ExceededResolvesFailureTolerance()
    {
        var (context, _, _, _) = CreateContext();

        var result = await context.MapAsync(
            new[] { 1, 2, 3 },
            async (ctx, item, index, all, _) =>
            {
                await Task.Yield();
                if (item != 3) throw new InvalidOperationException($"fail-{item}");
                return item;
            },
            config: new MapConfig<int>
            {
                CompletionConfig = new CompletionConfig { ToleratedFailureCount = 1 }
            });

        Assert.Equal(CompletionReason.FailureToleranceExceeded, result.CompletionReason);
        Assert.True(result.HasFailure);
        Assert.Equal(2, result.FailureCount);
    }

    // ──────────────────────────────────────────────────────────────────────
    // CompletionConfig — first/min-successful short-circuit
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task MapAsync_FirstSuccessful_ResolvesAfterFirstSuccess()
    {
        var (context, _, _, _) = CreateContext();

        // MaxConcurrency = 1 so dispatch order is deterministic: item 0 fires
        // first and succeeds; items 1 and 2 are never dispatched and are
        // EXCLUDED from the user-facing All list entirely.
        var result = await context.MapAsync(
            new[] { 1, 2, 3 },
            async (ctx, item, index, all, _) => { await Task.Yield(); return item; },
            config: new MapConfig<int>
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
            config: new MapConfig<int> { MaxConcurrency = 2 });

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
            config: new MapConfig<int> { MaxConcurrency = 10 });

        Assert.Equal(3, result.SuccessCount);
        Assert.Equal(new[] { 1, 2, 3 }, result.GetResults());
    }

    [Fact]
    public void MapConfig_MaxConcurrency_OutOfRange_Throws()
    {
        var config = new MapConfig<int>();
        Assert.Throws<ArgumentOutOfRangeException>(() => config.MaxConcurrency = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => config.MaxConcurrency = -1);
        config.MaxConcurrency = 1;
        config.MaxConcurrency = null;
    }

    [Fact]
    public void MapConfig_DefaultCompletionConfig_IsAllSuccessful()
    {
        // Map now defaults to fail-fast (AllSuccessful), matching ParallelConfig
        // and the JS/Python SDKs. AllSuccessful() sets ToleratedFailureCount = 0.
        var config = new MapConfig<int>();
        Assert.Equal(0, config.CompletionConfig.ToleratedFailureCount);
        Assert.Null(config.CompletionConfig.MinSuccessful);
        Assert.Null(config.CompletionConfig.ToleratedFailurePercentage);
    }

    // ──────────────────────────────────────────────────────────────────────
    // NestingType
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task MapAsync_NestingTypeFlat_SuppressesPerItemContextOps()
    {
        var (context, recorder, _, _) = CreateContext();

        var result = await context.MapAsync(
            new[] { 1, 2, 3 },
            async (ctx, item, index, all, _) => { await Task.Yield(); return item * 10; },
            name: "doubler",
            config: new MapConfig<int> { NestingType = NestingType.Flat });

        Assert.Equal(new[] { 10, 20, 30 }, result.GetResults());
        Assert.Equal(CompletionReason.AllCompleted, result.CompletionReason);

        await recorder.Batcher.DrainAsync();

        // Parent Map CONTEXT ops still emitted; no per-item CONTEXT ops under Flat.
        var parentActions = recorder.Flushed
            .Where(o => o.Type == "CONTEXT" && o.SubType == "Map")
            .Select(o => $"{o.Action}").ToArray();
        Assert.Equal(new[] { "START", "SUCCEED" }, parentActions);

        Assert.Empty(recorder.Flushed.Where(o =>
            o.Type == "CONTEXT" && o.SubType == "MapIteration"));
    }

    [Fact]
    public async Task MapAsync_NestingTypeFlat_InnerOpsReparentToMapOp()
    {
        var (context, recorder, _, _) = CreateContext();

        await context.MapAsync(
            new[] { 1, 2 },
            async (ctx, item, index, all, _) =>
                await ctx.StepAsync(async (_, _) => { await Task.Yield(); return item * 10; }),
            name: "doubler",
            config: new MapConfig<int> { NestingType = NestingType.Flat });

        await recorder.Batcher.DrainAsync();

        var parentOpId = IdAt(1);
        var item0Id = ChildIdAt(parentOpId, 1);
        var item1Id = ChildIdAt(parentOpId, 2);
        var step0Id = ChildIdAt(item0Id, 1);
        var step1Id = ChildIdAt(item1Id, 1);

        // A step emits both START and SUCCEED under the same Id; scope to START
        // so we assert on exactly one record per step.
        var steps = recorder.Flushed
            .Where(o => o.Type == "STEP" && $"{o.Action}" == "START").ToArray();
        var step0 = Assert.Single(steps, o => o.Id == step0Id);
        var step1 = Assert.Single(steps, o => o.Id == step1Id);

        // Inner steps re-parent to the MAP op (nearest non-virtual ancestor).
        Assert.Equal(parentOpId, step0.ParentId);
        Assert.Equal(parentOpId, step1.ParentId);
    }

    [Fact]
    public async Task MapAsync_NestingTypeFlat_ReplaySucceeded_RebuildsFromInlinePayload()
    {
        var parentOpId = IdAt(1);

        var summaryJson = """
            {"CompletionReason":"ALL_COMPLETED","Units":[
                {"Index":0,"Name":"0","Status":"SUCCEEDED","Result":"10"},
                {"Index":1,"Name":"1","Status":"SUCCEEDED","Result":"20"}
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
                    Name = "doubler",
                    ContextDetails = new ContextDetails { Result = summaryJson }
                }
            }
        });

        var executed = false;
        var result = await context.MapAsync(
            new[] { 1, 2 },
            async (ctx, item, index, all, _) => { executed = true; await Task.Yield(); return item * 999; },
            name: "doubler",
            config: new MapConfig<int> { NestingType = NestingType.Flat });

        Assert.False(executed);
        Assert.Equal(new[] { 10, 20 }, result.GetResults());
        Assert.Equal(CompletionReason.AllCompleted, result.CompletionReason);

        await recorder.Batcher.DrainAsync();
        Assert.Empty(recorder.Flushed);
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
                    SubType = OperationSubTypes.MapIteration,
                    Name = "0",
                    ContextDetails = new ContextDetails { Result = "100" }
                },
                new()
                {
                    Id = i1,
                    Type = OperationTypes.Context,
                    Status = OperationStatuses.Succeeded,
                    SubType = OperationSubTypes.MapIteration,
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
    public async Task MapAsync_NestedSucceeded_InlinesPerItemResultsOnParentPayload()
    {
        // A Nested map must persist each item's result INLINE on the parent
        // SUCCEED payload (not only on the per-item child checkpoints). The
        // service collapses completed per-item child contexts out of the state
        // returned on a later resume, so the inline copy is the only durable
        // source for reconstructing results on replay (see the 9-17 conformance
        // test: a wait after a successful map).
        var (context, recorder, _, _) = CreateContext();

        var result = await context.MapAsync(
            new[] { "a", "b" },
            async (ctx, item, index, all, _) => { await Task.Yield(); return item.ToUpperInvariant(); },
            name: "then-wait");

        Assert.Equal(new[] { "A", "B" }, result.GetResults());

        await recorder.Batcher.DrainAsync();

        var parentSucceed = Assert.Single(recorder.Flushed.Where(o =>
            o.Type == "CONTEXT" && o.SubType == "Map" && $"{o.Action}" == "SUCCEED"));

        // The parent payload carries the per-item results inline (the serializer
        // stores each item's serialized value on the summary unit).
        var summary = System.Text.Json.JsonSerializer.Deserialize<BatchSummary>(parentSucceed.Payload!);
        Assert.NotNull(summary);
        Assert.Equal("\"A\"", summary!.Units[0].Result);
        Assert.Equal("\"B\"", summary.Units[1].Result);
    }

    [Fact]
    public async Task MapAsync_ReplaySucceeded_RebuildsResultFromInlineSummaryWithoutChildOps()
    {
        // Reproduces the 9-17 conformance scenario: on a post-map resume the
        // service returns only the parent Map op (plus EXECUTION / the trailing
        // wait) — the per-item MapIteration child ops are collapsed away. Results
        // must be recovered from the inline summary alone, WITHOUT re-executing
        // the item callback and WITHOUT the child ops present in state.
        var parentOpId = IdAt(1);

        var summaryJson = """
            {"CompletionReason":"ALL_COMPLETED","Units":[
                {"Index":0,"Name":"0","Status":"SUCCEEDED","Result":"\"A\""},
                {"Index":1,"Name":"1","Status":"SUCCEEDED","Result":"\"B\""}
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
                    Name = "then-wait",
                    ContextDetails = new ContextDetails { Result = summaryJson }
                }
                // NOTE: no per-item child ops — mirrors the pruned resume state.
            }
        });

        var calls = 0;
        var result = await context.MapAsync(
            new[] { "a", "b" },
            async (ctx, item, index, all, _) => { calls++; await Task.Yield(); return "SHOULD-NOT-RUN"; },
            name: "then-wait");

        Assert.Equal(0, calls);
        Assert.Equal(2, result.SuccessCount);
        Assert.Equal(new[] { "A", "B" }, result.GetResults());

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
                    SubType = OperationSubTypes.MapIteration,
                    Name = "0",
                    ContextDetails = new ContextDetails { Result = "10" }
                },
                new()
                {
                    Id = i1,
                    Type = OperationTypes.Context,
                    Status = OperationStatuses.Succeeded,
                    SubType = OperationSubTypes.MapIteration,
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
        // Item 2 was never dispatched (STARTED in the summary, no child
        // checkpoint) so it is excluded from the reconstructed All.
        Assert.Equal(0, result.StartedCount);
        Assert.Equal(2, result.All.Count);
        Assert.Equal(BatchItemStatus.Succeeded, result.All[0].Status);
        Assert.Equal(BatchItemStatus.Succeeded, result.All[1].Status);
        Assert.Equal(new[] { 10, 20 }, result.GetResults());

        await recorder.Batcher.DrainAsync();
        Assert.Empty(recorder.Flushed);
    }

    [Fact]
    public async Task MapAsync_ReplayFailed_RebuildsResultWithoutThrowing()
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
                    SubType = OperationSubTypes.MapIteration,
                    Name = "0",
                    ContextDetails = new ContextDetails
                    {
                        Error = new ErrorObject { ErrorMessage = "stored failure", ErrorType = "System.InvalidOperationException" }
                    }
                }
            }
        });

        var result = await context.MapAsync(
            new[] { 1 },
            async (ctx, item, index, all, _) => { await Task.Yield(); return 999; },
            name: "m");

        // Replay reconstructs the frozen FailureToleranceExceeded result and
        // returns it — the operation never throws (JS parity).
        Assert.Equal(CompletionReason.FailureToleranceExceeded, result.CompletionReason);
        Assert.True(result.HasFailure);
        Assert.Equal(1, result.FailureCount);
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
                    SubType = OperationSubTypes.MapIteration,
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
                config: new MapConfig<int> { ItemNamer = (item, index) => "renamed" }));
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
                .Where(o => o.Type == "CONTEXT" && o.SubType == "MapIteration" && o.Action == "START")
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
