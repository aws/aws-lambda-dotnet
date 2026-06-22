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
/// Under <see cref="NestingType.Nested"/> per-unit results live on the children's
/// own CONTEXT checkpoints and only <see cref="BatchUnitSummary.Status"/> (plus
/// index/name) is recorded here. Under <see cref="NestingType.Flat"/> the
/// children emit no checkpoint, so each unit's serialized result
/// (<see cref="BatchUnitSummary.Result"/>) or error
/// (<see cref="BatchUnitSummary.Error"/>) is recorded inline here and read back
/// on replay.
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
    /// Serialized per-unit result, recorded inline only for
    /// <see cref="NestingType.Flat"/> succeeded units (where no child checkpoint
    /// exists to read it from). <c>null</c> under <see cref="NestingType.Nested"/>.
    /// </summary>
    [JsonPropertyName("Result")]
    public string? Result { get; set; }

    /// <summary>
    /// Per-unit error, recorded inline only for <see cref="NestingType.Flat"/>
    /// failed units. <c>null</c> under <see cref="NestingType.Nested"/>.
    /// </summary>
    [JsonPropertyName("Error")]
    public ErrorObject? Error { get; set; }
}
