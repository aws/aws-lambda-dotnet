// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

namespace Amazon.Lambda.DurableExecution.LocalEmulation;

/// <summary>
/// A transport-neutral checkpoint update consumed by <see cref="CheckpointProcessor"/>.
/// </summary>
/// <remarks>
/// The two local-emulation consumers receive checkpoint updates in different shapes:
/// the testing package gets the AWSSDK <c>Amazon.Lambda.Model.OperationUpdate</c> (whose
/// Type/Action/SubType are <c>ConstantClass</c> values), and the Test Tool gets a plain-STJ
/// wire DTO deserialized from the HTTP data plane. Rather than make the state machine generic
/// over those two shapes, each consumer flattens its own representation into this one record at
/// the boundary — unwrapping enum-like members to plain strings and mapping nested option/error
/// types up front. That keeps every branch of the shared state machine identical and free of
/// <c>?.Value</c> ceremony.
///
/// Only the members the checkpoint state machine actually reads are carried here; option groups
/// are flattened to their single relevant field (e.g. <see cref="WaitSeconds"/> from
/// <c>WaitOptions</c>).
/// </remarks>
internal sealed class OperationUpdateInput
{
    /// <summary>Operation id the update targets.</summary>
    public string? Id { get; init; }

    /// <summary>Parent operation id (null for top-level operations).</summary>
    public string? ParentId { get; init; }

    /// <summary>Human-readable operation name.</summary>
    public string? Name { get; init; }

    /// <summary>Operation type — compared against <c>OperationTypes</c> constants.</summary>
    public string? Type { get; init; }

    /// <summary>Operation sub-type — compared against <c>OperationSubTypes</c> constants.</summary>
    public string? SubType { get; init; }

    /// <summary>Lifecycle action: <c>START</c>, <c>SUCCEED</c>, <c>FAIL</c>, <c>RETRY</c>, or <c>CANCEL</c>.</summary>
    public string? Action { get; init; }

    /// <summary>Serialized result / carried state, depending on the action and operation type.</summary>
    public string? Payload { get; init; }

    /// <summary>Failure detail for a <c>FAIL</c>/<c>RETRY</c> action.</summary>
    public ErrorObject? Error { get; init; }

    /// <summary>Retry backoff (from <c>StepOptions.NextAttemptDelaySeconds</c>) for a STEP <c>RETRY</c>.</summary>
    public int? NextAttemptDelaySeconds { get; init; }

    /// <summary>Timer duration (from <c>WaitOptions.WaitSeconds</c>) for a WAIT <c>START</c>.</summary>
    public long? WaitSeconds { get; init; }

    /// <summary>
    /// Target function of a <c>CHAINED_INVOKE</c> START (from <c>ChainedInvokeOptions.FunctionName</c>).
    /// Carried on the update only — never persisted on the <see cref="Operation"/> — so the driver
    /// captures it here to resolve the sibling.
    /// </summary>
    public string? ChainedInvokeFunctionName { get; init; }
}
