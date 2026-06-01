// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using System.Text;
using Amazon.Lambda.DurableExecution.Internal;
using Xunit;

namespace Amazon.Lambda.DurableExecution.Tests;

public class OperationIdGeneratorTests
{
    private static string Sha256Hex(string input)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }

    [Fact]
    public void NextId_ProducesSha256OfPositionString_StartingAtOne()
    {
        var gen = new OperationIdGenerator();
        Assert.Equal(Sha256Hex("1"), gen.NextId());
        Assert.Equal(Sha256Hex("2"), gen.NextId());
        Assert.Equal(Sha256Hex("3"), gen.NextId());
    }

    [Fact]
    public void HashOperationId_IsStable()
    {
        Assert.Equal(Sha256Hex("hello"), OperationIdGenerator.HashOperationId("hello"));
        Assert.Equal(Sha256Hex("1"), OperationIdGenerator.HashOperationId("1"));
    }

    [Fact]
    public void ChildGenerator_PrefixesPositionWithParentHash()
    {
        var gen = new OperationIdGenerator();
        var parentId = gen.NextId();
        var child = gen.CreateChild(parentId);

        Assert.Equal(Sha256Hex(parentId + "-1"), child.NextId());
        Assert.Equal(Sha256Hex(parentId + "-2"), child.NextId());
    }

    [Fact]
    public void ChildGenerator_ParentIdProperty()
    {
        var gen = new OperationIdGenerator();
        Assert.Null(gen.ParentId);

        var child = new OperationIdGenerator("op-5");
        Assert.Equal("op-5", child.ParentId);
    }

    [Fact]
    public void MultipleChildren_IndependentCounters()
    {
        var child1 = new OperationIdGenerator("parent-1");
        var child2 = new OperationIdGenerator("parent-2");

        Assert.Equal(Sha256Hex("parent-1-1"), child1.NextId());
        Assert.Equal(Sha256Hex("parent-2-1"), child2.NextId());
        Assert.Equal(Sha256Hex("parent-1-2"), child1.NextId());
        Assert.Equal(Sha256Hex("parent-2-2"), child2.NextId());
    }

    [Fact]
    public void Deterministic_SameSequenceOnReplay()
    {
        var gen1 = new OperationIdGenerator();
        var ids1 = new[] { gen1.NextId(), gen1.NextId(), gen1.NextId() };

        var gen2 = new OperationIdGenerator();
        var ids2 = new[] { gen2.NextId(), gen2.NextId(), gen2.NextId() };

        Assert.Equal(ids1, ids2);
    }

    [Fact]
    public void Reset_RewindsCounter()
    {
        var gen = new OperationIdGenerator();
        gen.NextId();
        gen.NextId();
        gen.Reset();
        Assert.Equal(Sha256Hex("1"), gen.NextId());
    }

    [Fact]
    public async Task NextId_ConcurrentCallers_ProduceUniqueIds()
    {
        // Without Interlocked.Increment, two threads racing on ++_counter can
        // both observe the same pre-increment value and emit duplicate IDs,
        // silently breaking replay determinism. Drive enough contention to
        // catch a regression: many parallel callers, each making many calls.
        const int threads = 16;
        const int idsPerThread = 500;
        const int total = threads * idsPerThread;

        var gen = new OperationIdGenerator();
        var allIds = new string[total];
        var start = new ManualResetEventSlim(false);

        var tasks = Enumerable.Range(0, threads).Select(t => Task.Run(() =>
        {
            start.Wait();
            for (var i = 0; i < idsPerThread; i++)
            {
                allIds[t * idsPerThread + i] = gen.NextId();
            }
        })).ToArray();

        start.Set();
        await Task.WhenAll(tasks);

        Assert.Equal(total, allIds.Distinct().Count());

        // Counter advanced exactly `total` times — the next ID must be hash("total+1").
        Assert.Equal(Sha256Hex((total + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)),
            gen.NextId());
    }
}
