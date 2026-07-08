// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

namespace Amazon.Lambda.DurableExecution;

/// <summary>
/// Defines completion criteria for parallel/map operations.
/// </summary>
/// <remarks>
/// Construct via the static factories (<see cref="AllSuccessful"/>,
/// <see cref="AllCompleted"/>, <see cref="FirstSuccessful"/>) or set the
/// individual properties directly. Multiple criteria combine: the operation
/// resolves as soon as any criterion is met (success short-circuit) or violated
/// (failure short-circuit).
/// </remarks>
public sealed class CompletionConfig
{
    private int? _minSuccessful;
    private int? _toleratedFailureCount;
    private double? _toleratedFailurePercentage;

    /// <summary>
    /// Minimum number of <see cref="BatchItemStatus.Succeeded"/> items required
    /// before the operation resolves successfully. <c>null</c> = no minimum.
    /// </summary>
    /// <exception cref="System.ArgumentOutOfRangeException">
    /// Thrown by the setter if the value is less than <c>1</c>. A minimum of
    /// zero (or negative) would resolve the operation immediately without
    /// dispatching any branch.
    /// </exception>
    public int? MinSuccessful
    {
        get => _minSuccessful;
        set
        {
            if (value is { } v && v < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(value), v,
                    "MinSuccessful must be at least 1.");
            }
            _minSuccessful = value;
        }
    }

    /// <summary>
    /// Maximum tolerated <see cref="BatchItemStatus.Failed"/> count. When the
    /// failure count <i>strictly exceeds</i> this value, the operation resolves
    /// with <see cref="CompletionReason.FailureToleranceExceeded"/>.
    /// <c>null</c> = no count-based failure threshold.
    /// </summary>
    /// <exception cref="System.ArgumentOutOfRangeException">
    /// Thrown by the setter if the value is negative. A negative tolerance
    /// would fail the operation immediately without dispatching any branch.
    /// </exception>
    public int? ToleratedFailureCount
    {
        get => _toleratedFailureCount;
        set
        {
            if (value is { } v && v < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), v,
                    "ToleratedFailureCount must be zero or greater.");
            }
            _toleratedFailureCount = value;
        }
    }

    /// <summary>
    /// Maximum tolerated failure ratio, expressed as a value in the range
    /// <c>0.0</c> to <c>1.0</c> (inclusive). For example, <c>0.25</c> means
    /// "tolerate up to 25% failures; fail when the failure ratio strictly
    /// exceeds 25%". <c>null</c> = no ratio-based failure threshold.
    /// </summary>
    /// <exception cref="System.ArgumentOutOfRangeException">
    /// Thrown by the setter if the value is outside <c>[0.0, 1.0]</c>.
    /// </exception>
    public double? ToleratedFailurePercentage
    {
        get => _toleratedFailurePercentage;
        set
        {
            if (value is { } v && (v < 0.0 || v > 1.0))
            {
                throw new ArgumentOutOfRangeException(nameof(value), v,
                    "ToleratedFailurePercentage must be a ratio in [0.0, 1.0].");
            }
            _toleratedFailurePercentage = value;
        }
    }

    /// <summary>
    /// All items must succeed — any single failure resolves the batch with
    /// <see cref="CompletionReason.FailureToleranceExceeded"/>. Equivalent to
    /// <see cref="ToleratedFailureCount"/> = 0, and to a default (empty)
    /// <see cref="CompletionConfig"/>. The default for both
    /// <see cref="ParallelConfig.CompletionConfig"/> and
    /// <see cref="MapConfig{TItem}.CompletionConfig"/>, matching the JS/Python SDKs.
    /// </summary>
    public static CompletionConfig AllSuccessful() => new() { ToleratedFailureCount = 0 };

    /// <summary>
    /// Run every branch regardless of failures; surface failures per-item via
    /// <see cref="IBatchResult{T}.Failed"/>. Never resolves with
    /// <see cref="CompletionReason.FailureToleranceExceeded"/>. The caller
    /// inspects the result and can call <see cref="IBatchResult{T}.ThrowIfError"/>
    /// if it wants strict-success behavior.
    /// </summary>
    /// <remarks>
    /// Sets <see cref="ToleratedFailureCount"/> to <see cref="int.MaxValue"/> so it
    /// stays lenient. An <em>empty</em> <see cref="CompletionConfig"/> is fail-fast
    /// (equivalent to <see cref="AllSuccessful"/>), so leniency must be opted into
    /// explicitly.
    /// </remarks>
    public static CompletionConfig AllCompleted() => new() { ToleratedFailureCount = int.MaxValue };

    /// <summary>
    /// Resolve once at least one branch has succeeded. Branches that were not
    /// dispatched before the completion criteria was met are reported as
    /// <see cref="BatchItemStatus.Started"/>.
    /// </summary>
    public static CompletionConfig FirstSuccessful() => new() { MinSuccessful = 1 };
}
