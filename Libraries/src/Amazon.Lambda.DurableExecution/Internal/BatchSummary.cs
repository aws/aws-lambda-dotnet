using System.Text.Json.Serialization;

namespace Amazon.Lambda.DurableExecution.Internal;

/// <summary>
/// Internal payload shape stored on a concurrent operation's parent CONTEXT
/// checkpoint (as <c>ContextDetails.Result</c>) and reconstructed on replay.
/// Shared by both <see cref="ParallelOperation{T}"/> and
/// <see cref="MapOperation{TItem, TResult}"/>: carries the completion reason and
/// the per-unit index → status map so the <see cref="IBatchResult{T}"/> can be
/// rebuilt without depending on user T shape.
/// </summary>
/// <remarks>
/// Each dispatched unit's serialized result (<see cref="BatchUnitSummary.Result"/>)
/// or error (<see cref="BatchUnitSummary.Error"/>) is recorded inline here for
/// BOTH <see cref="NestingType.Nested"/> and <see cref="NestingType.Flat"/>. Flat
/// children emit no checkpoint of their own, and the service collapses completed
/// Nested per-unit child contexts out of the state returned on a later resume, so
/// the inline copy is the authoritative source for rebuilding the
/// <see cref="IBatchResult{T}"/> on replay. If the inline payload would exceed the
/// checkpoint size limit it is stripped (statuses only) and <c>ReplayChildren</c>
/// is set, so replay re-executes the units to recover their values instead.
/// </remarks>
internal sealed class BatchSummary
{
    [JsonPropertyName("CompletionReason")]
    public string? CompletionReason { get; set; }

    [JsonPropertyName("Units")]
    public IList<BatchUnitSummary> Units { get; set; } = new List<BatchUnitSummary>();
}

internal sealed class BatchUnitSummary
{
    [JsonPropertyName("Index")]
    public int Index { get; set; }

    [JsonPropertyName("Name")]
    public string? Name { get; set; }

    [JsonPropertyName("Status")]
    public string? Status { get; set; }

    /// <summary>
    /// Serialized per-unit result, recorded inline for succeeded units under both
    /// <see cref="NestingType.Nested"/> and <see cref="NestingType.Flat"/>.
    /// <c>null</c> for non-succeeded units, or when the summary was stripped on
    /// overflow (results recovered by re-executing units on replay).
    /// </summary>
    [JsonPropertyName("Result")]
    public string? Result { get; set; }

    /// <summary>
    /// Per-unit error, recorded inline for failed units under both
    /// <see cref="NestingType.Nested"/> and <see cref="NestingType.Flat"/>.
    /// <c>null</c> for non-failed units, or when the summary was stripped on
    /// overflow.
    /// </summary>
    [JsonPropertyName("Error")]
    public ErrorObject? Error { get; set; }
}
