namespace Amazon.Lambda.DurableExecution;

/// <summary>
/// Configuration for
/// <see cref="IDurableContext.MapAsync{TItem, TResult}(System.Collections.Generic.IReadOnlyList{TItem}, System.Func{IDurableContext, TItem, int, System.Collections.Generic.IReadOnlyList{TItem}, System.Threading.CancellationToken, System.Threading.Tasks.Task{TResult}}, string?, MapConfig?, System.Threading.CancellationToken)"/>.
/// </summary>
/// <remarks>
/// Per-item checkpoint payloads are serialized via the
/// <see cref="Amazon.Lambda.Core.ILambdaSerializer"/> registered on
/// <see cref="Amazon.Lambda.Core.ILambdaContext.Serializer"/> (typically
/// configured via <c>LambdaBootstrapBuilder.Create(handler, serializer)</c>);
/// this config does not expose a serializer slot.
/// </remarks>
public sealed class MapConfig
{
    private int? _maxConcurrency;

    /// <summary>
    /// Maximum number of items processed concurrently. <c>null</c> (default) =
    /// unlimited. Must be at least 1 when set.
    /// </summary>
    /// <exception cref="System.ArgumentOutOfRangeException">
    /// Thrown by the setter if the value is less than or equal to 0.
    /// </exception>
    public int? MaxConcurrency
    {
        get => _maxConcurrency;
        set
        {
            if (value is { } v && v <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), v,
                    "MaxConcurrency must be at least 1, or null for unlimited.");
            }
            _maxConcurrency = value;
        }
    }

    /// <summary>
    /// When the map operation is considered complete. Defaults to
    /// <see cref="CompletionConfig.AllCompleted"/> — every item runs regardless
    /// of per-item failures, which are surfaced via
    /// <see cref="IBatchResult{T}.Failed"/> rather than thrown.
    /// </summary>
    /// <remarks>
    /// This differs from <see cref="ParallelConfig.CompletionConfig"/>, which
    /// defaults to <see cref="CompletionConfig.AllSuccessful"/> (fail-fast). For
    /// fail-fast map behavior — any item failure surfaces a
    /// <see cref="MapException"/> when the result is awaited — set this to
    /// <see cref="CompletionConfig.AllSuccessful"/>, or call
    /// <see cref="IBatchResult{T}.ThrowIfError"/> on the result.
    /// </remarks>
    public CompletionConfig CompletionConfig { get; set; } = CompletionConfig.AllCompleted();

    /// <summary>
    /// How item branches are represented in the checkpoint graph. Defaults to
    /// <see cref="NestingType.Nested"/>.
    /// </summary>
    /// <remarks>
    /// Under <see cref="NestingType.Flat"/> each item runs in a virtual context
    /// that emits no per-item <c>CONTEXT</c> checkpoint; per-item results and
    /// errors are recorded inline on the map operation's payload instead.
    /// </remarks>
    public NestingType NestingType { get; set; } = NestingType.Nested;

    /// <summary>
    /// Optional function to generate a custom name for each item's branch.
    /// Receives the item and its zero-based index, and returns the branch name
    /// surfaced in execution traces and on <see cref="IBatchItem{T}.Name"/>.
    /// When <c>null</c> (default), branches are named by index (<c>"0"</c>,
    /// <c>"1"</c>, ...).
    /// </summary>
    public Func<object, int, string>? ItemNamer { get; set; }
}
