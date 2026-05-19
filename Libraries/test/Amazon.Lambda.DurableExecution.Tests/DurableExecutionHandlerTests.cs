using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.DurableExecution.Internal;
using Xunit;

namespace Amazon.Lambda.DurableExecution.Tests;

public class DurableExecutionHandlerTests
{
    [Fact]
    public async Task RunAsync_UserCodeCompletes_ReturnsSucceeded()
    {
        var state = new ExecutionState();
        state.LoadFromCheckpoint(null);
        var termination = new TerminationManager();

        var result = await DurableExecutionHandler.RunAsync<string>(
            state,
            termination,
            async () =>
            {
                await Task.Delay(1);
                return "hello";
            });

        Assert.Equal(InvocationStatus.Succeeded, result.Status);
        Assert.Equal("hello", result.Result);
        Assert.Null(result.Exception);
    }

    [Fact]
    public async Task RunAsync_UserCodeThrows_ReturnsFailed()
    {
        var state = new ExecutionState();
        state.LoadFromCheckpoint(null);
        var termination = new TerminationManager();

        var result = await DurableExecutionHandler.RunAsync<string>(
            state,
            termination,
            async () =>
            {
                await Task.Delay(1);
                throw new InvalidOperationException("something broke");
            });

        Assert.Equal(InvocationStatus.Failed, result.Status);
        Assert.Equal("something broke", result.Message);
        Assert.IsType<InvalidOperationException>(result.Exception);
    }

    [Fact]
    public async Task RunAsync_TerminationWins_ReturnsPending()
    {
        var state = new ExecutionState();
        state.LoadFromCheckpoint(null);
        var termination = new TerminationManager();

        var result = await DurableExecutionHandler.RunAsync<string>(
            state,
            termination,
            async () =>
            {
                // Simulate: user code hits a wait, signals termination, then blocks forever
                termination.Terminate(TerminationReason.WaitScheduled, "waiting 30s");
                await new TaskCompletionSource<string>().Task; // blocks forever
                return "unreachable";
            });

        Assert.Equal(InvocationStatus.Pending, result.Status);
        Assert.Equal("waiting 30s", result.Message);
        Assert.Null(result.Exception);
    }

    [Fact]
    public async Task RunAsync_TerminationWithException_ReturnsFailed()
    {
        var state = new ExecutionState();
        state.LoadFromCheckpoint(null);
        var termination = new TerminationManager();

        var result = await DurableExecutionHandler.RunAsync<string>(
            state,
            termination,
            async () =>
            {
                termination.Terminate(
                    TerminationReason.CheckpointFailed,
                    "checkpoint error",
                    new InvalidOperationException("service unavailable"));
                await new TaskCompletionSource<string>().Task;
                return "unreachable";
            });

        Assert.Equal(InvocationStatus.Failed, result.Status);
        Assert.IsType<InvalidOperationException>(result.Exception);
    }

    [Fact]
    public async Task RunAsync_FastUserCode_BeatsTermination()
    {
        var state = new ExecutionState();
        state.LoadFromCheckpoint(null);
        var termination = new TerminationManager();

        var result = await DurableExecutionHandler.RunAsync<int>(
            state,
            termination,
            async () =>
            {
                // User code completes before termination is called
                return 42;
            });

        Assert.Equal(InvocationStatus.Succeeded, result.Status);
        Assert.Equal(42, result.Result);
    }

    [Fact]
    public async Task RunAsync_IntResult_WorksWithValueTypes()
    {
        var state = new ExecutionState();
        state.LoadFromCheckpoint(null);
        var termination = new TerminationManager();

        var result = await DurableExecutionHandler.RunAsync<int>(
            state,
            termination,
            async () =>
            {
                await Task.CompletedTask;
                return 100;
            });

        Assert.Equal(InvocationStatus.Succeeded, result.Status);
        Assert.Equal(100, result.Result);
    }
}
