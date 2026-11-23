// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.DurableExecution.Instrumentation;
using SdkOperationUpdate = Amazon.Lambda.Model.OperationUpdate;

namespace Amazon.Lambda.DurableExecution.Internal;

/// <summary>
/// Abstract base for durable operations (Step, Wait, ...). Subclasses implement
/// <see cref="StartAsync"/> (no prior checkpoint) and <see cref="ReplayAsync"/>
/// (some checkpoint exists); the base handles lookup and dispatch.
/// </summary>
/// <typeparam name="TResult">The operation's result type.</typeparam>
internal abstract class DurableOperation<TResult>
{
    protected readonly ExecutionState State;
    protected readonly TerminationManager Termination;
    protected readonly string OperationId;
    protected readonly string? Name;
    protected readonly string? ParentId;
    protected readonly string DurableExecutionArn;
    protected readonly CheckpointBatcher? Batcher;

    protected DurableOperation(
        string operationId,
        string? name,
        string? parentId,
        ExecutionState state,
        TerminationManager termination,
        string durableExecutionArn,
        CheckpointBatcher? batcher = null)
    {
        OperationId = operationId;
        Name = name;
        ParentId = parentId;
        State = state;
        Termination = termination;
        DurableExecutionArn = durableExecutionArn;
        Batcher = batcher;
    }

    protected abstract string OperationType { get; }

    /// <summary>The checkpoint sub-type, reported to instrumentation plugins. Null when the operation has none.</summary>
    protected virtual string? OperationSubType => null;

    /// <summary>
    /// Whether the operation is finished when <see cref="ExecuteAsync"/> returns. False for an operation that
    /// hands back a handle and finishes later (a callback), which then calls <see cref="ReportEnd"/> itself.
    /// </summary>
    protected virtual bool EndsWhenExecuteReturns => true;

    /// <summary>
    /// The plugins of this invocation, or null when none is registered. Set by whoever creates the operation,
    /// so the constructors stay unchanged.
    /// </summary>
    internal PluginRunner? Plugins { get; init; }

    public Task<TResult> ExecuteAsync(CancellationToken cancellationToken)
    {
        State.ValidateReplayConsistency(OperationId, OperationType, Name);

        // Record that the workflow has reached this op. If every completed
        // checkpointed op has now been visited, the state flips out of replay.
        State.TrackReplay(OperationId);

        var existing = State.GetOperation(OperationId);
        if (Plugins != null)
            return ExecuteWithPluginsAsync(Plugins, existing, cancellationToken);

        return existing == null
            ? StartAsync(cancellationToken)
            : ReplayAsync(existing, cancellationToken);
    }

    protected abstract Task<TResult> StartAsync(CancellationToken cancellationToken);

    protected abstract Task<TResult> ReplayAsync(Operation existing, CancellationToken cancellationToken);

    protected Task EnqueueAsync(SdkOperationUpdate update, CancellationToken cancellationToken = default)
        => Batcher?.EnqueueAsync(update, cancellationToken) ?? Task.CompletedTask;

    /// <summary>
    /// Runs the operation and reports its start and, when it reaches a terminal state, its end to the plugins.
    /// An operation that suspends the invocation never completes, so it reports no end. Neither does one
    /// cancelled by the workflow or stopped by the invocation deadline: those are not operation outcomes.
    /// </summary>
    private async Task<TResult> ExecuteWithPluginsAsync(
        PluginRunner plugins, Operation? existing, CancellationToken cancellationToken)
    {
        // The workflow reaches an operation that already has a checkpoint when it replays history, or resumes one
        // that an earlier invocation started (a step after a retry, a child context that suspended). The end is a
        // replay only when the operation had already finished.
        var isReplay = existing != null && PluginInfoConverter.IsTerminal(existing.Status);

        plugins.OperationStart(PluginInfoConverter.ToOperationInfo(
            existing, OperationId, Name, OperationType, OperationSubType, ParentId, isReplay: existing != null,
            fallbackStatus: OperationStatuses.Started));

        TResult result;
        try
        {
            result = await (existing == null ? StartAsync(cancellationToken) : ReplayAsync(existing, cancellationToken));
        }
        catch (Exception ex) when (IsOperationFailure(ex))
        {
            if (EndsWhenExecuteReturns)
                plugins.OperationEnd(CreateEndInfo(existing, isReplay, OperationStatuses.Failed, ex));
            throw;
        }

        if (EndsWhenExecuteReturns)
            plugins.OperationEnd(CreateEndInfo(existing, isReplay, OperationStatuses.Succeeded, null));
        return result;
    }

    /// <summary>Reports that the operation reached a terminal state. For operations that finish after they return.</summary>
    protected void ReportEnd(string status, bool isReplay, Exception? exception) =>
        Plugins?.OperationEnd(CreateEndInfo(null, isReplay, status, exception));

    private OperationEndInfo CreateEndInfo(Operation? existing, bool isReplay, string status, Exception? exception)
    {
        // The checkpoint response may have stamped the terminal record with the service's timestamps.
        var latest = State.GetOperation(OperationId) ?? existing;
        return PluginInfoConverter.ToOperationEndInfo(
            latest, OperationId, Name, OperationType, OperationSubType, ParentId, isReplay, status, exception);
    }

    /// <summary>The exceptions that mean the operation itself failed, as opposed to the invocation.</summary>
    private static bool IsOperationFailure(Exception ex) =>
        ex is StepException or ChildContextException or InvokeException or CallbackException or WaitForConditionException;

    /// <summary>
    /// Runs one execution of an operation's user function and reports it to the plugins: attempt start, the
    /// user function inside the plugins' <c>WrapAsync</c>, attempt end. Cancellation is not an attempt outcome.
    /// </summary>
    /// <param name="attemptNumber">The 1-based attempt number.</param>
    /// <param name="body">The user function.</param>
    protected async Task<T> RunAttemptAsync<T>(int attemptNumber, Func<Task<T>> body)
    {
        var plugins = Plugins;
        if (plugins == null)
            return await body();

        var info = new AttemptInfo
        {
            Id = OperationId,
            Name = Name,
            Type = OperationType,
            SubType = OperationSubType,
            ParentId = ParentId,
            Status = OperationStatuses.Started,
            Attempt = attemptNumber,
            IsReplay = false,
        };
        plugins.AttemptStart(info);

        T result = default!;
        try
        {
            await plugins.WrapAsync(
                new UserCodeInfo { Kind = UserCodeKind.Attempt, Operation = info },
                async () => result = await body());
        }
        catch (Exception ex)
        {
            if (ex is not OperationCanceledException)
                plugins.AttemptEnd(CreateAttemptEndInfo(info, AttemptOutcome.Failed, ex));
            throw;
        }

        plugins.AttemptEnd(CreateAttemptEndInfo(info, AttemptOutcome.Succeeded, null));
        return result;
    }

    private static AttemptEndInfo CreateAttemptEndInfo(AttemptInfo started, AttemptOutcome outcome, Exception? exception) => new()
    {
        Id = started.Id,
        Name = started.Name,
        Type = started.Type,
        SubType = started.SubType,
        ParentId = started.ParentId,
        Status = outcome == AttemptOutcome.Succeeded ? OperationStatuses.Succeeded : OperationStatuses.Failed,
        Attempt = started.Attempt,
        IsReplay = false,
        Outcome = outcome,
        Exception = exception,
    };

    /// <summary>
    /// Runs a child context's function inside the plugins' <c>WrapAsync</c> so their ambient context is in
    /// effect while it runs.
    /// </summary>
    protected Task<T> RunChildContextAsync<T>(Func<Task<T>> body)
    {
        var plugins = Plugins;
        if (plugins == null)
            return body();

        return WrapChildContextAsync(plugins, body);
    }

    private async Task<T> WrapChildContextAsync<T>(PluginRunner plugins, Func<Task<T>> body)
    {
        T result = default!;
        await plugins.WrapAsync(
            new UserCodeInfo
            {
                Kind = UserCodeKind.ChildContext,
                Operation = PluginInfoConverter.ToOperationInfo(
                    State.GetOperation(OperationId), OperationId, Name, OperationType, OperationSubType, ParentId,
                    isReplay: false, fallbackStatus: OperationStatuses.Started),
            },
            async () => result = await body());
        return result;
    }
}
