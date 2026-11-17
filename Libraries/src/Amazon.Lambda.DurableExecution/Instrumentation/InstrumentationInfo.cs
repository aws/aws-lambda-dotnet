// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

namespace Amazon.Lambda.DurableExecution.Instrumentation;

/// <summary>Details of an invocation when it starts.</summary>
public sealed class InvocationInfo
{
    /// <summary>ARN of the durable execution.</summary>
    public required string ExecutionArn { get; init; }

    /// <summary>The Lambda request id of this invocation.</summary>
    public string? RequestId { get; init; }

    /// <summary>True when the execution has no checkpointed history, apart from its input.</summary>
    public bool IsFirstInvocation { get; init; }

    /// <summary>When the durable execution started, as recorded by the service.</summary>
    public DateTimeOffset? ExecutionStartTimestamp { get; init; }

    /// <summary>
    /// The X-Ray trace header of this invocation (<c>ILambdaContext.TraceId</c>), or null when the runtime
    /// provides none.
    /// </summary>
    public string? XRayTraceId { get; init; }

    /// <summary>Every operation known at the start of the invocation, by operation id.</summary>
    public IReadOnlyDictionary<string, OperationInfo> Operations { get; init; } = EmptyOperations.Instance;

    /// <summary>The operations the service reports as changed since the previous invocation, by operation id.</summary>
    public IReadOnlyDictionary<string, OperationInfo> UpdatedOperations { get; init; } = EmptyOperations.Instance;
}

/// <summary>How an invocation ended.</summary>
public enum InvocationEndStatus
{
    /// <summary>The workflow returned a result.</summary>
    Succeeded,

    /// <summary>The workflow failed permanently.</summary>
    Failed,

    /// <summary>The invocation suspended and the service will invoke the function again.</summary>
    Pending,

    /// <summary>
    /// The invocation ended with an exception that is not a workflow failure, such as a missed deadline or a
    /// transient checkpoint error, so Lambda will retry it.
    /// </summary>
    Retrying,
}

/// <summary>Details of an invocation when it ends.</summary>
public sealed class InvocationEndInfo
{
    /// <summary>ARN of the durable execution.</summary>
    public required string ExecutionArn { get; init; }

    /// <summary>The Lambda request id of this invocation.</summary>
    public string? RequestId { get; init; }

    /// <summary>How the invocation ended.</summary>
    public required InvocationEndStatus Status { get; init; }

    /// <summary>The workflow failure or the exception behind <see cref="InvocationEndStatus.Retrying"/>, if any.</summary>
    public Exception? Exception { get; init; }

    /// <summary>True when the execution has no checkpointed history, apart from its input.</summary>
    public bool IsFirstInvocation { get; init; }

    /// <summary>When the durable execution started, as recorded by the service.</summary>
    public DateTimeOffset? ExecutionStartTimestamp { get; init; }

    /// <summary>Every operation known at the end of the invocation, by operation id.</summary>
    public IReadOnlyDictionary<string, OperationInfo> Operations { get; init; } = EmptyOperations.Instance;
}

/// <summary>A durable operation.</summary>
public class OperationInfo
{
    /// <summary>The deterministic operation id.</summary>
    public required string Id { get; init; }

    /// <summary>The operation name given by the workflow, if any.</summary>
    public string? Name { get; init; }

    /// <summary>The operation type, one of <see cref="OperationTypes"/>.</summary>
    public required string Type { get; init; }

    /// <summary>The operation sub-type, one of <see cref="OperationSubTypes"/>, if any.</summary>
    public string? SubType { get; init; }

    /// <summary>The id of the enclosing child-context operation, or null at the top level.</summary>
    public string? ParentId { get; init; }

    /// <summary>The checkpointed status, one of <see cref="OperationStatuses"/>, if known.</summary>
    public string? Status { get; init; }

    /// <summary>When the operation started, as recorded by the service, if known.</summary>
    public DateTimeOffset? StartTimestamp { get; init; }

    /// <summary>When the operation ended, as recorded by the service, if known.</summary>
    public DateTimeOffset? EndTimestamp { get; init; }

    /// <summary>The attempt number (1-based) for operations that retry, if applicable.</summary>
    public int? Attempt { get; init; }

    /// <summary>True when the workflow reached this operation by replaying checkpointed history.</summary>
    public bool IsReplay { get; init; }
}

/// <summary>An operation that reached a terminal state.</summary>
public sealed class OperationEndInfo : OperationInfo
{
    /// <summary>The error recorded for the operation, if it failed.</summary>
    public ErrorObject? Error { get; init; }

    /// <summary>The exception observed in this invocation, if any. Null for operations replayed from history.</summary>
    public Exception? Exception { get; init; }
}

/// <summary>One execution of the user function of an operation. <see cref="OperationInfo.Attempt"/> is set.</summary>
public class AttemptInfo : OperationInfo
{
}

/// <summary>How an attempt ended.</summary>
public enum AttemptOutcome
{
    /// <summary>The user function returned.</summary>
    Succeeded,

    /// <summary>The user function threw.</summary>
    Failed,
}

/// <summary>An attempt that finished.</summary>
public sealed class AttemptEndInfo : AttemptInfo
{
    /// <summary>How the attempt ended.</summary>
    public required AttemptOutcome Outcome { get; init; }

    /// <summary>The exception thrown by the user function, if the attempt failed.</summary>
    public Exception? Exception { get; init; }
}

/// <summary>Operations whose state changed because of a checkpoint response.</summary>
public sealed class OperationChangeInfo
{
    /// <summary>ARN of the durable execution.</summary>
    public required string ExecutionArn { get; init; }

    /// <summary>The operations that changed, by operation id.</summary>
    public required IReadOnlyDictionary<string, OperationInfo> UpdatedOperations { get; init; }

    /// <summary>Every operation known after the change, by operation id.</summary>
    public required IReadOnlyDictionary<string, OperationInfo> Operations { get; init; }
}

/// <summary>What kind of user code <see cref="IDurableInstrumentationPlugin.WrapAsync"/> is wrapping.</summary>
public enum UserCodeKind
{
    /// <summary>The whole workflow function of the invocation.</summary>
    Invocation,

    /// <summary>The function of a child context (including map and parallel branches).</summary>
    ChildContext,

    /// <summary>One attempt of an operation's user function, such as a step.</summary>
    Attempt,
}

/// <summary>Identifies the user code being wrapped.</summary>
public sealed class UserCodeInfo
{
    /// <summary>What is being wrapped.</summary>
    public required UserCodeKind Kind { get; init; }

    /// <summary>The operation the code belongs to. Null for <see cref="UserCodeKind.Invocation"/>.</summary>
    public OperationInfo? Operation { get; init; }
}

/// <summary>Identifies a chained invoke for which trace headers are requested.</summary>
public sealed class PropagationInput
{
    /// <summary>ARN of the calling durable execution.</summary>
    public required string ExecutionArn { get; init; }

    /// <summary>Id of the chained invoke operation.</summary>
    public required string OperationId { get; init; }

    /// <summary>Id of the enclosing child context of the invoke, if any.</summary>
    public string? ParentOperationId { get; init; }

    /// <summary>The function being invoked.</summary>
    public required string TargetFunctionName { get; init; }
}

internal static class EmptyOperations
{
    internal static readonly IReadOnlyDictionary<string, OperationInfo> Instance =
        new Dictionary<string, OperationInfo>(0);
}
