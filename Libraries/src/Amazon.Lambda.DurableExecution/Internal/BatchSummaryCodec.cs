// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using System.Text.Json;

namespace Amazon.Lambda.DurableExecution.Internal;

/// <summary>
/// Shared (de)serialization primitives for the <see cref="BatchSummary"/> payload
/// stored on a concurrent operation's parent CONTEXT checkpoint. Centralising the
/// wire-string mappings and the payload serialize / overflow check here keeps the
/// batch (<see cref="ConcurrentOperation{T}"/>) and incremental
/// (<see cref="IncrementalParallelOperation"/>) parallel implementations in exact
/// agreement on the on-the-wire format, so a checkpoint written by one is
/// reconstructable by the other.
/// </summary>
internal static class BatchSummaryCodec
{
    /// <summary>
    /// Serializes the summary to its JSON payload using the source-generated
    /// <see cref="BatchJsonContext"/> (trim/AOT safe).
    /// </summary>
    public static string ToPayload(BatchSummary summary)
        => JsonSerializer.Serialize(summary, BatchJsonContext.Default.BatchSummary);

    /// <summary>
    /// True when <paramref name="payload"/> exceeds the per-operation checkpoint
    /// byte limit and must be re-emitted stripped (statuses only) with
    /// <c>ReplayChildren=true</c>.
    /// </summary>
    public static bool IsOverflow(string payload)
        => Encoding.UTF8.GetByteCount(payload) > DurableConstants.MaxOperationCheckpointBytes;

    /// <summary>
    /// Deserializes a <see cref="BatchSummary"/> from a checkpoint payload,
    /// tolerating null/empty/corrupt payloads by returning <c>null</c> (callers
    /// fall back to inferring per-unit status from child checkpoints).
    /// </summary>
    public static BatchSummary? ParseSummary(string? payload)
    {
        if (string.IsNullOrEmpty(payload)) return null;
        try
        {
            return JsonSerializer.Deserialize(payload, BatchJsonContext.Default.BatchSummary);
        }
        catch (JsonException)
        {
            // Tolerate older / corrupted payloads — fall back to inferring status
            // from per-unit checkpoints.
            return null;
        }
    }

    public static string SerializeStatus(BatchItemStatus status) => status switch
    {
        BatchItemStatus.Succeeded => "SUCCEEDED",
        BatchItemStatus.Failed    => "FAILED",
        BatchItemStatus.Started   => "STARTED",
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };

    public static BatchItemStatus DeserializeStatus(string? wire) => wire switch
    {
        "SUCCEEDED" => BatchItemStatus.Succeeded,
        "FAILED"    => BatchItemStatus.Failed,
        "STARTED"   => BatchItemStatus.Started,
        _           => BatchItemStatus.Started
    };

    public static string SerializeCompletionReason(CompletionReason reason) => reason switch
    {
        CompletionReason.AllCompleted             => "ALL_COMPLETED",
        CompletionReason.MinSuccessfulReached     => "MIN_SUCCESSFUL_REACHED",
        CompletionReason.FailureToleranceExceeded => "FAILURE_TOLERANCE_EXCEEDED",
        _ => throw new ArgumentOutOfRangeException(nameof(reason))
    };

    public static CompletionReason DeserializeCompletionReason(string? wire) => wire switch
    {
        "ALL_COMPLETED"              => CompletionReason.AllCompleted,
        "MIN_SUCCESSFUL_REACHED"     => CompletionReason.MinSuccessfulReached,
        "FAILURE_TOLERANCE_EXCEEDED" => CompletionReason.FailureToleranceExceeded,
        _                            => CompletionReason.AllCompleted
    };
}
