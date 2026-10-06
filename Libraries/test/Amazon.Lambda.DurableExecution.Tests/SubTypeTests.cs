// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.DurableExecution.Internal;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.Lambda.TestUtilities;
using Xunit;

namespace Amazon.Lambda.DurableExecution.Tests;

public class SubTypeTests
{
    private readonly ExecutionState _state = new();
    private readonly RecordingBatcher _recorder = new();
    private readonly DurableContext _context;

    public SubTypeTests()
    {
        var tm = new TerminationManager();
        _context = new DurableContext(_state, tm, new WorkflowCancellation(tm), new OperationIdGenerator(), "arn:test",
            new TestLambdaContext { Serializer = new DefaultLambdaJsonSerializer() }, _recorder.Batcher);
    }

    [Theory]
    [InlineData("ChargeCard", "ChargeCard")]
    [InlineData("", OperationSubTypes.Step)]
    [InlineData(null, OperationSubTypes.Step)]
    public async Task StepAsync_SendsConfiguredOrDefaultSubType(string? subType, string expected)
    {
        await _context.StepAsync(async (_, _) => { await Task.CompletedTask; return 1; },
            name: "s", config: new StepConfig { SubType = subType });

        Assert.Equal(2, _recorder.Flushed.Count); // START + SUCCEED
        Assert.All(_recorder.Flushed, u => Assert.Equal(expected, u.SubType));
    }

    [Theory]
    [InlineData("bad subtype!")]
    [InlineData("abcdefghijklmnopqrstuvwxyz0123456")] // 33 chars
    public async Task StepAsync_InvalidSubType_Throws(string subType)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _context.StepAsync(
            async (_, _) => { await Task.CompletedTask; return 1; },
            name: "s", config: new StepConfig { SubType = subType }));

        Assert.Empty(_recorder.Flushed);
    }

    [Fact]
    public async Task CreateCallbackAsync_SendsConfiguredSubType()
    {
        _recorder.OnFlush = ops => _state.AddOperations(ops.Select(u => new Operation
        {
            Id = u.Id,
            Type = OperationTypes.Callback,
            Status = OperationStatuses.Started,
            CallbackDetails = new CallbackDetails { CallbackId = "cb-1" }
        }).ToList());

        await _context.CreateCallbackAsync<string>(name: "cb", config: new CallbackConfig { SubType = "Approval" });

        Assert.Equal("Approval", Assert.Single(_recorder.Flushed).SubType);
    }

    [Fact]
    public async Task RunInChildContextAsync_InvalidSubType_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _context.RunInChildContextAsync(
            async (_, _) => { await Task.CompletedTask; return 1; },
            name: "child", config: new ChildContextConfig { SubType = "Order Saga" }));

        Assert.Empty(_recorder.Flushed);
    }
}
