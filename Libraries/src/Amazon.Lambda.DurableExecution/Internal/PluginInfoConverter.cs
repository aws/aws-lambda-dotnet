// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution.Instrumentation;

namespace Amazon.Lambda.DurableExecution.Internal;

/// <summary>Builds the immutable info objects handed to plugins from the SDK's checkpoint model.</summary>
internal static class PluginInfoConverter
{
    internal static OperationInfo ToOperationInfo(Operation op, bool isReplay = false) => new()
    {
        Id = op.Id ?? string.Empty,
        Name = op.Name,
        Type = op.Type ?? string.Empty,
        SubType = op.SubType,
        ParentId = op.ParentId,
        Status = op.Status,
        StartTimestamp = FromEpochMilliseconds(op.StartTimestamp),
        EndTimestamp = FromEpochMilliseconds(op.EndTimestamp),
        Attempt = op.StepDetails?.Attempt,
        IsReplay = isReplay,
    };

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
