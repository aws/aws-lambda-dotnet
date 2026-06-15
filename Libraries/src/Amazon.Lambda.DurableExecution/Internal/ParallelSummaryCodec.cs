// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;

namespace Amazon.Lambda.DurableExecution.Internal;

/// <summary>
/// Owns the on-the-wire representation of a parallel parent's CONTEXT payload:
/// the <see cref="ParallelSummary"/> JSON and the enum↔string mappings for
/// per-branch status and completion reason. Keeping every string literal and the
/// JSON (de)serialisation behind this one type means <see cref="ParallelOperation{T}"/>
/// deals only in domain enums, and the wire contract has a single owner.
/// </summary>
internal static class ParallelSummaryCodec
{
    /// <summary>Builds the summary snapshot for a settled batch.</summary>
    public static ParallelSummary Build<T>(IReadOnlyList<IBatchItem<T>> items, CompletionReason completionReason)
    {
        var summary = new ParallelSummary
        {
            CompletionReason = WriteCompletionReason(completionReason),
            Branches = new List<ParallelBranchSummary>(items.Count)
        };
        foreach (var item in items)
        {
            summary.Branches.Add(new ParallelBranchSummary
            {
                Index = item.Index,
                Name = item.Name,
                Status = WriteStatus(item.Status)
            });
        }
        return summary;
    }

    public static string ToPayload(ParallelSummary summary)
        => JsonSerializer.Serialize(summary, ParallelJsonContext.Default.ParallelSummary);

    /// <summary>
    /// Parses a checkpointed payload. Returns <c>null</c> for empty, older, or
    /// corrupted payloads so the caller can fall back to inferring per-branch
    /// status from the children's own checkpoints.
    /// </summary>
    public static ParallelSummary? FromPayload(string? payload)
    {
        if (string.IsNullOrEmpty(payload)) return null;
        try
        {
            return JsonSerializer.Deserialize(payload, ParallelJsonContext.Default.ParallelSummary);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static string WriteStatus(BatchItemStatus status) => status switch
    {
        BatchItemStatus.Succeeded => "SUCCEEDED",
        BatchItemStatus.Failed    => "FAILED",
        BatchItemStatus.Started   => "STARTED",
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };

    public static BatchItemStatus ReadStatus(string? wire) => wire switch
    {
        "SUCCEEDED" => BatchItemStatus.Succeeded,
        "FAILED"    => BatchItemStatus.Failed,
        "STARTED"   => BatchItemStatus.Started,
        _           => BatchItemStatus.Started
    };

    public static string WriteCompletionReason(CompletionReason reason) => reason switch
    {
        CompletionReason.AllCompleted             => "ALL_COMPLETED",
        CompletionReason.MinSuccessfulReached     => "MIN_SUCCESSFUL_REACHED",
        CompletionReason.FailureToleranceExceeded => "FAILURE_TOLERANCE_EXCEEDED",
        _ => throw new ArgumentOutOfRangeException(nameof(reason))
    };

    public static CompletionReason ReadCompletionReason(string? wire) => wire switch
    {
        "ALL_COMPLETED"              => CompletionReason.AllCompleted,
        "MIN_SUCCESSFUL_REACHED"     => CompletionReason.MinSuccessfulReached,
        "FAILURE_TOLERANCE_EXCEEDED" => CompletionReason.FailureToleranceExceeded,
        _                            => CompletionReason.AllCompleted
    };
}
