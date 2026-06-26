// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.DurableExecution.Testing;
using Amazon.Lambda.Model;
using Xunit;
using SdkOperationUpdate = Amazon.Lambda.Model.OperationUpdate;

namespace Amazon.Lambda.DurableExecution.Testing.Tests;

public class CheckpointProcessorTests
{
    private const string Arn = "arn:aws:lambda:us-east-1:123:execution:fn:exec";

    [Fact]
    public void Process_StepStart_CreatesOperation()
    {
        var (store, processor) = Create(skipTime: false);

        var updates = new List<SdkOperationUpdate>
        {
            new() { Id = "op-1", Type = OperationTypes.Step, Action = OperationAction.START, Name = "validate", SubType = OperationSubTypes.Step }
        };

        var (token, newOps) = processor.Process(Arn, null, updates);

        Assert.NotNull(token);
        var op = store.GetOperation(Arn, "op-1");
        Assert.NotNull(op);
        Assert.Equal(OperationStatuses.Started, op!.Status);
        Assert.Equal("validate", op.Name);
        Assert.Equal(1, op.StepDetails?.Attempt);
        Assert.NotNull(op.StartTimestamp);
    }

    [Fact]
    public void Process_StepSucceed_UpdatesStatus()
    {
        var (store, processor) = Create(skipTime: false);
        processor.Process(Arn, null, new List<SdkOperationUpdate>
        {
            new() { Id = "op-1", Type = OperationTypes.Step, Action = OperationAction.START, Name = "step1" }
        });

        processor.Process(Arn, "1", new List<SdkOperationUpdate>
        {
            new() { Id = "op-1", Type = OperationTypes.Step, Action = OperationAction.SUCCEED, Payload = """{"x":1}""" }
        });

        var op = store.GetOperation(Arn, "op-1");
        Assert.Equal(OperationStatuses.Succeeded, op!.Status);
        Assert.Equal("""{"x":1}""", op.StepDetails!.Result);
        Assert.Null(op.StepDetails.Error);
        Assert.NotNull(op.EndTimestamp);
    }

    [Fact]
    public void Process_StepFail_SetsError()
    {
        var (store, processor) = Create(skipTime: false);
        processor.Process(Arn, null, new List<SdkOperationUpdate>
        {
            new() { Id = "op-1", Type = OperationTypes.Step, Action = OperationAction.START }
        });

        processor.Process(Arn, "1", new List<SdkOperationUpdate>
        {
            new()
            {
                Id = "op-1", Type = OperationTypes.Step, Action = OperationAction.FAIL,
                Error = new Amazon.Lambda.Model.ErrorObject { ErrorType = "TestEx", ErrorMessage = "boom" }
            }
        });

        var op = store.GetOperation(Arn, "op-1");
        Assert.Equal(OperationStatuses.Failed, op!.Status);
        Assert.Equal("TestEx", op.StepDetails!.Error!.ErrorType);
        Assert.Equal("boom", op.StepDetails.Error.ErrorMessage);
    }

    [Fact]
    public void Process_StepRetry_SetsPendingWithDelay()
    {
        var (store, processor) = Create(skipTime: false);
        processor.Process(Arn, null, new List<SdkOperationUpdate>
        {
            new() { Id = "op-1", Type = OperationTypes.Step, Action = OperationAction.START }
        });

        processor.Process(Arn, "1", new List<SdkOperationUpdate>
        {
            new()
            {
                Id = "op-1", Type = OperationTypes.Step, Action = OperationAction.RETRY,
                StepOptions = new StepOptions { NextAttemptDelaySeconds = 5 },
                Error = new Amazon.Lambda.Model.ErrorObject { ErrorType = "TransientEx" }
            }
        });

        var op = store.GetOperation(Arn, "op-1");
        Assert.Equal(OperationStatuses.Pending, op!.Status);
        Assert.NotNull(op.StepDetails!.NextAttemptTimestamp);
        Assert.Equal("TransientEx", op.StepDetails.Error!.ErrorType);
    }

    [Fact]
    public void Process_StepRetry_WithSkipTime_SetsReady()
    {
        var (store, processor) = Create(skipTime: true);
        processor.Process(Arn, null, new List<SdkOperationUpdate>
        {
            new() { Id = "op-1", Type = OperationTypes.Step, Action = OperationAction.START }
        });

        processor.Process(Arn, "1", new List<SdkOperationUpdate>
        {
            new()
            {
                Id = "op-1", Type = OperationTypes.Step, Action = OperationAction.RETRY,
                StepOptions = new StepOptions { NextAttemptDelaySeconds = 60 }
            }
        });

        var op = store.GetOperation(Arn, "op-1");
        Assert.Equal(OperationStatuses.Ready, op!.Status);
        Assert.True(op.StepDetails!.NextAttemptTimestamp <= DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    }

    [Fact]
    public void Process_WaitStart_SetsScheduledEnd()
    {
        var (store, processor) = Create(skipTime: false);

        processor.Process(Arn, null, new List<SdkOperationUpdate>
        {
            new()
            {
                Id = "op-1", Type = OperationTypes.Wait, Action = OperationAction.START,
                WaitOptions = new WaitOptions { WaitSeconds = 300 }
            }
        });

        var op = store.GetOperation(Arn, "op-1");
        Assert.Equal(OperationStatuses.Started, op!.Status);
        Assert.NotNull(op.WaitDetails?.ScheduledEndTimestamp);
        var scheduled = DateTimeOffset.FromUnixTimeMilliseconds(op.WaitDetails!.ScheduledEndTimestamp!.Value);
        Assert.True(scheduled > DateTimeOffset.UtcNow.AddSeconds(290));
    }

    [Fact]
    public void Process_WaitStart_WithSkipTime_ImmediatelySucceeds()
    {
        var (store, processor) = Create(skipTime: true);

        processor.Process(Arn, null, new List<SdkOperationUpdate>
        {
            new()
            {
                Id = "op-1", Type = OperationTypes.Wait, Action = OperationAction.START,
                WaitOptions = new WaitOptions { WaitSeconds = 86400 }
            }
        });

        var op = store.GetOperation(Arn, "op-1");
        Assert.Equal(OperationStatuses.Succeeded, op!.Status);
        Assert.True(op.WaitDetails!.ScheduledEndTimestamp <= DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    }

    [Fact]
    public void Process_CallbackStart_MintsCallbackId()
    {
        var (store, processor) = Create(skipTime: false);

        processor.Process(Arn, null, new List<SdkOperationUpdate>
        {
            new() { Id = "op-cb-1", Type = OperationTypes.Callback, Action = OperationAction.START, Name = "approval" }
        });

        var op = store.GetOperation(Arn, "op-cb-1");
        Assert.Equal(OperationStatuses.Started, op!.Status);
        Assert.Equal("cb-op-cb-1", op.CallbackDetails!.CallbackId);
    }

    [Fact]
    public void Process_IncreasesTokenEachCall()
    {
        var (store, processor) = Create(skipTime: false);

        var (t1, _) = processor.Process(Arn, null, new List<SdkOperationUpdate>
        {
            new() { Id = "op-1", Type = OperationTypes.Step, Action = OperationAction.START }
        });
        var (t2, _) = processor.Process(Arn, t1, new List<SdkOperationUpdate>
        {
            new() { Id = "op-1", Type = OperationTypes.Step, Action = OperationAction.SUCCEED }
        });

        Assert.NotEqual(t1, t2);
    }

    [Fact]
    public void Process_ReturnsNewOperations()
    {
        var (_, processor) = Create(skipTime: false);

        var (_, newOps) = processor.Process(Arn, null, new List<SdkOperationUpdate>
        {
            new() { Id = "op-1", Type = OperationTypes.Step, Action = OperationAction.START, Name = "s1" },
            new() { Id = "op-2", Type = OperationTypes.Wait, Action = OperationAction.START, WaitOptions = new WaitOptions { WaitSeconds = 10 } }
        });

        Assert.Equal(2, newOps.Count);
        Assert.Equal("op-1", newOps[0].Id);
        Assert.Equal("op-2", newOps[1].Id);
    }

    [Fact]
    public void Process_ContextStart_SetsContextDetails()
    {
        var (store, processor) = Create(skipTime: false);

        processor.Process(Arn, null, new List<SdkOperationUpdate>
        {
            new() { Id = "op-ctx", Type = OperationTypes.Context, Action = OperationAction.START, Name = "parallel_batch", SubType = "Parallel" }
        });

        var op = store.GetOperation(Arn, "op-ctx");
        Assert.Equal(OperationStatuses.Started, op!.Status);
        Assert.Equal("Parallel", op.SubType);
        Assert.NotNull(op.ContextDetails);
    }

    [Fact]
    public void Process_ChainedInvokeStart_SetsDetails()
    {
        var (store, processor) = Create(skipTime: false);

        processor.Process(Arn, null, new List<SdkOperationUpdate>
        {
            new() { Id = "op-inv", Type = OperationTypes.ChainedInvoke, Action = OperationAction.START, Name = "process-payment" }
        });

        var op = store.GetOperation(Arn, "op-inv");
        Assert.Equal(OperationStatuses.Started, op!.Status);
        Assert.NotNull(op.ChainedInvokeDetails);
    }

    [Fact]
    public void Process_MultipleUpdatesInBatch_AllApplied()
    {
        var (store, processor) = Create(skipTime: true);

        processor.Process(Arn, null, new List<SdkOperationUpdate>
        {
            new() { Id = "op-1", Type = OperationTypes.Step, Action = OperationAction.START, Name = "a" },
            new() { Id = "op-2", Type = OperationTypes.Step, Action = OperationAction.START, Name = "b" },
            new() { Id = "op-3", Type = OperationTypes.Wait, Action = OperationAction.START, WaitOptions = new WaitOptions { WaitSeconds = 60 } }
        });

        Assert.Equal(3, store.OperationCount(Arn));
        Assert.Equal(OperationStatuses.Started, store.GetOperation(Arn, "op-1")!.Status);
        Assert.Equal(OperationStatuses.Succeeded, store.GetOperation(Arn, "op-3")!.Status);
    }

    [Fact]
    public void Process_WaitForCondition_Retry_WithSkipTime_SetsReady()
    {
        var (store, processor) = Create(skipTime: true);

        // The runtime wire-encodes WaitForCondition as Type=STEP with
        // SubType=WaitForCondition (see WaitForConditionOperation.OperationType).
        // A STEP START is NOT time-skipped to Succeeded — only WAIT timers are —
        // so the op stays Started and the condition is genuinely re-evaluated.
        processor.Process(Arn, null, new List<SdkOperationUpdate>
        {
            new() { Id = "op-wfc", Type = OperationTypes.Step, Action = OperationAction.START, SubType = OperationSubTypes.WaitForCondition }
        });

        var op = store.GetOperation(Arn, "op-wfc");
        Assert.Equal(OperationStatuses.Started, op!.Status);

        // A RETRY (condition not yet met) becomes immediately READY under SkipTime,
        // so the next replay re-runs the check without waiting for the poll delay.
        processor.Process(Arn, "1", new List<SdkOperationUpdate>
        {
            new() { Id = "op-wfc", Type = OperationTypes.Step, Action = OperationAction.RETRY, SubType = OperationSubTypes.WaitForCondition, StepOptions = new StepOptions { NextAttemptDelaySeconds = 10 } }
        });

        op = store.GetOperation(Arn, "op-wfc");
        Assert.Equal(OperationStatuses.Ready, op!.Status);
    }

    private static (InMemoryOperationStore Store, CheckpointProcessor Processor) Create(bool skipTime)
    {
        var store = new InMemoryOperationStore();
        var processor = new CheckpointProcessor(store, skipTime);
        return (store, processor);
    }
}
