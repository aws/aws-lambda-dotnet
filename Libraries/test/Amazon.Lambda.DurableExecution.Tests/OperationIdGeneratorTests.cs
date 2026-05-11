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
    public void NextId_NameIsNotPartOfId()
    {
        // Name must not influence the deterministic ID — replays must still
        // correlate after a step is renamed. The reference SDKs (Java/JS/Python)
        // all keep Name in a separate field on OperationUpdate.
        var gen = new OperationIdGenerator();
        Assert.Equal(Sha256Hex("1"), gen.NextId());
        Assert.Equal(Sha256Hex("2"), gen.NextId());
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
}
