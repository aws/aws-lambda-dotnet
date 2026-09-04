// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution.Internal;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.Lambda.TestUtilities;
using Xunit;

namespace Amazon.Lambda.DurableExecution.Tests;

/// <summary>
/// Verifies that when a per-operation serializer implements
/// <see cref="IDurableResultSerializer"/>, the durable runtime invokes the context-aware
/// overloads and supplies the operation's identity (<c>EntityId</c> = operation id, and
/// the durable execution ARN). Also verifies that a plain
/// <see cref="ILambdaSerializer"/> still works via the fallback path.
/// </summary>
public class DurableResultSerializerContextTests
{
    private const string TestArn = "arn:aws:lambda:us-east-1:123:durable-execution:test";

    private static string IdAt(int position) => OperationIdGenerator.HashOperationId(position.ToString());

    private static DurableContext CreateContext(ILambdaSerializer global, InitialExecutionState? initial = null)
    {
        var state = new ExecutionState();
        state.LoadFromCheckpoint(initial);
        var tm = new TerminationManager();
        var idGen = new OperationIdGenerator();
        var lambdaContext = new TestLambdaContext { Serializer = global };
        return new DurableContext(state, tm, new WorkflowCancellation(tm), idGen, TestArn, lambdaContext);
    }

    /// <summary>
    /// A serializer that captures the <see cref="DurableSerializationContext"/> it is
    /// handed on each context-aware call, delegating the actual bytes to JSON.
    /// </summary>
    private sealed class CapturingSerializer : ILambdaSerializer, IDurableResultSerializer
    {
        private readonly ILambdaSerializer _inner = new DefaultLambdaJsonSerializer();
        public List<DurableSerializationContext> SerializeContexts { get; } = new();
        public List<DurableSerializationContext> DeserializeContexts { get; } = new();

        public void Serialize<T>(T value, Stream stream, DurableSerializationContext context)
        {
            SerializeContexts.Add(context);
            _inner.Serialize(value, stream);
        }

        public T Deserialize<T>(Stream stream, DurableSerializationContext context)
        {
            DeserializeContexts.Add(context);
            return _inner.Deserialize<T>(stream);
        }

        // Plain path — should not be exercised while dispatch prefers the context overload.
        void ILambdaSerializer.Serialize<T>(T response, Stream responseStream) => _inner.Serialize(response, responseStream);
        T ILambdaSerializer.Deserialize<T>(Stream requestStream) => _inner.Deserialize<T>(requestStream);
    }

    /// <summary>Plain serializer (no <see cref="IDurableResultSerializer"/>) that just counts.</summary>
    private sealed class PlainSpy : ILambdaSerializer
    {
        private readonly ILambdaSerializer _inner = new DefaultLambdaJsonSerializer();
        public int SerializeCount { get; private set; }
        public void Serialize<T>(T response, Stream responseStream) { SerializeCount++; _inner.Serialize(response, responseStream); }
        public T Deserialize<T>(Stream requestStream) => _inner.Deserialize<T>(requestStream);
    }

    [Fact]
    public async Task Step_PassesOperationIdAndArn_AsContext()
    {
        var ser = new CapturingSerializer();
        var ctx = CreateContext(new DefaultLambdaJsonSerializer());

        var result = await ctx.StepAsync(
            async (_, _) => { await Task.CompletedTask; return 42; },
            name: "s",
            config: new StepConfig { Serializer = ser });

        Assert.Equal(42, result);
        // Fresh success round-trips (serialize + deserialize), both carry the same context.
        Assert.All(ser.SerializeContexts, c => { Assert.Equal(IdAt(1), c.EntityId); Assert.Equal(TestArn, c.DurableExecutionArn); });
        Assert.All(ser.DeserializeContexts, c => { Assert.Equal(IdAt(1), c.EntityId); Assert.Equal(TestArn, c.DurableExecutionArn); });
        Assert.NotEmpty(ser.SerializeContexts);
        // NotEmpty guards the Assert.All above from passing vacuously: a regression that
        // dropped the fresh-success deserialize round-trip would leave DeserializeContexts
        // empty and must fail here.
        Assert.NotEmpty(ser.DeserializeContexts);
    }

    [Fact]
    public async Task ChildContext_PassesOperationIdAndArn_AsContext()
    {
        var ser = new CapturingSerializer();
        var ctx = CreateContext(new DefaultLambdaJsonSerializer());

        var result = await ctx.RunInChildContextAsync(
            async (_, _) => { await Task.CompletedTask; return 99; },
            name: "child",
            config: new ChildContextConfig { Serializer = ser });

        Assert.Equal(99, result);
        Assert.NotEmpty(ser.SerializeContexts);
        Assert.All(ser.SerializeContexts, c => { Assert.Equal(IdAt(1), c.EntityId); Assert.Equal(TestArn, c.DurableExecutionArn); });
        // The child context also round-trips on fresh success; assert the deserialize
        // side both ran (NotEmpty) and carried the operation identity, so a regression
        // dropping the round-trip is caught here too.
        Assert.NotEmpty(ser.DeserializeContexts);
        Assert.All(ser.DeserializeContexts, c => { Assert.Equal(IdAt(1), c.EntityId); Assert.Equal(TestArn, c.DurableExecutionArn); });
    }

    [Fact]
    public async Task Map_Nested_PassesChildOperationEntityIds()
    {
        // Nested (the DEFAULT NestingType): each unit is a NON-virtual child that serializes
        // its OWN result at its OWN operation id (childOpId), and the parent inlines that
        // child payload verbatim. So the per-item serialize contexts carry the child op ids,
        // NOT the parent's per-unit "{parentOpId}#{i}" ids. (Before the comment-1 fix the
        // parent re-serialized each Nested unit at "{parentOpId}#{i}" — a DIFFERENT entity id
        // than the child wrote at, which orphaned a context-aware serializer's file and
        // double-transformed a non-round-tripping serializer.)
        var ser = new CapturingSerializer();
        var ctx = CreateContext(new DefaultLambdaJsonSerializer());

        var result = await ctx.MapAsync(
            new[] { "a", "b" },
            async (_, item, _, _, _) => { await Task.CompletedTask; return item.ToUpperInvariant(); },
            name: "map",
            config: new MapConfig<string> { ItemSerializer = ser });

        Assert.Equal(2, result.SuccessCount);
        var parentOpId = IdAt(1);
        var child0 = OperationIdGenerator.HashOperationId($"{parentOpId}-1");
        var child1 = OperationIdGenerator.HashOperationId($"{parentOpId}-2");
        var ids = ser.SerializeContexts.Select(c => c.EntityId).Distinct().ToList();
        Assert.Contains(child0, ids);
        Assert.Contains(child1, ids);
        // The stale parent per-unit ids must NOT appear (the double-serialize is gone).
        Assert.DoesNotContain($"{parentOpId}#0", ids);
        Assert.DoesNotContain($"{parentOpId}#1", ids);
        Assert.All(ser.SerializeContexts, c => Assert.Equal(TestArn, c.DurableExecutionArn));
    }

    [Fact]
    public async Task Map_Flat_PassesDistinctPerItemEntityIds()
    {
        // Flat (virtual) units emit no child checkpoint, so the PARENT serializes each unit
        // result inline at its per-unit id "{parentOpId}#{i}".
        var ser = new CapturingSerializer();
        var ctx = CreateContext(new DefaultLambdaJsonSerializer());

        var result = await ctx.MapAsync(
            new[] { "a", "b" },
            async (_, item, _, _, _) => { await Task.CompletedTask; return item.ToUpperInvariant(); },
            name: "map",
            config: new MapConfig<string> { NestingType = NestingType.Flat, ItemSerializer = ser });

        Assert.Equal(2, result.SuccessCount);
        var ids = ser.SerializeContexts.Select(c => c.EntityId).Distinct().ToList();
        Assert.Contains($"{IdAt(1)}#0", ids);
        Assert.Contains($"{IdAt(1)}#1", ids);
        Assert.All(ser.SerializeContexts, c => Assert.Equal(TestArn, c.DurableExecutionArn));
    }

    [Fact]
    public async Task PlainSerializer_StillRoundTrips_ViaFallback()
    {
        var plain = new PlainSpy();
        var ctx = CreateContext(new DefaultLambdaJsonSerializer());

        var result = await ctx.StepAsync(
            async (_, _) => { await Task.CompletedTask; return "hello"; },
            name: "s",
            config: new StepConfig { Serializer = plain });

        Assert.Equal("hello", result);
        Assert.True(plain.SerializeCount >= 1);
    }

    /// <summary>
    /// A serializer whose deserialize throws must fail the step (the round-trip runs
    /// BEFORE the SUCCEED checkpoint), not leave it checkpointed SUCCEEDED-but-thrown.
    /// Regression guard for the fresh-success round-trip being inside the fault path.
    /// </summary>
    [Fact]
    public async Task Step_FreshSuccessRoundTripThatThrows_SurfacesAsStepFailure()
    {
        var ser = new ThrowingDeserializeSerializer();
        var ctx = CreateContext(new DefaultLambdaJsonSerializer());
        var runs = 0;

        await Assert.ThrowsAsync<StepException>(() => ctx.StepAsync(
            async (_, _) => { runs++; await Task.CompletedTask; return 42; },
            name: "s",
            config: new StepConfig { Serializer = ser }));

        // Body ran exactly once (no retry loop), and it was serialized before the
        // failing deserialize — i.e. the round-trip failed cleanly, not after a
        // second terminal checkpoint.
        Assert.Equal(1, runs);
        Assert.Equal(1, ser.SerializeCount);
    }

    /// <summary>
    /// On replay, a Map reconstructed from inline per-unit payloads must deserialize
    /// each unit with the SAME per-unit EntityId (<c>{OperationId}#{index}</c>) that
    /// the serialize side used — otherwise a context-aware serializer keying storage
    /// by EntityId cannot find the value. Regression guard for the serialize/deserialize
    /// context asymmetry in ConcurrentOperation.
    /// </summary>
    [Fact]
    public async Task Map_Replay_DeserializesEachUnitWithPerItemEntityId()
    {
        var parentOpId = IdAt(1);
        var summaryJson =
            "{\"CompletionReason\":\"ALL_COMPLETED\",\"Units\":[" +
            "{\"Index\":0,\"Name\":\"0\",\"Status\":\"SUCCEEDED\",\"Result\":\"\\\"A\\\"\"}," +
            "{\"Index\":1,\"Name\":\"1\",\"Status\":\"SUCCEEDED\",\"Result\":\"\\\"B\\\"\"}]}";

        var ser = new CapturingSerializer();
        var ctx = CreateContext(new DefaultLambdaJsonSerializer(), new InitialExecutionState
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
                }
            }
        });

        var executed = false;
        var result = await ctx.MapAsync(
            new[] { "a", "b" },
            async (_, item, _, _, _) => { executed = true; await Task.Yield(); return item.ToUpperInvariant(); },
            name: "map",
            config: new MapConfig<string> { NestingType = NestingType.Flat, ItemSerializer = ser });

        Assert.False(executed); // replay: bodies not re-run
        Assert.Equal(new[] { "A", "B" }, result.GetResults());

        var ids = ser.DeserializeContexts.Select(c => c.EntityId).ToList();
        Assert.Contains($"{parentOpId}#0", ids);
        Assert.Contains($"{parentOpId}#1", ids);
        Assert.All(ser.DeserializeContexts, c => Assert.Equal(TestArn, c.DurableExecutionArn));
    }

    /// <summary>Serializes via JSON but always throws on the context-aware deserialize.</summary>
    private sealed class ThrowingDeserializeSerializer : ILambdaSerializer, IDurableResultSerializer
    {
        private readonly ILambdaSerializer _inner = new DefaultLambdaJsonSerializer();
        public int SerializeCount { get; private set; }

        public void Serialize<T>(T value, Stream stream, DurableSerializationContext context)
        {
            SerializeCount++;
            _inner.Serialize(value, stream);
        }

        public T Deserialize<T>(Stream stream, DurableSerializationContext context) =>
            throw new InvalidOperationException("boom on deserialize");

        void ILambdaSerializer.Serialize<T>(T response, Stream responseStream) => _inner.Serialize(response, responseStream);
        T ILambdaSerializer.Deserialize<T>(Stream requestStream) => _inner.Deserialize<T>(requestStream);
    }
}
