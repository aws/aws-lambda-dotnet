// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

namespace Amazon.Lambda.DurableExecution;

/// <summary>
/// Configuration for
/// <see cref="IDurableContext.ParallelAsync{T}(IReadOnlyList{System.Func{IDurableContext, System.Threading.CancellationToken, System.Threading.Tasks.Task{T}}}, string?, ParallelConfig?, System.Threading.CancellationToken)"/>.
/// </summary>
/// <remarks>
/// Per-branch checkpoint payloads are serialized via the
/// <see cref="Amazon.Lambda.Core.ILambdaSerializer"/> registered on
/// <see cref="Amazon.Lambda.Core.ILambdaContext.Serializer"/> (typically
/// configured via <c>LambdaBootstrapBuilder.Create(handler, serializer)</c>);
/// this config does not expose a serializer slot.
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
    /// <see cref="CompletionConfig.AllSuccessful"/> — any single branch failure
    /// surfaces as a <see cref="ParallelException"/> when the parallel result
    /// is awaited.
    /// </summary>
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
}
