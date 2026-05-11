using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.DurableExecution.Internal;
using Xunit;
using Operation = Amazon.Lambda.DurableExecution.Internal.Operation;
using StepDetails = Amazon.Lambda.DurableExecution.Internal.StepDetails;
namespace Amazon.Lambda.DurableExecution.Tests;

public class ExecutionStateTests
{
    [Fact]
    public void LoadFromCheckpoint_NullState_EntersExecutionMode()
    {
        var state = new ExecutionState();
        state.LoadFromCheckpoint(null);

        Assert.Equal(ExecutionMode.Execution, state.Mode);
        Assert.Equal(0, state.CheckpointedOperationCount);
    }

    [Fact]
    public void LoadFromCheckpoint_EmptyOperations_EntersExecutionMode()
    {
        var state = new ExecutionState();
        state.LoadFromCheckpoint(new InitialExecutionState { Operations = new List<Operation>() });

        Assert.Equal(ExecutionMode.Execution, state.Mode);
        Assert.Equal(0, state.CheckpointedOperationCount);
    }

    [Fact]
    public void LoadFromCheckpoint_WithOperations_StaysInReplayMode()
    {
        var state = new ExecutionState();
        state.LoadFromCheckpoint(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = "0-fetch_user",
                    Type = OperationTypes.Step,
                    Status = OperationStatuses.Succeeded,
                    StepDetails = new StepDetails { Result = "{\"name\":\"Alice\"}" }
                }
            }
        });

        Assert.Equal(ExecutionMode.Replay, state.Mode);
        Assert.Equal(1, state.CheckpointedOperationCount);
    }

    [Fact]
    public void GetOperation_ReturnsCheckpointedRecord()
    {
        var state = new ExecutionState();
        state.LoadFromCheckpoint(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = "0-validate",
                    Type = OperationTypes.Step,
                    Status = OperationStatuses.Succeeded,
                    StepDetails = new StepDetails { Result = "true" }
                }
            }
        });

        var op = state.GetOperation("0-validate");
        Assert.NotNull(op);
        Assert.Equal(OperationStatuses.Succeeded, op!.Status);
        Assert.Equal("true", op.StepDetails?.Result);
    }

    [Fact]
    public void GetOperation_ReturnsNull_WhenNotFound()
    {
        var state = new ExecutionState();
        state.LoadFromCheckpoint(null);

        var op = state.GetOperation("0-nonexistent");
        Assert.Null(op);
    }

    [Fact]
    public void HasOperation_ReturnsTrueForExisting()
    {
        var state = new ExecutionState();
        state.LoadFromCheckpoint(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = "0-step_a",
                    Type = OperationTypes.Step,
                    Status = OperationStatuses.Succeeded
                }
            }
        });

        Assert.True(state.HasOperation("0-step_a"));
        Assert.False(state.HasOperation("1-step_b"));
    }

    [Fact]
    public void EnterExecutionMode_FlipsModeAndIsIdempotent()
    {
        var state = new ExecutionState();
        state.LoadFromCheckpoint(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = "0",
                    Type = OperationTypes.Step,
                    Status = OperationStatuses.Succeeded
                }
            }
        });

        Assert.Equal(ExecutionMode.Replay, state.Mode);

        state.EnterExecutionMode();
        Assert.Equal(ExecutionMode.Execution, state.Mode);

        state.EnterExecutionMode();
        Assert.Equal(ExecutionMode.Execution, state.Mode);
    }

    [Fact]
    public void GetOperation_ReturnsLatestRecord_WhenIdAppearsMultipleTimes()
    {
        // Wire format: when the service replays an envelope it includes the
        // most recent record per ID. Java/Python/JS reference SDKs all key by
        // ID alone and rely on the service to provide the authoritative record.
        var state = new ExecutionState();
        state.LoadFromCheckpoint(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                new()
                {
                    Id = "0-payment",
                    Type = OperationTypes.Step,
                    Status = OperationStatuses.Started
                },
                new()
                {
                    Id = "0-payment",
                    Type = OperationTypes.Step,
                    Status = OperationStatuses.Succeeded,
                    StepDetails = new StepDetails { Result = "\"paid\"" }
                }
            }
        });

        var op = state.GetOperation("0-payment");
        Assert.NotNull(op);
        Assert.Equal(OperationStatuses.Succeeded, op!.Status);
        Assert.Equal("\"paid\"", op.StepDetails?.Result);
    }
}
