using System.Text.Json.Serialization;

namespace Amazon.Lambda.DurableExecution.Internal;

/// <summary>
/// Internal payload shape stored on a concurrent operation's parent CONTEXT
/// checkpoint (as <c>ContextDetails.Result</c>) and reconstructed on replay.
/// Shared by both <see cref="ParallelOperation{T}"/> and
/// <see cref="MapOperation{TItem, TResult}"/>: carries the completion reason and
/// the per-unit index → status map so the <see cref="IBatchResult{T}"/> can be
/// rebuilt without depending on user T shape — per-unit results live on the
/// children's own checkpoints.
/// </summary>
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
}
