// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution.Instrumentation;

namespace Amazon.Lambda.DurableExecution.Internal;

/// <summary>Builds the immutable info objects handed to plugins from the SDK's checkpoint model.</summary>
internal static class PluginInfoConverter
{
    internal static bool IsTerminal(string? status) =>
        status == OperationStatuses.Succeeded
        || status == OperationStatuses.Failed
        || status == OperationStatuses.Cancelled
        || status == OperationStatuses.Stopped;

    /// <summary>Describes a checkpointed operation, for the snapshots in invocation and change info.</summary>
    internal static OperationInfo ToOperationInfo(Operation op) =>
        ToOperationInfo(op, op.Id ?? string.Empty, op.Name, op.Type ?? string.Empty, op.SubType, op.ParentId,
            isReplay: false, fallbackStatus: null);

    /// <summary>
    /// Describes an operation the workflow is running. <paramref name="existing"/> is its checkpoint record, if
    /// it has one; the identity arguments are what the SDK knows even before the first checkpoint.
    /// </summary>
    internal static OperationInfo ToOperationInfo(
        Operation? existing, string id, string? name, string type, string? subType, string? parentId,
        bool isReplay, string? fallbackStatus) => new()
    {
        Id = id,
        Name = name ?? existing?.Name,
        Type = type,
        SubType = subType ?? existing?.SubType,
        ParentId = parentId,
        Status = existing?.Status ?? fallbackStatus,
        StartTimestamp = FromEpochMilliseconds(existing?.StartTimestamp),
        EndTimestamp = existing != null && IsTerminal(existing.Status) ? FromEpochMilliseconds(existing.EndTimestamp) : null,
        Attempt = AttemptOf(type, existing),
        IsReplay = isReplay,
    };

    /// <summary>Describes an operation that reached a terminal state with the given outcome.</summary>
    internal static OperationEndInfo ToOperationEndInfo(
        Operation? latest, string id, string? name, string type, string? subType, string? parentId,
        bool isReplay, string status, Exception? exception) => new()
    {
        Id = id,
        Name = name ?? latest?.Name,
        Type = type,
        SubType = subType ?? latest?.SubType,
        ParentId = parentId,
        Status = status,
        StartTimestamp = FromEpochMilliseconds(latest?.StartTimestamp),
        // Only the service's own end time. A plugin that needs one when it is missing reads its own clock.
        EndTimestamp = latest != null && IsTerminal(latest.Status) ? FromEpochMilliseconds(latest.EndTimestamp) : null,
        Attempt = AttemptOf(type, latest),
        IsReplay = isReplay,
        Error = exception != null ? ErrorObject.FromException(exception) : null,
        Exception = isReplay ? null : exception,
    };

    /// <summary>
    /// The 1-based number of the latest attempt of a step. The service records how many attempts came before
    /// (<c>StepDetails.Attempt</c>), and the SDK numbers the next one from it.
    /// </summary>
    private static int? AttemptOf(string type, Operation? op) =>
        type == OperationTypes.Step ? (op?.StepDetails?.Attempt ?? 0) + 1 : null;

    internal static IReadOnlyDictionary<string, OperationInfo> ToOperationMap(IEnumerable<Operation> operations, ISet<string>? onlyIds = null)
    {
        var map = new Dictionary<string, OperationInfo>(StringComparer.Ordinal);
        foreach (var op in operations)
        {
            if (op.Id == null || (onlyIds != null && !onlyIds.Contains(op.Id)))
                continue;
            map[op.Id] = ToOperationInfo(op);
        }
        return map;
    }

    internal static InvocationInfo ToInvocationInfo(
        DurableExecutionInvocationInput input,
        ExecutionState state,
        ILambdaContext lambdaContext,
        bool isMultiConcurrency)
    {
        var operations = state.GetAllOperations();

        // The EXECUTION operation only carries the input, so an execution with nothing else checkpointed is on
        // its first invocation.
        var hasHistory = false;
        DateTimeOffset? executionStart = null;
        foreach (var op in operations)
        {
            if (op.Type == OperationTypes.Execution)
                executionStart ??= FromEpochMilliseconds(op.StartTimestamp);
            else
                hasHistory = true;
        }

        ISet<string>? updatedIds = input.UpdatedOperationIds is { Count: > 0 } ids
            ? new HashSet<string>(ids, StringComparer.Ordinal)
            : null;

        return new InvocationInfo
        {
            ExecutionArn = input.DurableExecutionArn,
            RequestId = lambdaContext.AwsRequestId,
            IsFirstInvocation = !hasHistory,
            ExecutionStartTimestamp = executionStart,
            XRayTraceId = ResolveTraceId(lambdaContext, isMultiConcurrency),
            Operations = ToOperationMap(operations),
            UpdatedOperations = updatedIds == null
                ? EmptyOperations.Instance
                : ToOperationMap(operations, updatedIds),
        };
    }

    /// <summary>
    /// The trace header of this invocation. <c>ILambdaContext.TraceId</c> is per invocation. The environment
    /// variable is only a fallback for runtimes that do not fill it in, and only where it is safe: on Lambda
    /// Managed Instances several invocations share the process, so it does not identify this one.
    /// </summary>
    private static string? ResolveTraceId(ILambdaContext lambdaContext, bool isMultiConcurrency)
    {
        var traceId = lambdaContext.TraceId;
        if (!string.IsNullOrEmpty(traceId))
            return traceId;

        if (isMultiConcurrency)
            return null;

        var fromEnvironment = Environment.GetEnvironmentVariable("_X_AMZN_TRACE_ID");
        return string.IsNullOrEmpty(fromEnvironment) ? null : fromEnvironment;
    }

    private static DateTimeOffset? FromEpochMilliseconds(long? value) =>
        value is { } ms ? DateTimeOffset.FromUnixTimeMilliseconds(ms) : null;
}
