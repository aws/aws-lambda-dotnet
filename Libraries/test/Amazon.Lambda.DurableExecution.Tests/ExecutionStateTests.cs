using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.DurableExecution.Internal;
using Xunit;

namespace Amazon.Lambda.DurableExecution.Tests;

public class ExecutionStateTests
{
    private const string ExecutionInputId = "exec-input";

    private static Operation ExecutionInputOp(string id = ExecutionInputId) => new()
    {
        Id = id,
        Type = OperationTypes.Execution,
        Status = OperationStatuses.Started
    };

    private static Operation StepOp(string id, string status, string? name = null) => new()
    {
        Id = id,
        Type = OperationTypes.Step,
        Status = status,
        Name = name,
        StepDetails = new StepDetails { Result = "true" }
    };

    [Fact]
    public void LoadFromCheckpoint_NullState_NotReplaying()
    {
        var state = new ExecutionState();
        state.LoadFromCheckpoint(null);

        Assert.False(state.IsReplaying);
        Assert.Equal(0, state.CheckpointedOperationCount);
    }

    [Fact]
    public void LoadFromCheckpoint_EmptyOperations_NotReplaying()
    {
        var state = new ExecutionState();
        state.LoadFromCheckpoint(new InitialExecutionState { Operations = new List<Operation>() });

        Assert.False(state.IsReplaying);
        Assert.Equal(0, state.CheckpointedOperationCount);
    }

    [Fact]
    public void LoadFromCheckpoint_OnlyExecutionInputOp_NotReplaying()
    {
        // The service sends one EXECUTION-type op carrying the input payload
        // even on the first invocation. That op is bookkeeping, not user
        // history — it must not put us into replay mode. (Matches Python
        // execution.py:258, Java ExecutionManager:81, JS execution-context.ts:62.)
        var state = new ExecutionState();
        state.LoadFromCheckpoint(new InitialExecutionState
        {
            Operations = new List<Operation> { ExecutionInputOp() }
        });

        Assert.False(state.IsReplaying);
        Assert.Equal(1, state.CheckpointedOperationCount);
    }

    [Fact]
    public void LoadFromCheckpoint_WithReplayableOperations_IsReplaying()
    {
        var state = new ExecutionState();
        state.LoadFromCheckpoint(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                ExecutionInputOp(),
                StepOp("0-fetch_user", OperationStatuses.Succeeded)
            }
        });

        Assert.True(state.IsReplaying);
        Assert.Equal(2, state.CheckpointedOperationCount);
    }

    [Fact]
    public void TrackReplay_FlipsOutOfReplay_OnceAllCompletedOpsVisited()
    {
        var state = new ExecutionState();
        state.LoadFromCheckpoint(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                ExecutionInputOp(),
                StepOp("0", OperationStatuses.Succeeded),
                StepOp("1", OperationStatuses.Succeeded),
            }
        });
        Assert.True(state.IsReplaying);

        state.TrackReplay("0");
        Assert.True(state.IsReplaying); // 1-of-2 completed ops visited

        state.TrackReplay("1");
        Assert.False(state.IsReplaying); // all completed ops visited → fresh
    }

    [Fact]
    public void TrackReplay_PendingOpDoesNotBlockTransition()
    {
        // A PENDING op (e.g. retry timer waiting) is not "completed" in the
        // checkpoint sense — once the workflow has visited every terminally-
        // completed op the SDK treats subsequent code as fresh. Matches Python's
        // {SUCCEEDED, FAILED, CANCELLED, STOPPED, TIMED_OUT} terminal set.
        var state = new ExecutionState();
        state.LoadFromCheckpoint(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                ExecutionInputOp(),
                StepOp("0", OperationStatuses.Succeeded),
                StepOp("1", OperationStatuses.Pending),
            }
        });
        Assert.True(state.IsReplaying);

        state.TrackReplay("0");
        Assert.False(state.IsReplaying);
    }

    [Fact]
    public void TrackReplay_IsIdempotent()
    {
        var state = new ExecutionState();
        state.LoadFromCheckpoint(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                ExecutionInputOp(),
                StepOp("0", OperationStatuses.Succeeded),
            }
        });

        state.TrackReplay("0");
        Assert.False(state.IsReplaying);

        // Second call is a no-op.
        state.TrackReplay("0");
        Assert.False(state.IsReplaying);
    }

    [Fact]
    public void TrackReplay_NoOpWhenNotReplaying()
    {
        var state = new ExecutionState();
        state.LoadFromCheckpoint(null);
        Assert.False(state.IsReplaying);

        state.TrackReplay("anything");
        Assert.False(state.IsReplaying);
    }

    [Fact]
    public void GetOperation_ReturnsCheckpointedRecord()
    {
        var state = new ExecutionState();
        state.LoadFromCheckpoint(new InitialExecutionState
        {
            Operations = new List<Operation>
            {
                StepOp("0-validate", OperationStatuses.Succeeded)
            }
        });

        var op = state.GetOperation("0-validate");
        Assert.NotNull(op);
        Assert.Equal(OperationStatuses.Succeeded, op!.Status);
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
            Operations = new List<Operation> { StepOp("0-step_a", OperationStatuses.Succeeded) }
        });

        Assert.True(state.HasOperation("0-step_a"));
        Assert.False(state.HasOperation("1-step_b"));
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
