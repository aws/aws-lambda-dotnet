// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

namespace Amazon.Lambda.DurableExecution.LocalEmulation;

/// <summary>
/// Processes checkpoint updates against the in-memory operation store. Handles action-to-status
/// mapping, callback ID minting, time skipping, and producing the "new operations" the runtime
/// expects back in the checkpoint response.
/// </summary>
/// <remarks>
/// This is the shared local-emulation state machine used by both the durable-execution testing
/// package (driving the workflow as an in-process delegate) and the Lambda Test Tool (driving a
/// real, separately-running function over the Runtime API + HTTP data plane). Both feed it the
/// same transport-neutral <see cref="OperationUpdateInput"/>; the state transitions below are the
/// single source of truth for how a local checkpoint mutates recorded operations.
/// </remarks>
internal sealed class CheckpointProcessor
{
    private readonly InMemoryOperationStore _store;
    // Read on each checkpoint (not captured) so a consumer's time-skip toggle can be flipped at
    // runtime — e.g. the Test Tool's UI switch — and take effect for subsequent checkpoints.
    private readonly Func<bool> _skipTimeProvider;
    private readonly object _pendingGate = new();
    private readonly List<PendingInvoke> _pendingInvokes = new();

    /// <summary>Creates a processor whose time-skip mode is fixed for the run.</summary>
    public CheckpointProcessor(InMemoryOperationStore store, bool skipTime)
        : this(store, () => skipTime)
    {
    }

    /// <summary>Creates a processor whose time-skip mode is read live on each checkpoint.</summary>
    public CheckpointProcessor(InMemoryOperationStore store, Func<bool> skipTimeProvider)
    {
        _store = store;
        _skipTimeProvider = skipTimeProvider;
    }

    /// <summary>
    /// A chained-invoke (<c>ctx.InvokeAsync</c>) that has been started by the workflow but not yet
    /// resolved. The runtime suspends after emitting the START and expects an external system to
    /// run the target function; the local drivers drain these between invocations and resolve them
    /// (the testing package via its function registry, the Test Tool by starting a nested durable
    /// execution). The target function name lives only on the update, not on the persisted
    /// <see cref="Operation"/>, so it is captured here.
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
        IReadOnlyList<OperationUpdateInput> updates)
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

    private Operation ApplyUpdate(string arn, OperationUpdateInput update)
    {
        var existing = _store.GetOperation(arn, update.Id!);
        var operation = existing ?? new Operation { Id = update.Id };

        operation.Type = update.Type ?? operation.Type;
        operation.Name = update.Name ?? operation.Name;
        operation.ParentId = update.ParentId ?? operation.ParentId;
        operation.SubType = update.SubType ?? operation.SubType;

        var action = update.Action;
        ApplyAction(operation, action, update);

        if (_skipTimeProvider())
            ApplyTimeSkipping(operation, action);

        // A chained-invoke START suspends the workflow until an external system resolves it.
        // Record it so a driver can run the sibling and stamp the result/error before the next
        // replay. The function name is only carried on the update, not on the Operation.
        if (action == "START"
            && operation.Type == OperationTypes.ChainedInvoke
            && update.ChainedInvokeFunctionName is { } functionName)
        {
            lock (_pendingGate)
            {
                _pendingInvokes.Add(new PendingInvoke(operation.Id!, functionName, update.Payload));
            }
        }

        _store.Upsert(arn, operation);
        return operation;
    }

    private static void ApplyAction(Operation operation, string? action, OperationUpdateInput update)
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

    private static void ApplyStartDetails(Operation operation, OperationUpdateInput update)
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
                if (update.WaitSeconds is { } seconds)
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

    private static void ApplySucceedDetails(Operation operation, OperationUpdateInput update)
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

    private static void ApplyFailDetails(Operation operation, OperationUpdateInput update)
    {
        var error = update.Error;
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

    private static void ApplyRetryDetails(Operation operation, OperationUpdateInput update)
    {
        // Both retried steps and WaitForCondition polls are wire-encoded as Type=STEP
        // (WaitForCondition uses SubType=WaitForCondition); the runtime never emits a
        // WAIT-typed RETRY, so this single STEP branch covers both.
        if (operation.Type == OperationTypes.Step)
        {
            operation.StepDetails ??= new StepDetails();
            if (update.NextAttemptDelaySeconds is { } delaySeconds)
            {
                operation.StepDetails.NextAttemptTimestamp =
                    DateTimeOffset.UtcNow.AddSeconds(delaySeconds).ToUnixTimeMilliseconds();
            }
            operation.StepDetails.Error = update.Error;

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
}
