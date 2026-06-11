// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Serialization;

namespace Amazon.Lambda.DurableExecution.Internal;

/// <summary>
/// Internal payload shape stored on a parallel parent's CONTEXT checkpoint
/// (as <c>ContextDetails.Result</c>) and reconstructed on replay. Carries the
/// completion reason and the per-branch index → status map so the
/// <see cref="IBatchResult{T}"/> can be rebuilt without depending on user T
/// shape — per-branch results live on the children's own checkpoints.
/// </summary>
internal sealed class ParallelSummary
{
    [JsonPropertyName("CompletionReason")]
    public string? CompletionReason { get; set; }

    [JsonPropertyName("Branches")]
    public IList<ParallelBranchSummary> Branches { get; set; } = new List<ParallelBranchSummary>();
}

internal sealed class ParallelBranchSummary
{
    [JsonPropertyName("Index")]
    public int Index { get; set; }

    [JsonPropertyName("Name")]
    public string? Name { get; set; }

    [JsonPropertyName("Status")]
    public string? Status { get; set; }
}
