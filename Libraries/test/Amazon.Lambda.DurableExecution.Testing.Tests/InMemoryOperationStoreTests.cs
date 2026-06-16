// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.DurableExecution.Testing;
using Xunit;

namespace Amazon.Lambda.DurableExecution.Testing.Tests;

public class InMemoryOperationStoreTests
{
    [Fact]
    public void InitialState_EmptyOperations()
    {
        var store = new InMemoryOperationStore();
        Assert.Empty(store.GetAllOperations("arn:test"));
        Assert.Equal(0, store.OperationCount("arn:test"));
    }

    [Fact]
    public void InitialToken_IsZero()
    {
        var store = new InMemoryOperationStore();
        Assert.Equal("0", store.CurrentToken("arn:test"));
    }

    [Fact]
    public void Upsert_AddsNewOperation()
    {
        var store = new InMemoryOperationStore();
        var op = new Operation { Id = "op-1", Type = OperationTypes.Step, Name = "step1" };

        store.Upsert("arn:test", op);

        Assert.Equal(1, store.OperationCount("arn:test"));
        var retrieved = store.GetOperation("arn:test", "op-1");
        Assert.NotNull(retrieved);
        Assert.Equal("step1", retrieved!.Name);
    }

    [Fact]
    public void Upsert_UpdatesExistingOperation()
    {
        var store = new InMemoryOperationStore();
        var op1 = new Operation { Id = "op-1", Type = OperationTypes.Step, Status = OperationStatuses.Started };
        store.Upsert("arn:test", op1);

        var op2 = new Operation { Id = "op-1", Type = OperationTypes.Step, Status = OperationStatuses.Succeeded };
        store.Upsert("arn:test", op2);

        Assert.Equal(1, store.OperationCount("arn:test"));
        var retrieved = store.GetOperation("arn:test", "op-1");
        Assert.Equal(OperationStatuses.Succeeded, retrieved!.Status);
    }

    [Fact]
    public void GetOperation_ReturnsNull_WhenNotFound()
    {
        var store = new InMemoryOperationStore();
        Assert.Null(store.GetOperation("arn:test", "nonexistent"));
    }

    [Fact]
    public void IncrementToken_IncrementsCounter()
    {
        var store = new InMemoryOperationStore();
        var t1 = store.IncrementToken("arn:test");
        var t2 = store.IncrementToken("arn:test");
        var t3 = store.IncrementToken("arn:test");

        Assert.Equal("1", t1);
        Assert.Equal("2", t2);
        Assert.Equal("3", t3);
        Assert.Equal("3", store.CurrentToken("arn:test"));
    }

    [Fact]
    public void Executions_AreIsolated()
    {
        var store = new InMemoryOperationStore();
        store.Upsert("arn:exec-1", new Operation { Id = "op-1", Type = OperationTypes.Step });
        store.Upsert("arn:exec-2", new Operation { Id = "op-2", Type = OperationTypes.Step });

        Assert.Equal(1, store.OperationCount("arn:exec-1"));
        Assert.Equal(1, store.OperationCount("arn:exec-2"));
        Assert.Null(store.GetOperation("arn:exec-1", "op-2"));
        Assert.Null(store.GetOperation("arn:exec-2", "op-1"));
    }

    [Fact]
    public void GetAllOperations_PreservesInsertionOrder()
    {
        var store = new InMemoryOperationStore();
        store.Upsert("arn:test", new Operation { Id = "op-1", Type = OperationTypes.Step });
        store.Upsert("arn:test", new Operation { Id = "op-2", Type = OperationTypes.Wait });
        store.Upsert("arn:test", new Operation { Id = "op-3", Type = OperationTypes.Callback });

        var all = store.GetAllOperations("arn:test");
        Assert.Equal(3, all.Count);
        Assert.Equal("op-1", all[0].Id);
        Assert.Equal("op-2", all[1].Id);
        Assert.Equal("op-3", all[2].Id);
    }

    [Fact]
    public void Tokens_AreIsolatedPerExecution()
    {
        var store = new InMemoryOperationStore();
        store.IncrementToken("arn:exec-1");
        store.IncrementToken("arn:exec-1");
        store.IncrementToken("arn:exec-2");

        Assert.Equal("2", store.CurrentToken("arn:exec-1"));
        Assert.Equal("1", store.CurrentToken("arn:exec-2"));
    }
}
