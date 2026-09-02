// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.Core;

namespace Amazon.Lambda.DurableExecution;

/// <summary>
/// Configuration for
/// <see cref="IDurableContext.ParallelAsync{T}(IReadOnlyList{System.Func{IDurableContext, System.Threading.CancellationToken, System.Threading.Tasks.Task{T}}}, string?, ParallelConfig?, System.Threading.CancellationToken)"/>.
/// </summary>
/// <remarks>
/// Per-branch result payloads are serialized via the
/// <see cref="ILambdaSerializer"/> registered on
/// <see cref="ILambdaContext.Serializer"/> (typically configured via
/// <c>LambdaBootstrapBuilder.Create(handler, serializer)</c>), unless overridden per
/// operation via <see cref="ItemSerializer"/>. The aggregated batch envelope (per-branch
/// statuses and completion reason) is SDK-internal and is not user-serialized.
/// </remarks>
public sealed class ParallelConfig
{
    private int? _maxConcurrency;

    /// <summary>
    /// Maximum number of branches running concurrently. <c>null</c> (default) =
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
    /// When the parallel operation is considered complete. Defaults to
    /// <see cref="CompletionConfig.AllSuccessful"/> (fail-fast) — any single
    /// branch failure resolves the operation with
    /// <see cref="CompletionReason.FailureToleranceExceeded"/>.
    /// </summary>
    /// <remarks>
    /// The parallel operation never throws on failure — it always returns an
    /// <see cref="IBatchResult{T}"/>. Inspect
    /// <see cref="IBatchResult.CompletionReason"/> /
    /// <see cref="IBatchResult.HasFailure"/> or call
    /// <see cref="IBatchResult{T}.ThrowIfError"/> to surface failures.
    /// </remarks>
    public CompletionConfig CompletionConfig { get; set; } = CompletionConfig.AllSuccessful();

    /// <summary>
    /// How branches are represented in the checkpoint graph. Defaults to
    /// <see cref="NestingType.Nested"/>.
    /// </summary>
    /// <remarks>
    /// Under <see cref="NestingType.Flat"/> each branch runs in a virtual
    /// context that emits no per-branch <c>CONTEXT</c> checkpoint; per-branch
    /// results and errors are recorded inline on the parallel operation's
    /// payload instead.
    /// </remarks>
    public NestingType NestingType { get; set; } = NestingType.Nested;

    /// <summary>
    /// Optional serializer for each branch's <b>result</b> payload. When <c>null</c>
    /// (default), branch results are serialized with the <see cref="ILambdaSerializer"/>
    /// registered on <see cref="ILambdaContext.Serializer"/>. This controls only the
    /// per-branch result — not the aggregated batch envelope (statuses / completion
    /// reason) — and durable operations inside a branch use their own configuration.
    /// </summary>
    public ILambdaSerializer? ItemSerializer { get; set; }
}
