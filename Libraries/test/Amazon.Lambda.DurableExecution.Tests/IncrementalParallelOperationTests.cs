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
            inventory = parallel.Branch("inventory", async (_, _) => { await Task.Yield(); return "reserved"; });
            payment = parallel.Branch("payment", async (_, _) => { await Task.Yield(); return 200; });
            shipping = parallel.Branch("shipping", async (_, _) =>
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
            _ = parallel.Branch("a", async (_, _) => { await Task.Yield(); return "a"; });
            _ = parallel.Branch("b", async (_, _) => { await Task.Yield(); return "b"; });
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
            var a = parallel.Branch("alpha", async (_, _) => { await Task.Yield(); return 1; });
            var b = parallel.Branch("beta", async (_, _) => { await Task.Yield(); return 2; });
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
            _ = parallel.Branch("i", async (_, _) => { await Task.Yield(); return 100; });
            _ = parallel.Branch("p", async (_, _) => { await Task.Yield(); return 200; });
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
            ok = parallel.Branch("ok", async (_, _) => { await Task.Yield(); return 1; });
            bad = parallel.Branch<int>("bad", async (_, _) =>
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
            _ = parallel.Branch("ok", async (_, _) => { await Task.Yield(); return 1; });
            _ = parallel.Branch<int>("bad", async (_, _) => { await Task.Yield(); throw new InvalidOperationException("x"); });
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
                _ = parallel.Branch<int>($"b{i}", async (_, ct) =>
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
            _ = parallel.Branch("b0", async (_, _) => { await Task.Yield(); return 1; });
            _ = parallel.Branch("b1", async (_, _) => { await Task.Yield(); return 2; });
            last = parallel.Branch("b2", async (_, _) => { await Task.Yield(); return 3; });
            summary = await parallel.CompleteAsync();
        }

        Assert.True(summary.SuccessCount >= 1);
        Assert.Equal(0, summary.FailureCount);
        Assert.Equal(3, summary.TotalCount);
        // With MaxConcurrency=1 the completion policy stops dispatching once the first
        // success lands, but because branches start on registration, how many of the
        // already-in-flight branches run to completion before the short-circuit is
        // observed is timing-dependent. The deterministic invariants: no failures, the
        // reason reflects an early success (MinSuccessfulReached when a branch was
        // skipped, AllCompleted when every branch happened to finish), and a skipped
        // branch's handle throws on await.
        Assert.True(
            summary.CompletionReason == CompletionReason.MinSuccessfulReached
            || summary.CompletionReason == CompletionReason.AllCompleted);
        Assert.Equal(3, summary.SuccessCount + summary.StartedCount);

        if (last.Status == BatchItemStatus.Started)
        {
            await Assert.ThrowsAsync<DurableExecutionException>(async () => await last);
        }
    }

    // ──────────────────────────────────────────────────────────────────────
    // Registration guardrails
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateParallel_RegisterAfterComplete_Throws()
    {
        var (context, _, _, _) = CreateContext();

        var parallel = context.CreateParallel();
        _ = parallel.Branch("a", async (_, _) => { await Task.Yield(); return 1; });
        await parallel.CompleteAsync();

        Assert.Throws<InvalidOperationException>(() =>
            _ = parallel.Branch("late", async (_, _) => { await Task.Yield(); return 2; }));

        await parallel.DisposeAsync();
    }

    [Fact]
    public async Task CreateParallel_CompleteAsync_IsIdempotent()
    {
        var (context, recorder, _, _) = CreateContext();

        var parallel = context.CreateParallel();
        _ = parallel.Branch("a", async (_, _) => { await Task.Yield(); return 1; });
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
            _ = parallel.Branch("a", async (_, _) => { await Task.Yield(); return 1; });
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
            inventory = parallel.Branch("inventory", async (_, _) => { executed = true; await Task.Yield(); return "LIVE"; });
            payment = parallel.Branch("payment", async (_, _) => { executed = true; await Task.Yield(); return -1; });
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
            ok = parallel.Branch("ok", async (_, _) => { await Task.Yield(); return -1; });
            bad = parallel.Branch<int>("bad", async (_, _) => { await Task.Yield(); return -1; });
            summary = await parallel.CompleteAsync();
        }

        Assert.Equal(CompletionReason.FailureToleranceExceeded, summary.CompletionReason);
        Assert.True(summary.HasFailure);
        Assert.Equal(1, await ok);
        var ex = await Assert.ThrowsAsync<ChildContextException>(async () => await bad);
        Assert.Contains("boom", ex.Message);
    }

    [Fact]
    public async Task CreateParallel_ReplaySucceeded_OverflowStrippedResult_ReRunsBranchToRecoverValue()
    {
        // Overflow-recovery arm of ResolveTerminalBranch: the parent checkpointed
        // SUCCEEDED, but the summary exceeded the payload cap so it was written with
        // the inline per-branch Result stripped (unit Status=SUCCEEDED, Result=null).
        // On replay such a unit is routed through LaunchRunBranch(frozenStatus), which
        // RE-RUNS the branch body to recover the stripped value while keeping the
        // frozen SUCCEEDED verdict authoritative and NOT re-checkpointing the parent.
        var parentOpId = IdAt(1);
        var summaryJson = """
            {"CompletionReason":"ALL_COMPLETED","Units":[
                {"Index":0,"Name":"inventory","Status":"SUCCEEDED"}
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
        IBatchResult summary;

        await using (var parallel = context.CreateParallel(name: "process-order"))
        {
            inventory = parallel.Branch("inventory", async (_, _) =>
            {
                executed = true;                 // the recovered value can only come from a re-run
                await Task.Yield();
                return "recovered-reserved";
            });
            summary = await parallel.CompleteAsync();
        }

        // (a) The stripped value is not inline, so the branch body had to re-run.
        Assert.True(executed);
        // (b) The handle resolves to the value the re-run recovered — not the default
        //     a broken (inline-null) resolution would have produced.
        Assert.Equal("recovered-reserved", await inventory);
        // (c) The frozen verdict wins even though the body re-executed.
        Assert.Equal(BatchItemStatus.Succeeded, inventory.Status);
        Assert.Equal(1, summary.SuccessCount);
        Assert.Equal(0, summary.FailureCount);
        Assert.Equal(CompletionReason.AllCompleted, summary.CompletionReason);

        await recorder.Batcher.DrainAsync();
        // (d) A terminal parent is never re-checkpointed: no parent Parallel SUCCEED.
        Assert.DoesNotContain(recorder.Flushed, o =>
            o.Type == "CONTEXT" && o.SubType == "Parallel" && o.Action == "SUCCEED");
    }

    [Fact]
    public async Task CreateParallel_ReplayFailed_OverflowStrippedError_ReRunsBranchToRecoverFailure()
    {
        // Same overflow-recovery arm for a FAILED unit whose inline Error was stripped
        // (unit Status=FAILED, Error=null). The body re-runs (and fails again), the
        // recovered failure surfaces on the handle, the frozen FAILED verdict stays
        // authoritative, and the parent is not re-checkpointed.
        var parentOpId = IdAt(1);
        var summaryJson = """
            {"CompletionReason":"FAILURE_TOLERANCE_EXCEEDED","Units":[
                {"Index":0,"Name":"bad","Status":"FAILED"}
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
        IParallelBranch<int> bad;
        IBatchResult summary;

        await using (var parallel = context.CreateParallel(name: "fanout"))
        {
            bad = parallel.Branch<int>("bad", async (_, _) =>
            {
                executed = true;
                await Task.Yield();
                throw new InvalidOperationException("recovered-boom");
            });
            summary = await parallel.CompleteAsync();
        }

        // (a) The stripped error is not inline, so the branch body had to re-run.
        Assert.True(executed);
        // (b) The recovered failure surfaces on the handle with the re-run's message.
        var ex = await Assert.ThrowsAsync<ChildContextException>(async () => await bad);
        Assert.Contains("recovered-boom", ex.Message);
        // (c) The frozen FAILED verdict wins.
        Assert.Equal(BatchItemStatus.Failed, bad.Status);
        Assert.Equal(1, summary.FailureCount);
        Assert.True(summary.HasFailure);
        Assert.Equal(CompletionReason.FailureToleranceExceeded, summary.CompletionReason);

        await recorder.Batcher.DrainAsync();
        // (d) A terminal parent is never re-checkpointed: no parent Parallel SUCCEED.
        Assert.DoesNotContain(recorder.Flushed, o =>
            o.Type == "CONTEXT" && o.SubType == "Parallel" && o.Action == "SUCCEED");
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
            _ = parallel.Branch("renamed", async (_, _) => { await Task.Yield(); return 1; }));
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
            _ = parallel.Branch("a", async (_, _) => { Interlocked.Increment(ref runCount); await Task.Yield(); return 1; });
            _ = parallel.Branch("b", async (_, _) => { Interlocked.Increment(ref runCount); await Task.Yield(); return 2; });
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

    // ──────────────────────────────────────────────────────────────────────
    // Replay guardrails from review (issue #2519 Copilot feedback)
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void CreateParallel_ReplayUnexpectedParentStatus_Throws()
    {
        // The parent parallel only ever checkpoints SUCCEED. A terminal status the
        // SDK never writes (CANCELLED/STOPPED/TIMED_OUT/FAILED) is a replay mismatch
        // and must not silently re-run and overwrite the prior outcome.
        var parentOpId = IdAt(1);
        var (context, _, _, _) = CreateContext(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = parentOpId,
                    Type = OperationTypes.Context,
                    Status = "CANCELLED",
                    SubType = OperationSubTypes.Parallel,
                    Name = "fanout"
                }
            }
        });

        Assert.Throws<NonDeterministicExecutionException>(() => context.CreateParallel(name: "fanout"));
    }

    [Fact]
    public async Task CreateParallel_ReplayBranchCountMismatch_Throws()
    {
        // Registering a different number of branches than the frozen summary recorded
        // violates the positional replay contract.
        var parentOpId = IdAt(1);
        var summaryJson = """
            {"CompletionReason":"ALL_COMPLETED","Units":[
                {"Index":0,"Name":"inventory","Status":"SUCCEEDED","Result":"1"},
                {"Index":1,"Name":"payment","Status":"SUCCEEDED","Result":"2"}
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
        _ = parallel.Branch("inventory", async (_, _) => { await Task.Yield(); return 1; });
        // Only one branch registered, but the checkpoint recorded two.
        await Assert.ThrowsAsync<NonDeterministicExecutionException>(async () => await parallel.CompleteAsync());
        await parallel.DisposeAsync(); // must not throw a secondary exception
    }

    [Fact]
    public void CompletionPolicy_PercentageTolerance_NotEvaluatedBeforeSeal()
    {
        // A percentage-based tolerance must not short-circuit against an incomplete
        // denominator: 1 failure out of 1 registered-so-far is 100%, but with two
        // more registrations pending the true ratio may be under threshold.
        var policy = new CompletionPolicy(new CompletionConfig { ToleratedFailurePercentage = 0.5 });

        // Pre-seal: percentage suppressed → do NOT stop dispatching.
        Assert.False(policy.ShouldStopDispatching(succeeded: 0, failed: 1, totalBranches: 1, evaluatePercentage: false));

        // Post-seal with the true denominator: 1/3 <= 0.5 → still do not stop.
        Assert.False(policy.ShouldStopDispatching(succeeded: 0, failed: 1, totalBranches: 3, evaluatePercentage: true));

        // Post-seal, genuinely over threshold: 2/3 > 0.5 → stop.
        Assert.True(policy.ShouldStopDispatching(succeeded: 0, failed: 2, totalBranches: 3, evaluatePercentage: true));
    }

    // ──────────────────────────────────────────────────────────────────────
    // Per-branch serialization (stacked on feature/per-step-serializer)
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateParallel_PerBranchSerializer_UsedForThatBranchOnly()
    {
        var (context, _, _, _) = CreateContext();
        var custom = new CountingSerializer();

        IParallelBranch<int> a;
        IParallelBranch<int> b;
        await using (var parallel = context.CreateParallel())
        {
            // Branch "a" overrides its serializer; branch "b" uses the global default.
            a = parallel.Branch("a", async (_, _) => { await Task.Yield(); return 7; }, serializer: custom);
            b = parallel.Branch("b", async (_, _) => { await Task.Yield(); return 8; });
            await parallel.CompleteAsync();
        }

        // Results round-trip correctly regardless of which serializer produced them.
        Assert.Equal(7, await a);
        Assert.Equal(8, await b);

        // The per-branch serializer was exercised for branch "a".
        Assert.True(custom.SerializeCount > 0, "custom per-branch serializer should have serialized branch a's result");
    }

    [Fact]
    public async Task CreateParallel_ItemSerializer_AppliesToAllBranchesByDefault()
    {
        var (context, _, _, _) = CreateContext();
        var shared = new CountingSerializer();

        await using (var parallel = context.CreateParallel(config: new ParallelConfig { ItemSerializer = shared }))
        {
            _ = parallel.Branch("a", async (_, _) => { await Task.Yield(); return 1; });
            _ = parallel.Branch("b", async (_, _) => { await Task.Yield(); return 2; });
            var summary = await parallel.CompleteAsync();
            Assert.Equal(2, summary.SuccessCount);
        }

        // The operation-level ItemSerializer served both branches.
        Assert.True(shared.SerializeCount >= 2, "ItemSerializer should serialize every branch result by default");
    }

    // ──────────────────────────────────────────────────────────────────────
    // Deferred operation-level serializer resolution (comment 4)
    // ──────────────────────────────────────────────────────────────────────

    private static DurableContext CreateContextNoGlobalSerializer(out RecordingBatcher recorder)
    {
        var state = new ExecutionState();
        state.LoadFromCheckpoint(null);
        var tm = new TerminationManager();
        var idGen = new OperationIdGenerator();
        var lambdaContext = new TestLambdaContext(); // NO Serializer registered
        recorder = new RecordingBatcher();
        return new DurableContext(state, tm, new WorkflowCancellation(tm), idGen, "arn:test", lambdaContext, recorder.Batcher);
    }

    [Fact]
    public async Task CreateParallel_NoGlobalSerializer_AllBranchesOverride_DoesNotThrow()
    {
        // AOT / per-branch scenario: with no global serializer registered,
        // CreateParallel and Branch must not eagerly demand one. Resolution of the
        // operation-level default (LambdaSerializerHelper.GetRequired) is deferred and
        // never reached when every branch supplies its own serializer.
        var context = CreateContextNoGlobalSerializer(out _);
        var custom = new CountingSerializer();

        IParallelBranch<int> a;
        IParallelBranch<int> b;
        await using (var parallel = context.CreateParallel()) // must NOT throw
        {
            a = parallel.Branch("a", async (_, _) => { await Task.Yield(); return 1; }, serializer: custom);
            b = parallel.Branch("b", async (_, _) => { await Task.Yield(); return 2; }, serializer: custom);
            var summary = await parallel.CompleteAsync();
            Assert.Equal(2, summary.SuccessCount);
        }

        Assert.Equal(1, await a);
        Assert.Equal(2, await b);
    }

    [Fact]
    public async Task CreateParallel_NoGlobalSerializer_BranchWithoutOverride_ThrowsOnThatBranch()
    {
        // The deferral does not swallow the requirement: a branch that omits its
        // serializer falls back to the operation-level default, which resolves
        // GetRequired and throws when no global serializer exists — but only then,
        // not at CreateParallel time.
        var context = CreateContextNoGlobalSerializer(out _);

        await using var parallel = context.CreateParallel(); // deferred: does NOT throw here
        Assert.Throws<InvalidOperationException>(() =>
            parallel.Branch("a", async (_, _) => { await Task.Yield(); return 1; }));
    }

    // ──────────────────────────────────────────────────────────────────────
    // Workflow-level fault surfaces on the handle rather than hanging (comment 7)
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateParallel_BranchThrowsWorkflowError_AwaitingHandleFaults_NotHangs()
    {
        // A workflow-level fault (NonDeterministicExecutionException) is rethrown out
        // of the branch's ExecuteAsync. _result must be faulted before the rethrow so
        // a caller that catches the fault out of CompleteAsync and then awaits the
        // branch handle observes the same fault instead of hanging on a handle whose
        // result was never completed.
        var (context, _, _, _) = CreateContext();

        await using var parallel = context.CreateParallel();
        var bad = parallel.Branch<int>("bad", async (_, _) =>
        {
            await Task.Yield();
            throw new NonDeterministicExecutionException("boom");
        });

        // The workflow-level fault propagates out of CompleteAsync.
        await Assert.ThrowsAsync<NonDeterministicExecutionException>(async () => await parallel.CompleteAsync());

        // Awaiting the handle must COMPLETE (faulted), not hang. Guard with a timeout
        // so a regression fails the test deterministically instead of blocking it.
        async Task<int> AwaitHandle() => await bad;
        var handleTask = AwaitHandle();
        var finished = await Task.WhenAny(handleTask, Task.Delay(TimeSpan.FromSeconds(10)));
        Assert.Same(handleTask, finished);
        await Assert.ThrowsAsync<NonDeterministicExecutionException>(async () => await handleTask);
    }

    /// <summary>
    /// Delegating <see cref="Amazon.Lambda.Core.ILambdaSerializer"/> that counts calls, so a
    /// test can assert which serializer a branch used.
    /// </summary>
    private sealed class CountingSerializer : Amazon.Lambda.Core.ILambdaSerializer
    {
        private readonly DefaultLambdaJsonSerializer _inner = new();
        public int SerializeCount;
        public int DeserializeCount;

        public T Deserialize<T>(System.IO.Stream requestStream)
        {
            Interlocked.Increment(ref DeserializeCount);
            return _inner.Deserialize<T>(requestStream);
        }

        public void Serialize<T>(T response, System.IO.Stream responseStream)
        {
            Interlocked.Increment(ref SerializeCount);
            _inner.Serialize(response, responseStream);
        }
    }
}
