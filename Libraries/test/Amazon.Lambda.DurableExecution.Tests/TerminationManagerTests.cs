// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.DurableExecution.Internal;
using Xunit;

namespace Amazon.Lambda.DurableExecution.Tests;

public class TerminationManagerTests
{
    [Fact]
    public async Task Terminate_ResolvesTerminationTask()
    {
        var manager = new TerminationManager();
        Assert.False(manager.IsTerminated);

        manager.Terminate(TerminationReason.WaitScheduled, "wait pending");

        Assert.True(manager.IsTerminated);
        var result = await manager.TerminationTask;
        Assert.Equal(TerminationReason.WaitScheduled, result.Reason);
        Assert.Equal("wait pending", result.Message);
    }

    [Fact]
    public void Terminate_OnlyFirstCallWins()
    {
        var manager = new TerminationManager();

        var first = manager.Terminate(TerminationReason.WaitScheduled, "first");
        var second = manager.Terminate(TerminationReason.CallbackPending, "second");

        Assert.True(first);
        Assert.False(second);
    }

    [Fact]
    public async Task Terminate_FirstReasonIsPreserved()
    {
        var manager = new TerminationManager();

        manager.Terminate(TerminationReason.CallbackPending, "callback");
        manager.Terminate(TerminationReason.WaitScheduled, "wait");

        var result = await manager.TerminationTask;
        Assert.Equal(TerminationReason.CallbackPending, result.Reason);
        Assert.Equal("callback", result.Message);
    }

    [Fact]
    public async Task Terminate_WithException()
    {
        var manager = new TerminationManager();
        var ex = new Exception("checkpoint failed");

        manager.Terminate(TerminationReason.CheckpointFailed, "error", ex);

        var result = await manager.TerminationTask;
        Assert.Equal(TerminationReason.CheckpointFailed, result.Reason);
        Assert.Same(ex, result.Exception);
    }

    [Fact]
    public async Task TerminationTask_WinsRaceAgainstNeverCompletingTask()
    {
        var manager = new TerminationManager();
        var neverCompletes = new TaskCompletionSource<string>().Task;

        manager.Terminate(TerminationReason.WaitScheduled);

        var winner = await Task.WhenAny(neverCompletes, manager.TerminationTask);
        Assert.Same(manager.TerminationTask, winner);
    }

    [Fact]
    public async Task ConcurrentTerminate_OnlyOneSucceeds()
    {
        var manager = new TerminationManager();
        var results = new bool[10];

        var tasks = Enumerable.Range(0, 10).Select(i => Task.Run(() =>
        {
            results[i] = manager.Terminate(TerminationReason.WaitScheduled, $"caller-{i}");
        }));

        await Task.WhenAll(tasks);

        Assert.Equal(1, results.Count(r => r));
        Assert.True(manager.IsTerminated);
    }
}
