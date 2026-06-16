// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.Model;
using SdkOperationUpdate = Amazon.Lambda.Model.OperationUpdate;

namespace Amazon.Lambda.DurableExecution.Testing;

/// <summary>
/// Processes checkpoint updates against the in-memory operation store.
/// Handles action-to-status mapping, callback ID minting, time skipping,
/// and producing the "new operations" response the runtime expects.
/// </summary>
internal sealed class CheckpointProcessor
{
    private readonly InMemoryOperationStore _store;
    private readonly bool _skipTime;

    public CheckpointProcessor(InMemoryOperationStore store, bool skipTime)
    {
        _store = store;
        _skipTime = skipTime;
    }

    /// <summary>
    /// Processes a batch of updates and returns the new checkpoint token
    /// and any operations that were created or modified (to feed back to
    /// the runtime's onNewOperations callback).
    /// </summary>
    public (string NewToken, IReadOnlyList<Operation> NewOperations) Process(
        string arn,
        string? currentToken,
        IReadOnlyList<SdkOperationUpdate> updates)
    {
        var newOperations = new List<Operation>();

        foreach (var update in updates)
        {
            var operation = ApplyUpdate(arn, update);
            newOperations.Add(operation);
        }

        var newToken = _store.IncrementToken(arn);
        return (newToken, newOperations);
    }

    private Operation ApplyUpdate(string arn, SdkOperationUpdate update)
    {
        var existing = _store.GetOperation(arn, update.Id);
        var operation = existing ?? new Operation { Id = update.Id };

        operation.Type = update.Type?.Value ?? operation.Type;
        operation.Name = update.Name ?? operation.Name;
        operation.ParentId = update.ParentId ?? operation.ParentId;
        operation.SubType = update.SubType ?? operation.SubType;

        var action = update.Action?.Value;
        ApplyAction(operation, action, update);

        if (_skipTime)
            ApplyTimeSkipping(operation, action);

        _store.Upsert(arn, operation);
        return operation;
    }

    private static void ApplyAction(Operation operation, string? action, SdkOperationUpdate update)
    {
        switch (action)
        {
            case "START":
                operation.Status = OperationStatuses.Started;
                operation.StartTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                ApplyStartDetails(operation, update);
                break;

            case "SUCCEED":
                operation.Status = OperationStatuses.Succeeded;
                operation.EndTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                ApplySucceedDetails(operation, update);
                break;

            case "FAIL":
                operation.Status = OperationStatuses.Failed;
                operation.EndTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                ApplyFailDetails(operation, update);
                break;

            case "RETRY":
                operation.Status = OperationStatuses.Pending;
                ApplyRetryDetails(operation, update);
                break;

            case "CANCEL":
                operation.Status = OperationStatuses.Cancelled;
                operation.EndTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                break;
        }
    }

    private static void ApplyStartDetails(Operation operation, SdkOperationUpdate update)
    {
        switch (operation.Type)
        {
            case OperationTypes.Step:
                operation.StepDetails ??= new StepDetails();
                operation.StepDetails.Attempt = (operation.StepDetails.Attempt ?? 0) + 1;
                break;

            case OperationTypes.Wait:
                operation.WaitDetails ??= new WaitDetails();
                if (update.WaitOptions?.WaitSeconds is { } seconds)
                {
                    operation.WaitDetails.ScheduledEndTimestamp =
                        DateTimeOffset.UtcNow.AddSeconds(seconds).ToUnixTimeMilliseconds();
                }
                break;

            case OperationTypes.Callback:
                operation.CallbackDetails ??= new CallbackDetails();
                operation.CallbackDetails.CallbackId = $"cb-{operation.Id}";
                break;

            case OperationTypes.ChainedInvoke:
                operation.ChainedInvokeDetails ??= new ChainedInvokeDetails();
                break;

            case OperationTypes.Context:
                operation.ContextDetails ??= new ContextDetails();
                break;

            case OperationTypes.Execution:
                operation.ExecutionDetails ??= new ExecutionDetails();
                break;
        }
    }

    private static void ApplySucceedDetails(Operation operation, SdkOperationUpdate update)
    {
        var payload = update.Payload;
        switch (operation.Type)
        {
            case OperationTypes.Step:
                operation.StepDetails ??= new StepDetails();
                operation.StepDetails.Result = payload;
                operation.StepDetails.Error = null;
                break;

            case OperationTypes.ChainedInvoke:
                operation.ChainedInvokeDetails ??= new ChainedInvokeDetails();
                operation.ChainedInvokeDetails.Result = payload;
                operation.ChainedInvokeDetails.Error = null;
                break;

            case OperationTypes.Context:
                operation.ContextDetails ??= new ContextDetails();
                operation.ContextDetails.Result = payload;
                operation.ContextDetails.Error = null;
                break;

            case OperationTypes.Callback:
                operation.CallbackDetails ??= new CallbackDetails();
                operation.CallbackDetails.Result = payload;
                operation.CallbackDetails.Error = null;
                break;
        }
    }

    private static void ApplyFailDetails(Operation operation, SdkOperationUpdate update)
    {
        var error = MapSdkError(update.Error);
        switch (operation.Type)
        {
            case OperationTypes.Step:
                operation.StepDetails ??= new StepDetails();
                operation.StepDetails.Error = error;
                break;

            case OperationTypes.ChainedInvoke:
                operation.ChainedInvokeDetails ??= new ChainedInvokeDetails();
                operation.ChainedInvokeDetails.Error = error;
                break;

            case OperationTypes.Context:
                operation.ContextDetails ??= new ContextDetails();
                operation.ContextDetails.Error = error;
                break;

            case OperationTypes.Callback:
                operation.CallbackDetails ??= new CallbackDetails();
                operation.CallbackDetails.Error = error;
                break;
        }
    }

    private static void ApplyRetryDetails(Operation operation, SdkOperationUpdate update)
    {
        if (operation.Type == OperationTypes.Step)
        {
            operation.StepDetails ??= new StepDetails();
            if (update.StepOptions?.NextAttemptDelaySeconds is { } delaySeconds)
            {
                operation.StepDetails.NextAttemptTimestamp =
                    DateTimeOffset.UtcNow.AddSeconds(delaySeconds).ToUnixTimeMilliseconds();
            }
            operation.StepDetails.Error = MapSdkError(update.Error);
        }

        if (operation.Type == OperationTypes.Wait && operation.SubType == OperationSubTypes.WaitForCondition)
        {
            operation.WaitDetails ??= new WaitDetails();
            if (update.WaitOptions?.WaitSeconds is { } waitSeconds)
            {
                operation.WaitDetails.ScheduledEndTimestamp =
                    DateTimeOffset.UtcNow.AddSeconds(waitSeconds).ToUnixTimeMilliseconds();
            }
        }
    }

    private void ApplyTimeSkipping(Operation operation, string? action)
    {
        if (action == "START" && operation.Type == OperationTypes.Wait)
        {
            operation.Status = OperationStatuses.Succeeded;
            operation.EndTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (operation.WaitDetails != null)
            {
                operation.WaitDetails.ScheduledEndTimestamp =
                    DateTimeOffset.UtcNow.AddMilliseconds(-1).ToUnixTimeMilliseconds();
            }
        }

        if (action == "RETRY" && operation.Type == OperationTypes.Step)
        {
            operation.Status = OperationStatuses.Ready;
            if (operation.StepDetails != null)
            {
                operation.StepDetails.NextAttemptTimestamp =
                    DateTimeOffset.UtcNow.AddMilliseconds(-1).ToUnixTimeMilliseconds();
            }
        }

        if (action == "RETRY" && operation.Type == OperationTypes.Wait
            && operation.SubType == OperationSubTypes.WaitForCondition)
        {
            operation.Status = OperationStatuses.Ready;
            if (operation.WaitDetails != null)
            {
                operation.WaitDetails.ScheduledEndTimestamp =
                    DateTimeOffset.UtcNow.AddMilliseconds(-1).ToUnixTimeMilliseconds();
            }
        }
    }

    private static ErrorObject? MapSdkError(Amazon.Lambda.Model.ErrorObject? sdkError)
    {
        if (sdkError == null) return null;
        return new ErrorObject
        {
            ErrorType = sdkError.ErrorType,
            ErrorMessage = sdkError.ErrorMessage,
            ErrorData = sdkError.ErrorData,
            StackTrace = sdkError.StackTrace
        };
    }
}
