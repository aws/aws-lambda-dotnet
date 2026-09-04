// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.Core;

namespace Amazon.Lambda.DurableExecution;

/// <summary>
/// Configuration for
/// <see cref="IDurableContext.MapAsync{TItem, TResult}(System.Collections.Generic.IReadOnlyList{TItem}, System.Func{IDurableContext, TItem, int, System.Collections.Generic.IReadOnlyList{TItem}, System.Threading.CancellationToken, System.Threading.Tasks.Task{TResult}}, string?, MapConfig{TItem}?, System.Threading.CancellationToken)"/>.
/// </summary>
/// <typeparam name="TItem">
/// The type of each item processed by the map. Generic so <see cref="ItemNamer"/>
/// receives the strongly-typed item rather than <c>object</c>.
/// </typeparam>
/// <remarks>
/// Per-item result payloads are serialized via the
/// <see cref="ILambdaSerializer"/> registered on
/// <see cref="ILambdaContext.Serializer"/> (typically configured via
/// <c>LambdaBootstrapBuilder.Create(handler, serializer)</c>), unless overridden per
/// operation via <see cref="ItemSerializer"/>. The aggregated batch envelope (per-item
/// statuses and completion reason) is SDK-internal and is not user-serialized.
/// </remarks>
public sealed class MapConfig<TItem>
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
    /// <see cref="CompletionConfig.AllSuccessful"/> (fail-fast) — any item failure
    /// resolves the map with
    /// <see cref="CompletionReason.FailureToleranceExceeded"/>, matching the
    /// JS/Python SDKs and <see cref="ParallelConfig.CompletionConfig"/>.
    /// </summary>
    /// <remarks>
    /// The map never throws on failure — it always returns an
    /// <see cref="IBatchResult{T}"/>. Inspect
    /// <see cref="IBatchResult.CompletionReason"/> /
    /// <see cref="IBatchResult.HasFailure"/> or call
    /// <see cref="IBatchResult{T}.ThrowIfError"/> to surface failures. For
    /// run-everything semantics, set this to
    /// <see cref="CompletionConfig.AllCompleted"/>.
    /// </remarks>
    public CompletionConfig CompletionConfig { get; set; } = CompletionConfig.AllSuccessful();

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
    /// Receives the strongly-typed item and its zero-based index, and returns the
    /// branch name surfaced in execution traces and on
    /// <see cref="IBatchItem{T}.Name"/>. When <c>null</c> (default), branches are
    /// named by index (<c>"0"</c>, <c>"1"</c>, ...).
    /// </summary>
    public Func<TItem, int, string>? ItemNamer { get; set; }

    /// <summary>
    /// Optional serializer for each item's <b>result</b> payload. When <c>null</c>
    /// (default), item results are serialized with the <see cref="ILambdaSerializer"/>
    /// registered on <see cref="ILambdaContext.Serializer"/>. This controls only the
    /// per-item result — both the inline copy on the map's checkpoint and each nested
    /// item's own checkpoint. It does not change the aggregated batch envelope
    /// (statuses / completion reason), and durable operations inside an item's body use
    /// their own configuration.
    /// </summary>
    /// <remarks>
    /// <b>Size-dependent transform on overflow (Flat only).</b> Under
    /// <see cref="NestingType.Flat"/> each item's result is round-tripped through this
    /// serializer on a fresh (non-replay) success before it enters the result, so a
    /// serializer that transforms the value (e.g. redaction, canonicalization) has that
    /// transform reflected in the returned item — but <em>only</em> while the aggregated
    /// per-item payloads fit inline in a single checkpoint (≈256&#160;KB). If they are
    /// large enough to overflow, the inline results are stripped and each value is instead
    /// recovered by re-running the item body on replay, which does <em>not</em> apply the
    /// round-trip transform. A serializer whose <c>Deserialize</c> is not a pure inverse of
    /// its <c>Serialize</c> will therefore observe a size-dependent difference (transform
    /// applied for small results, skipped for overflowed ones). Prefer a round-tripping
    /// serializer, or do not depend on the transform being applied to results large enough
    /// to overflow the checkpoint. (Under <see cref="NestingType.Nested"/> the same
    /// size-dependent caveat is documented on each item's child-context result.)
    /// </remarks>
    public ILambdaSerializer? ItemSerializer { get; set; }
}
