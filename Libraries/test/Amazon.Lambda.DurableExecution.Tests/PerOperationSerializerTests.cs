// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution.Internal;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.Lambda.TestUtilities;
using Xunit;

namespace Amazon.Lambda.DurableExecution.Tests;

/// <summary>
/// Verifies the per-operation serializer override (Phase 1): each single-result
/// operation config exposes an optional <see cref="ILambdaSerializer"/> that, when set,
/// is used for that operation's payload (serialize and/or replay-deserialize) instead of
/// the globally-registered serializer on <see cref="ILambdaContext.Serializer"/>. When
/// unset, the global serializer is used.
/// </summary>
public class PerOperationSerializerTests
{
    private const string TestArn = "arn:aws:lambda:us-east-1:123:durable-execution:test";

    private static string IdAt(int position) => OperationIdGenerator.HashOperationId(position.ToString());

    private static string ChildIdAt(string parentOpId, int position) =>
        OperationIdGenerator.HashOperationId($"{parentOpId}-{position}");

    private static DurableContext CreateContext(ILambdaSerializer globalSerializer, InitialExecutionState? initialState = null)
    {
        var state = new ExecutionState();
        state.LoadFromCheckpoint(initialState);
        var tm = new TerminationManager();
        var idGen = new OperationIdGenerator();
        var lambdaContext = new TestLambdaContext { Serializer = globalSerializer };
        return new DurableContext(state, tm, new WorkflowCancellation(tm), idGen, TestArn, lambdaContext);
    }

    /// <summary>
    /// Spy <see cref="ILambdaSerializer"/> that counts calls and delegates to a real
    /// System.Text.Json serializer, so tests can assert which serializer an operation used.
    /// </summary>
    private sealed class SpySerializer : ILambdaSerializer
    {
        private readonly ILambdaSerializer _inner = new DefaultLambdaJsonSerializer();
        public int SerializeCount { get; private set; }
        public int DeserializeCount { get; private set; }

        public T Deserialize<T>(Stream requestStream)
        {
            DeserializeCount++;
            return _inner.Deserialize<T>(requestStream);
        }

        public void Serialize<T>(T response, Stream responseStream)
        {
            SerializeCount++;
            _inner.Serialize(response, responseStream);
        }
    }

    // ---------------------------------------------------------------- Step

    [Fact]
    public async Task Step_WithConfigSerializer_UsesPerOpSerializer_NotGlobal()
    {
        var global = new SpySerializer();
        var perOp = new SpySerializer();
        var ctx = CreateContext(global);

        var result = await ctx.StepAsync(
            async (_, _) => { await Task.CompletedTask; return 42; },
            name: "s",
            config: new StepConfig { Serializer = perOp });

        Assert.Equal(42, result);
        Assert.Equal(1, perOp.SerializeCount);
        Assert.Equal(0, global.SerializeCount);
    }

    [Fact]
    public async Task Step_NoConfigSerializer_UsesGlobal()
    {
        var global = new SpySerializer();
        var ctx = CreateContext(global);

        var result = await ctx.StepAsync(
            async (_, _) => { await Task.CompletedTask; return 7; },
            name: "s");

        Assert.Equal(7, result);
        Assert.Equal(1, global.SerializeCount);
    }

    [Fact]
    public async Task Step_Replay_UsesPerOpSerializerToDeserialize()
    {
        var global = new SpySerializer();
        var perOp = new SpySerializer();
        var ctx = CreateContext(global, new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = IdAt(1),
                    Type = OperationTypes.Step,
                    Status = OperationStatuses.Succeeded,
                    StepDetails = new StepDetails { Result = "\"cached\"" }
                }
            }
        });

        var result = await ctx.StepAsync(
            async (_, _) => { await Task.CompletedTask; return "fresh"; },
            name: "s",
            config: new StepConfig { Serializer = perOp });

        Assert.Equal("cached", result);
        Assert.Equal(1, perOp.DeserializeCount);
        Assert.Equal(0, global.DeserializeCount);
    }

    // ---------------------------------------------------------------- Step round-trip failure (fresh success)

    /// <summary>
    /// Serializer that serializes normally but throws on deserialize — models a
    /// custom serializer that cannot round-trip its own just-written payload.
    /// </summary>
    private sealed class DeserializeThrowingSerializer : ILambdaSerializer
    {
        private readonly ILambdaSerializer _inner = new DefaultLambdaJsonSerializer();
        public sealed class CannotDeserialize : Exception { }

        public T Deserialize<T>(Stream requestStream) => throw new CannotDeserialize();
        public void Serialize<T>(T response, Stream responseStream) => _inner.Serialize(response, responseStream);
    }

    [Fact]
    public async Task Step_FreshSuccess_RoundTripDeserializeFailure_FailsTerminallyWithoutRetry()
    {
        // The fresh-success round-trip (serialize + deserialize) runs BEFORE the
        // SUCCEED checkpoint is emitted. A serializer that cannot deserialize its own
        // just-written payload is a terminal failure: the step body already ran to
        // completion, so re-running it under the retry strategy would duplicate side
        // effects. Instead the fault is funneled through FailStepTerminallyAsync,
        // which emits a FAIL checkpoint and throws StepException WITHOUT consulting
        // the retry strategy. Because the deserialize runs before SUCCEED, no terminal
        // SUCCEED is ever committed for the poison payload.
        var state = new ExecutionState();
        state.LoadFromCheckpoint(null);
        var tm = new TerminationManager();
        var idGen = new OperationIdGenerator();
        var lambdaContext = new TestLambdaContext { Serializer = new DefaultLambdaJsonSerializer() };
        var recorder = new RecordingBatcher();
        var ctx = new DurableContext(state, tm, new WorkflowCancellation(tm), idGen, TestArn, lambdaContext, recorder.Batcher);

        var perOp = new DeserializeThrowingSerializer();

        // The deserialize fault is wrapped in a StepException by FailStepTerminallyAsync;
        // the original CannotDeserialize is preserved as the inner exception.
        var ex = await Assert.ThrowsAsync<StepException>(async () =>
            await ctx.StepAsync(
                async (_, _) => { await Task.CompletedTask; return 42; },
                name: "s",
                config: new StepConfig { Serializer = perOp }));
        Assert.IsType<DeserializeThrowingSerializer.CannotDeserialize>(ex.InnerException);

        await recorder.Batcher.DrainAsync();
        var stepActions = recorder.Flushed
            .Where(o => o.Type == OperationTypes.Step)
            .Select(o => o.Action)
            .ToList();

        // A terminal FAIL was emitted, and crucially NO SUCCEED (the round-trip failed
        // before SUCCEED) and NO RETRY (the side-effecting body already ran).
        Assert.Contains(OperationAction.FAIL, stepActions);
        Assert.DoesNotContain(OperationAction.SUCCEED, stepActions);
        Assert.DoesNotContain(OperationAction.RETRY, stepActions);
    }

    // ---------------------------------------------------------------- Callback (deserialize side)

    [Fact]
    public async Task Callback_Replay_UsesPerOpSerializerToDeserialize()
    {
        var global = new SpySerializer();
        var perOp = new SpySerializer();
        var ctx = CreateContext(global, new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = IdAt(1),
                    Type = OperationTypes.Callback,
                    Status = OperationStatuses.Succeeded,
                    CallbackDetails = new CallbackDetails { CallbackId = "cb-1", Result = "\"cbval\"" }
                }
            }
        });

        var callback = await ctx.CreateCallbackAsync<string>(name: "cb", config: new CallbackConfig { Serializer = perOp });
        var result = await callback.GetResultAsync();

        Assert.Equal("cbval", result);
        Assert.Equal(1, perOp.DeserializeCount);
        Assert.Equal(0, global.DeserializeCount);
    }

    // ---------------------------------------------------------------- Invoke

    [Fact]
    public async Task Invoke_Replay_UsesPerOpSerializerToDeserialize()
    {
        var global = new SpySerializer();
        var perOp = new SpySerializer();
        var ctx = CreateContext(global, new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = IdAt(1),
                    Type = OperationTypes.ChainedInvoke,
                    Status = OperationStatuses.Succeeded,
                    ChainedInvokeDetails = new ChainedInvokeDetails { Result = "\"invval\"" }
                }
            }
        });

        var result = await ctx.InvokeAsync<string, string>(
            "arn:aws:lambda:us-east-1:123:function:callee:1",
            "payload",
            name: "inv",
            config: new InvokeConfig { Serializer = perOp });

        Assert.Equal("invval", result);
        Assert.Equal(1, perOp.DeserializeCount);
        Assert.Equal(0, global.DeserializeCount);
    }

    [Fact]
    public async Task Invoke_FreshExecution_UsesPerOpSerializerToSerializeRequestPayload()
    {
        // Comment (Copilot): InvokeConfig.Serializer applies to the outbound request
        // payload on the initial (non-replay) execution as well as to the replay result.
        // This covers the outbound-serialize path (the replay-deserialize path is above).
        var global = new SpySerializer();
        var perOp = new SpySerializer();

        var state = new ExecutionState();
        state.LoadFromCheckpoint(null);
        var tm = new TerminationManager();
        var idGen = new OperationIdGenerator();
        var lambdaContext = new TestLambdaContext { Serializer = global };
        var recorder = new RecordingBatcher();
        var ctx = new DurableContext(
            state, tm, new WorkflowCancellation(tm), idGen, TestArn, lambdaContext, recorder.Batcher);

        // Fresh chained invoke: the request payload is serialized (via the per-op
        // serializer), the CHAINED_INVOKE START is flushed, then the workflow suspends.
        // The returned task never completes on the fresh path.
        var task = ctx.InvokeAsync<string, string>(
            "arn:aws:lambda:us-east-1:123:function:callee:1",
            "payload",
            name: "inv",
            config: new InvokeConfig { Serializer = perOp });

        await tm.WaitForTerminationAsync();
        Assert.False(task.IsCompleted);

        // The outbound request payload used the per-op serializer, not the global one.
        Assert.Equal(1, perOp.SerializeCount);
        Assert.Equal(0, global.SerializeCount);
    }

    // ---------------------------------------------------------------- ChildContext

    [Fact]
    public async Task ChildContext_WithConfigSerializer_UsesPerOpSerializer()
    {
        var global = new SpySerializer();
        var perOp = new SpySerializer();
        var ctx = CreateContext(global);

        var result = await ctx.RunInChildContextAsync(
            async (_, _) => { await Task.CompletedTask; return 99; },
            name: "child",
            config: new ChildContextConfig { Serializer = perOp });

        Assert.Equal(99, result);
        Assert.True(perOp.SerializeCount >= 1);
        Assert.Equal(0, global.SerializeCount);
    }

    // ---------------------------------------------------------------- WaitForCondition

    private sealed class StopImmediatelyStrategy : IWaitStrategy<int>
    {
        public WaitDecision Decide(int state, int attemptNumber) => WaitDecision.Stop();
    }

    [Fact]
    public async Task WaitForCondition_WithConfigSerializer_UsesPerOpSerializer()
    {
        var global = new SpySerializer();
        var perOp = new SpySerializer();
        var ctx = CreateContext(global);

        var result = await ctx.WaitForConditionAsync<int>(
            async (state, _, _) => { await Task.CompletedTask; return state + 1; },
            new WaitForConditionConfig<int>
            {
                InitialState = 0,
                WaitStrategy = new StopImmediatelyStrategy(),
                Serializer = perOp,
            },
            name: "wfc");

        Assert.Equal(1, result);
        Assert.True(perOp.SerializeCount >= 1);
        Assert.Equal(0, global.SerializeCount);
    }

    // ---------------------------------------------------------------- Map / Parallel (ItemSerializer)

    [Fact]
    public async Task Map_WithItemSerializer_UsesItForItemResults()
    {
        var global = new SpySerializer();
        var itemSer = new SpySerializer();
        var ctx = CreateContext(global);

        var result = await ctx.MapAsync(
            new[] { "a", "b" },
            async (_, item, _, _, _) => { await Task.CompletedTask; return item.ToUpperInvariant(); },
            name: "map",
            config: new MapConfig<string> { ItemSerializer = itemSer });

        Assert.Equal(2, result.SuccessCount);
        Assert.Equal(new[] { "A", "B" }, result.GetResults());
        Assert.True(itemSer.SerializeCount >= 2);   // each item result serialized via the item serializer
        Assert.Equal(0, global.SerializeCount);
    }

    [Fact]
    public async Task Map_NoItemSerializer_UsesGlobal()
    {
        var global = new SpySerializer();
        var ctx = CreateContext(global);

        var result = await ctx.MapAsync(
            new[] { "a" },
            async (_, item, _, _, _) => { await Task.CompletedTask; return item; },
            name: "map");

        Assert.Equal(1, result.SuccessCount);
        Assert.True(global.SerializeCount >= 1);
    }

    [Fact]
    public async Task Parallel_WithItemSerializer_UsesItForBranchResults()
    {
        var global = new SpySerializer();
        var itemSer = new SpySerializer();
        var ctx = CreateContext(global);

        var branches = new Func<IDurableContext, CancellationToken, Task<string>>[]
        {
            async (_, _) => { await Task.CompletedTask; return "x"; },
            async (_, _) => { await Task.CompletedTask; return "y"; },
        };

        var result = await ctx.ParallelAsync(
            branches,
            name: "par",
            config: new ParallelConfig { ItemSerializer = itemSer });

        Assert.Equal(2, result.SuccessCount);
        Assert.True(itemSer.SerializeCount >= 2);   // each branch result serialized via the item serializer
        Assert.Equal(0, global.SerializeCount);
    }

    // ---------------------------------------------------------------- Map / Parallel replay (deserialize)

    [Fact]
    public async Task Map_Replay_UsesItemSerializerToDeserializeItemResults()
    {
        // Comment (Copilot): also cover that ItemSerializer is used for replay
        // deserialization of cached per-item results (not just fresh serialization).
        var global = new SpySerializer();
        var itemSer = new SpySerializer();

        var parentOpId = IdAt(1);
        var i0 = ChildIdAt(parentOpId, 1);
        var i1 = ChildIdAt(parentOpId, 2);

        var summaryJson =
            "{\"CompletionReason\":\"ALL_COMPLETED\",\"Units\":[" +
            "{\"Index\":0,\"Name\":\"0\",\"Status\":\"SUCCEEDED\"}," +
            "{\"Index\":1,\"Name\":\"1\",\"Status\":\"SUCCEEDED\"}]}";

        var ctx = CreateContext(global, new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = parentOpId,
                    Type = OperationTypes.Context,
                    Status = OperationStatuses.Succeeded,
                    SubType = OperationSubTypes.Map,
                    Name = "map",
                    ContextDetails = new ContextDetails { Result = summaryJson }
                },
                new()
                {
                    Id = i0,
                    Type = OperationTypes.Context,
                    Status = OperationStatuses.Succeeded,
                    SubType = OperationSubTypes.MapIteration,
                    Name = "0",
                    ContextDetails = new ContextDetails { Result = "\"A\"" }
                },
                new()
                {
                    Id = i1,
                    Type = OperationTypes.Context,
                    Status = OperationStatuses.Succeeded,
                    SubType = OperationSubTypes.MapIteration,
                    Name = "1",
                    ContextDetails = new ContextDetails { Result = "\"B\"" }
                }
            }
        });

        var calls = 0;
        var result = await ctx.MapAsync(
            new[] { "a", "b" },
            async (_, item, _, _, _) => { calls++; await Task.CompletedTask; return item.ToUpperInvariant(); },
            name: "map",
            config: new MapConfig<string> { ItemSerializer = itemSer });

        Assert.Equal(0, calls);                                  // cached — callback not re-run
        Assert.Equal(new[] { "A", "B" }, result.GetResults());
        Assert.True(itemSer.DeserializeCount >= 2);              // each cached item result deserialized via the item serializer
        Assert.Equal(0, global.DeserializeCount);                // aggregate summary uses source-gen, not the ILambdaSerializer
    }

    [Fact]
    public async Task Parallel_Replay_UsesItemSerializerToDeserializeBranchResults()
    {
        var global = new SpySerializer();
        var itemSer = new SpySerializer();

        var parentOpId = IdAt(1);
        var b0 = ChildIdAt(parentOpId, 1);
        var b1 = ChildIdAt(parentOpId, 2);

        var summaryJson =
            "{\"CompletionReason\":\"ALL_COMPLETED\",\"Units\":[" +
            "{\"Index\":0,\"Name\":\"0\",\"Status\":\"SUCCEEDED\"}," +
            "{\"Index\":1,\"Name\":\"1\",\"Status\":\"SUCCEEDED\"}]}";

        var ctx = CreateContext(global, new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = parentOpId,
                    Type = OperationTypes.Context,
                    Status = OperationStatuses.Succeeded,
                    SubType = OperationSubTypes.Parallel,
                    Name = "par",
                    ContextDetails = new ContextDetails { Result = summaryJson }
                },
                new()
                {
                    Id = b0,
                    Type = OperationTypes.Context,
                    Status = OperationStatuses.Succeeded,
                    SubType = OperationSubTypes.ParallelBranch,
                    Name = "0",
                    ContextDetails = new ContextDetails { Result = "\"x\"" }
                },
                new()
                {
                    Id = b1,
                    Type = OperationTypes.Context,
                    Status = OperationStatuses.Succeeded,
                    SubType = OperationSubTypes.ParallelBranch,
                    Name = "1",
                    ContextDetails = new ContextDetails { Result = "\"y\"" }
                }
            }
        });

        var executed = false;
        var branches = new Func<IDurableContext, CancellationToken, Task<string>>[]
        {
            async (_, _) => { executed = true; await Task.CompletedTask; return "x"; },
            async (_, _) => { executed = true; await Task.CompletedTask; return "y"; },
        };

        var result = await ctx.ParallelAsync(
            branches,
            name: "par",
            config: new ParallelConfig { ItemSerializer = itemSer });

        Assert.False(executed);                                  // cached — branches not re-run
        Assert.Equal(new[] { "x", "y" }, result.GetResults());
        Assert.True(itemSer.DeserializeCount >= 2);              // each cached branch result deserialized via the item serializer
        Assert.Equal(0, global.DeserializeCount);
    }
}
