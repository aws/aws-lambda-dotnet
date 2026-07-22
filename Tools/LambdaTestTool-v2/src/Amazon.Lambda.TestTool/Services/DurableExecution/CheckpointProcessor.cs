// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.DurableExecution;

namespace Amazon.Lambda.TestTool.Services.DurableExecution;

/// <summary>
/// Processes checkpoint updates against the in-memory operation store. Handles
/// action-to-status mapping, callback ID minting, time skipping, and producing the
/// "new operations" the runtime expects back in the checkpoint response.
/// </summary>
/// <remarks>
/// Ported from <c>Amazon.Lambda.DurableExecution.Testing.CheckpointProcessor</c> (which is
/// <c>internal</c>). The state-machine logic is intentionally identical; the only change is
/// the input type — a plain-STJ <see cref="WireOperationUpdate"/> deserialized from the wire,
/// instead of the AWSSDK <c>Amazon.Lambda.Model.OperationUpdate</c> whose ConstantClass
/// members can't be deserialized by System.Text.Json. This drops the <c>?.Value</c> access on
/// the enum-like members but leaves every branch and status transition unchanged.
/// </remarks>
internal sealed class CheckpointProcessor
{
    private readonly InMemoryOperationStore _store;
    private readonly bool _skipTime;
    private readonly object _pendingGate = new();
    private readonly List<PendingInvoke> _pendingInvokes = new();

    public CheckpointProcessor(InMemoryOperationStore store, bool skipTime)
    {
        _store = store;
        _skipTime = skipTime;
    }

    /// <summary>
    /// A chained-invoke (<c>ctx.InvokeAsync</c>) started by the workflow but not yet resolved.
    /// The runtime suspends after emitting the START and expects an external system to run the
    /// target function. Phase 1 records these but does not resolve them (chained durable
    /// invokes are a Phase 4 feature); the driver detects a non-empty pending list and fails
    /// fast. The target function name lives only on the wire-format
    /// <c>ChainedInvokeOptions</c>, so it is captured here rather than on the persisted
    /// <see cref="Operation"/>.
    /// </summary>
    internal readonly record struct PendingInvoke(string OperationId, string FunctionName, string? Payload);

    /// <summary>Returns and clears the chained-invokes started since the last drain.</summary>
    public IReadOnlyList<PendingInvoke> DrainPendingInvokes()
    {
        lock (_pendingGate)
        {
            if (_pendingInvokes.Count == 0)
                return Array.Empty<PendingInvoke>();
            var drained = _pendingInvokes.ToArray();
            _pendingInvokes.Clear();
            return drained;
        }
    }

    /// <summary>
    /// Processes a batch of updates and returns the new checkpoint token and any operations
    /// created or modified (to feed back to the runtime's onNewOperations mechanism).
    /// </summary>
    public (string NewToken, IReadOnlyList<Operation> NewOperations) Process(
        string arn,
        string? currentToken,
        IReadOnlyList<WireOperationUpdate> updates)
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

    private Operation ApplyUpdate(string arn, WireOperationUpdate update)
    {
        var existing = _store.GetOperation(arn, update.Id!);
        var operation = existing ?? new Operation { Id = update.Id };

        operation.Type = update.Type ?? operation.Type;
        operation.Name = update.Name ?? operation.Name;
        operation.ParentId = update.ParentId ?? operation.ParentId;
        operation.SubType = update.SubType ?? operation.SubType;

        var action = update.Action;
        ApplyAction(operation, action, update);

        if (_skipTime)
            ApplyTimeSkipping(operation, action);

        // A chained-invoke START suspends the workflow until an external system resolves it.
        // Record it so a future driver can run the registered sibling and stamp the
        // result/error before the next replay. The function name is only carried on the
        // wire-format update, not on the Operation.
        if (action == "START"
            && operation.Type == OperationTypes.ChainedInvoke
            && update.ChainedInvokeOptions?.FunctionName is { } functionName)
        {
            lock (_pendingGate)
            {
                _pendingInvokes.Add(new PendingInvoke(operation.Id!, functionName, update.Payload));
            }
        }

        _store.Upsert(arn, operation);
        return operation;
    }

    private static void ApplyAction(Operation operation, string? action, WireOperationUpdate update)
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

    private static void ApplyStartDetails(Operation operation, WireOperationUpdate update)
    {
        switch (operation.Type)
        {
            case OperationTypes.Step:
                operation.StepDetails ??= new StepDetails();
                // A plain step re-emits START before every attempt, so START owns the attempt
                // count. WaitForCondition (Type=STEP, SubType=WaitForCondition) emits START
                // only once and advances the count on each RETRY instead, so it must NOT
                // increment here.
                if (operation.SubType != OperationSubTypes.WaitForCondition)
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

    private static void ApplySucceedDetails(Operation operation, WireOperationUpdate update)
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

    private static void ApplyFailDetails(Operation operation, WireOperationUpdate update)
    {
        var error = MapWireError(update.Error);
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

    private static void ApplyRetryDetails(Operation operation, WireOperationUpdate update)
    {
        // Both retried steps and WaitForCondition polls are wire-encoded as Type=STEP
        // (WaitForCondition uses SubType=WaitForCondition); the runtime never emits a
        // WAIT-typed RETRY, so this single STEP branch covers both.
        if (operation.Type == OperationTypes.Step)
        {
            operation.StepDetails ??= new StepDetails();
            if (update.StepOptions?.NextAttemptDelaySeconds is { } delaySeconds)
            {
                operation.StepDetails.NextAttemptTimestamp =
                    DateTimeOffset.UtcNow.AddSeconds(delaySeconds).ToUnixTimeMilliseconds();
            }
            operation.StepDetails.Error = MapWireError(update.Error);

            // WaitForCondition emits START once and advances per RETRY: it carries the next
            // poll state in Payload and relies on the persistence layer to own the attempt
            // count. Persist both so the next replay resumes from the latest state with an
            // advanced attempt number (a plain step RETRY carries no Payload and owns its count
            // via START, so leave it alone).
            if (operation.SubType == OperationSubTypes.WaitForCondition)
            {
                if (update.Payload is not null)
                    operation.StepDetails.Result = update.Payload;
                operation.StepDetails.Attempt = (operation.StepDetails.Attempt ?? 0) + 1;
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

        // A retried step (or WaitForCondition poll, also Type=STEP) becomes immediately READY
        // under time-skipping so the next replay runs the next attempt without waiting for the
        // backoff/poll delay.
        if (action == "RETRY" && operation.Type == OperationTypes.Step)
        {
            operation.Status = OperationStatuses.Ready;
            if (operation.StepDetails != null)
            {
                operation.StepDetails.NextAttemptTimestamp =
                    DateTimeOffset.UtcNow.AddMilliseconds(-1).ToUnixTimeMilliseconds();
            }
        }
    }

    private static ErrorObject? MapWireError(WireErrorObject? wireError)
    {
        if (wireError == null) return null;
        return new ErrorObject
        {
            ErrorType = wireError.ErrorType,
            ErrorMessage = wireError.ErrorMessage,
            ErrorData = wireError.ErrorData,
            StackTrace = wireError.StackTrace
        };
    }
}
