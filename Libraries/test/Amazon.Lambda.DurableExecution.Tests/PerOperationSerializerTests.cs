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
}
