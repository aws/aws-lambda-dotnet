// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.DurableExecution.Internal;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.Lambda.TestUtilities;
using Xunit;

namespace Amazon.Lambda.DurableExecution.Tests;

/// <summary>
/// Tests for the incremental, heterogeneous parallel API
/// (<see cref="IDurableContext.CreateParallel"/> / <see cref="IDurableParallel"/>).
/// The checkpoint shape mirrors the batch <c>ParallelOperation&lt;T&gt;</c>, so these
/// reuse the same <c>IdAt</c>/<c>ChildIdAt</c>/<c>CreateContext</c> harness as
/// <see cref="ParallelOperationTests"/>.
/// </summary>
public class IncrementalParallelOperationTests
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

    public sealed class Money
    {
        public string Currency { get; set; } = "";
        public int Amount { get; set; }
    }

    // ──────────────────────────────────────────────────────────────────────
    // Fresh execution — happy paths
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateParallel_FreshExecution_HeterogeneousBranches_ResolveTypedResults()
    {
        var (context, recorder, tm, _) = CreateContext();

        IParallelBranch<string> inventory;
        IParallelBranch<int> payment;
        IParallelBranch<Money> shipping;

        await using (var parallel = context.CreateParallel(name: "process-order"))
        {
            inventory = parallel.BranchAsync("inventory", async (_, _) => { await Task.Yield(); return "reserved"; });
            payment = parallel.BranchAsync("payment", async (_, _) => { await Task.Yield(); return 200; });
            shipping = parallel.BranchAsync("shipping", async (_, _) =>
            {
                await Task.Yield();
                return new Money { Currency = "USD", Amount = 4200 };
            });

            var summary = await parallel.CompleteAsync();

            Assert.False(tm.IsTerminated);
            Assert.Equal(3, summary.TotalCount);
            Assert.Equal(3, summary.SuccessCount);
            Assert.Equal(0, summary.FailureCount);
            Assert.False(summary.HasFailure);
            Assert.Equal(CompletionReason.AllCompleted, summary.CompletionReason);
        }

        // Each branch handle yields its own concrete type — no shared T, no casts.
        Assert.Equal("reserved", await inventory);
        Assert.Equal(200, await payment);
        var ship = await shipping;
        Assert.Equal("USD", ship.Currency);
        Assert.Equal(4200, ship.Amount);

        Assert.Equal(BatchItemStatus.Succeeded, inventory.Status);
        Assert.Equal(0, inventory.Index);
        Assert.Equal(2, shipping.Index);

        await recorder.Batcher.DrainAsync();
        var contextActions = recorder.Flushed.Where(o => o.Type == "CONTEXT")
            .Select(o => $"{o.SubType}:{o.Action}").ToArray();
        // Parent START + 3 child STARTs + 3 child SUCCEEDs + parent SUCCEED
        Assert.Equal(8, contextActions.Length);
        Assert.Equal("Parallel:START", contextActions[0]);
        Assert.Equal("Parallel:SUCCEED", contextActions[^1]);
    }

    [Fact]
    public async Task CreateParallel_BranchOperationIds_AreDeterministic()
    {
        var (context, recorder, _, _) = CreateContext();

        await using (var parallel = context.CreateParallel())
        {
            _ = parallel.BranchAsync("a", async (_, _) => { await Task.Yield(); return "a"; });
            _ = parallel.BranchAsync("b", async (_, _) => { await Task.Yield(); return "b"; });
            await parallel.CompleteAsync();
        }

        await recorder.Batcher.DrainAsync();

        var parentOpId = IdAt(1);
        var branchStarts = recorder.Flushed
            .Where(o => o.Type == "CONTEXT" && o.SubType == "ParallelBranch" && o.Action == "START")
            .ToArray();
        Assert.Equal(2, branchStarts.Length);
        Assert.Contains(branchStarts, o => o.Id == ChildIdAt(parentOpId, 1));
        Assert.Contains(branchStarts, o => o.Id == ChildIdAt(parentOpId, 2));
    }

    [Fact]
    public async Task CreateParallel_EmptyOperation_FlushesStartAndSucceed()
    {
        var (context, recorder, _, _) = CreateContext();

        IBatchResult summary;
        await using (var parallel = context.CreateParallel())
        {
            summary = await parallel.CompleteAsync();
        }

        Assert.Equal(0, summary.TotalCount);
        Assert.Equal(CompletionReason.AllCompleted, summary.CompletionReason);

        await recorder.Batcher.DrainAsync();
        var contextActions = recorder.Flushed.Where(o => o.Type == "CONTEXT")
            .Select(o => $"{o.SubType}:{o.Action}").ToArray();
        Assert.Equal(new[] { "Parallel:START", "Parallel:SUCCEED" }, contextActions);
    }

    [Fact]
    public async Task CreateParallel_NamesPropagateToCheckpointAndHandle()
    {
        var (context, recorder, _, _) = CreateContext();

        await using (var parallel = context.CreateParallel(name: "fanout"))
        {
            var a = parallel.BranchAsync("alpha", async (_, _) => { await Task.Yield(); return 1; });
            var b = parallel.BranchAsync("beta", async (_, _) => { await Task.Yield(); return 2; });
            await parallel.CompleteAsync();
            Assert.Equal("alpha", a.Name);
            Assert.Equal("beta", b.Name);
        }

        await recorder.Batcher.DrainAsync();
        var branchSucceeds = recorder.Flushed
            .Where(o => o.Type == "CONTEXT" && o.SubType == "ParallelBranch" && o.Action == "SUCCEED")
            .ToArray();
        Assert.Contains(branchSucceeds, o => o.Name == "alpha");
        Assert.Contains(branchSucceeds, o => o.Name == "beta");
    }

    [Fact]
    public async Task CreateParallel_NestedSucceeded_InlinesPerBranchResultsOnParentPayload()
    {
        var (context, recorder, _, _) = CreateContext();

        await using (var parallel = context.CreateParallel(name: "fanout"))
        {
            _ = parallel.BranchAsync("i", async (_, _) => { await Task.Yield(); return 100; });
            _ = parallel.BranchAsync("p", async (_, _) => { await Task.Yield(); return 200; });
            await parallel.CompleteAsync();
        }

        await recorder.Batcher.DrainAsync();
        var parentSucceed = Assert.Single(recorder.Flushed.Where(o =>
            o.Type == "CONTEXT" && o.SubType == "Parallel" && $"{o.Action}" == "SUCCEED"));
        var summary = System.Text.Json.JsonSerializer.Deserialize<BatchSummary>(parentSucceed.Payload!);
        Assert.NotNull(summary);
        Assert.Equal("100", summary!.Units[0].Result);
        Assert.Equal("200", summary.Units[1].Result);
    }

    // ──────────────────────────────────────────────────────────────────────
    // Failure handling
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateParallel_DefaultFailFast_BranchFailure_SurfacesOnResultAndHandle()
    {
        var (context, _, _, _) = CreateContext();

        IParallelBranch<int> ok;
        IParallelBranch<int> bad;
        IBatchResult summary;

        await using (var parallel = context.CreateParallel())
        {
            ok = parallel.BranchAsync("ok", async (_, _) => { await Task.Yield(); return 1; });
            bad = parallel.BranchAsync<int>("bad", async (_, _) =>
            {
                await Task.Yield();
                throw new InvalidOperationException("branch boom");
            });
            // Never throws on per-branch failure.
            summary = await parallel.CompleteAsync();
        }

        Assert.True(summary.HasFailure);
        Assert.Equal(CompletionReason.FailureToleranceExceeded, summary.CompletionReason);
        Assert.Equal(1, summary.FailureCount);

        Assert.Equal(1, await ok);
        Assert.Equal(BatchItemStatus.Failed, bad.Status);
        var ex = await Assert.ThrowsAsync<ChildContextException>(async () => await bad);
        Assert.Contains("branch boom", ex.Message);
    }

    [Fact]
    public async Task CreateParallel_AllCompleted_PartialFailureDoesNotExceedTolerance()
    {
        var (context, _, _, _) = CreateContext();

        IBatchResult summary;
        await using (var parallel = context.CreateParallel(
            config: new ParallelConfig { CompletionConfig = CompletionConfig.AllCompleted() }))
        {
            _ = parallel.BranchAsync("ok", async (_, _) => { await Task.Yield(); return 1; });
            _ = parallel.BranchAsync<int>("bad", async (_, _) => { await Task.Yield(); throw new InvalidOperationException("x"); });
            summary = await parallel.CompleteAsync();
        }

        Assert.Equal(CompletionReason.AllCompleted, summary.CompletionReason);
        Assert.Equal(1, summary.SuccessCount);
        Assert.Equal(1, summary.FailureCount);
        Assert.True(summary.HasFailure);
    }

    // ──────────────────────────────────────────────────────────────────────
    // MaxConcurrency + completion short-circuit
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateParallel_MaxConcurrency_LimitsInFlight()
    {
        var (context, _, _, _) = CreateContext();

        var inFlight = 0;
        var maxObserved = 0;
        var gate = new object();

        await using (var parallel = context.CreateParallel(config: new ParallelConfig { MaxConcurrency = 2 }))
        {
            for (var i = 0; i < 6; i++)
            {
                _ = parallel.BranchAsync<int>($"b{i}", async (_, ct) =>
                {
                    lock (gate) { inFlight++; maxObserved = Math.Max(maxObserved, inFlight); }
                    await Task.Delay(20, ct);
                    lock (gate) { inFlight--; }
                    return 1;
                });
            }
            await parallel.CompleteAsync();
        }

        Assert.True(maxObserved <= 2, $"Observed concurrency {maxObserved} exceeded MaxConcurrency = 2");
    }

    [Fact]
    public async Task CreateParallel_FirstSuccessful_WithMaxConcurrency1_SkipsTrailingBranches()
    {
        var (context, _, _, _) = CreateContext();

        IParallelBranch<int> last;
        IBatchResult summary;

        await using (var parallel = context.CreateParallel(config: new ParallelConfig
        {
            MaxConcurrency = 1,
            CompletionConfig = CompletionConfig.FirstSuccessful()
        }))
        {
            _ = parallel.BranchAsync("b0", async (_, _) => { await Task.Yield(); return 1; });
            _ = parallel.BranchAsync("b1", async (_, _) => { await Task.Yield(); return 2; });
            last = parallel.BranchAsync("b2", async (_, _) => { await Task.Yield(); return 3; });
            summary = await parallel.CompleteAsync();
        }

        Assert.Equal(CompletionReason.MinSuccessfulReached, summary.CompletionReason);
        Assert.True(summary.SuccessCount >= 1);
        Assert.True(summary.StartedCount >= 1);
        Assert.Equal(3, summary.TotalCount);

        // The trailing branch never ran; awaiting it surfaces a skip error.
        Assert.Equal(BatchItemStatus.Started, last.Status);
        await Assert.ThrowsAsync<DurableExecutionException>(async () => await last);
    }

    // ──────────────────────────────────────────────────────────────────────
    // Registration guardrails
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateParallel_RegisterAfterComplete_Throws()
    {
        var (context, _, _, _) = CreateContext();

        var parallel = context.CreateParallel();
        _ = parallel.BranchAsync("a", async (_, _) => { await Task.Yield(); return 1; });
        await parallel.CompleteAsync();

        Assert.Throws<InvalidOperationException>(() =>
            _ = parallel.BranchAsync("late", async (_, _) => { await Task.Yield(); return 2; }));

        await parallel.DisposeAsync();
    }

    [Fact]
    public async Task CreateParallel_CompleteAsync_IsIdempotent()
    {
        var (context, recorder, _, _) = CreateContext();

        var parallel = context.CreateParallel();
        _ = parallel.BranchAsync("a", async (_, _) => { await Task.Yield(); return 1; });
        var first = await parallel.CompleteAsync();
        var second = await parallel.CompleteAsync();
        Assert.Same(first, second);
        await parallel.DisposeAsync();

        await recorder.Batcher.DrainAsync();
        // Exactly one parent SUCCEED despite two CompleteAsync calls + dispose.
        Assert.Single(recorder.Flushed.Where(o =>
            o.Type == "CONTEXT" && o.SubType == "Parallel" && o.Action == "SUCCEED"));
    }

    [Fact]
    public async Task CreateParallel_DisposeWithoutComplete_StillCheckpointsParent()
    {
        var (context, recorder, _, _) = CreateContext();

        await using (var parallel = context.CreateParallel(name: "auto"))
        {
            _ = parallel.BranchAsync("a", async (_, _) => { await Task.Yield(); return 1; });
            // No explicit CompleteAsync — DisposeAsync must seal + checkpoint.
        }

        await recorder.Batcher.DrainAsync();
        Assert.Single(recorder.Flushed.Where(o =>
            o.Type == "CONTEXT" && o.SubType == "Parallel" && o.Action == "SUCCEED"));
    }

    // ──────────────────────────────────────────────────────────────────────
    // Replay — terminal parent reconstructs without re-running branches
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateParallel_ReplaySucceeded_RebuildsFromInlineSummary_WithoutRerunning()
    {
        var parentOpId = IdAt(1);
        var summaryJson = """
            {"CompletionReason":"ALL_COMPLETED","Units":[
                {"Index":0,"Name":"inventory","Status":"SUCCEEDED","Result":"\"reserved\""},
                {"Index":1,"Name":"payment","Status":"SUCCEEDED","Result":"200"}
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
                    Name = "process-order",
                    ContextDetails = new ContextDetails { Result = summaryJson }
                }
            }
        });

        var executed = false;
        IParallelBranch<string> inventory;
        IParallelBranch<int> payment;
        IBatchResult summary;

        await using (var parallel = context.CreateParallel(name: "process-order"))
        {
            inventory = parallel.BranchAsync("inventory", async (_, _) => { executed = true; await Task.Yield(); return "LIVE"; });
            payment = parallel.BranchAsync("payment", async (_, _) => { executed = true; await Task.Yield(); return -1; });
            summary = await parallel.CompleteAsync();
        }

        Assert.False(executed);
        Assert.Equal("reserved", await inventory);
        Assert.Equal(200, await payment);
        Assert.Equal(2, summary.SuccessCount);
        Assert.Equal(CompletionReason.AllCompleted, summary.CompletionReason);

        await recorder.Batcher.DrainAsync();
        Assert.Empty(recorder.Flushed); // terminal parent → no re-checkpoint
    }

    [Fact]
    public async Task CreateParallel_ReplaySucceeded_FailedBranch_AwaitRethrows()
    {
        var parentOpId = IdAt(1);
        var summaryJson = """
            {"CompletionReason":"FAILURE_TOLERANCE_EXCEEDED","Units":[
                {"Index":0,"Name":"ok","Status":"SUCCEEDED","Result":"1"},
                {"Index":1,"Name":"bad","Status":"FAILED","Error":{"ErrorType":"System.InvalidOperationException","ErrorMessage":"boom"}}
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
                }
            }
        });

        IParallelBranch<int> ok;
        IParallelBranch<int> bad;
        IBatchResult summary;

        await using (var parallel = context.CreateParallel(name: "fanout"))
        {
            ok = parallel.BranchAsync("ok", async (_, _) => { await Task.Yield(); return -1; });
            bad = parallel.BranchAsync<int>("bad", async (_, _) => { await Task.Yield(); return -1; });
            summary = await parallel.CompleteAsync();
        }

        Assert.Equal(CompletionReason.FailureToleranceExceeded, summary.CompletionReason);
        Assert.True(summary.HasFailure);
        Assert.Equal(1, await ok);
        var ex = await Assert.ThrowsAsync<ChildContextException>(async () => await bad);
        Assert.Contains("boom", ex.Message);
    }

    [Fact]
    public async Task CreateParallel_ReplayNameDrift_Throws()
    {
        var parentOpId = IdAt(1);
        var summaryJson = """
            {"CompletionReason":"ALL_COMPLETED","Units":[
                {"Index":0,"Name":"inventory","Status":"SUCCEEDED","Result":"1"}
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
                }
            }
        });

        var parallel = context.CreateParallel(name: "fanout");
        // Registered a branch whose name drifted from the checkpointed "inventory".
        Assert.Throws<NonDeterministicExecutionException>(() =>
            _ = parallel.BranchAsync("renamed", async (_, _) => { await Task.Yield(); return 1; }));
        await parallel.DisposeAsync();
    }

    // ──────────────────────────────────────────────────────────────────────
    // Replay — STARTED parent re-runs branches (children replay from own checkpoints)
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateParallel_ReplayStartedParent_ReRunsBranches_AndCheckpointsSucceed()
    {
        var parentOpId = IdAt(1);
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
                }
            }
        });

        var runCount = 0;
        IBatchResult summary;
        await using (var parallel = context.CreateParallel(name: "fanout"))
        {
            _ = parallel.BranchAsync("a", async (_, _) => { Interlocked.Increment(ref runCount); await Task.Yield(); return 1; });
            _ = parallel.BranchAsync("b", async (_, _) => { Interlocked.Increment(ref runCount); await Task.Yield(); return 2; });
            summary = await parallel.CompleteAsync();
        }

        Assert.Equal(2, runCount); // children re-run (no terminal checkpoints for them)
        Assert.Equal(2, summary.SuccessCount);

        await recorder.Batcher.DrainAsync();
        // STARTED parent is not re-emitted, but the terminal SUCCEED is written now.
        var parentActions = recorder.Flushed
            .Where(o => o.Type == "CONTEXT" && o.SubType == "Parallel")
            .Select(o => $"{o.Action}").ToArray();
        Assert.DoesNotContain("START", parentActions);
        Assert.Contains("SUCCEED", parentActions);
    }
}
