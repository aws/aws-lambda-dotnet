// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

namespace Amazon.Lambda.DurableExecution;

/// <summary>
/// Non-generic marker for <see cref="IBatchResult{T}"/>. Used by
/// <see cref="ParallelException.Result"/> so callers can hold a reference to
/// the aggregate result without knowing the per-branch type at compile time.
/// </summary>
public interface IBatchResult
{
    /// <summary>
    /// Why the batch resolved.
    /// </summary>
    CompletionReason CompletionReason { get; }

    /// <summary>True if any item is in <see cref="BatchItemStatus.Failed"/>.</summary>
    bool HasFailure { get; }

    /// <summary>Number of items in <see cref="BatchItemStatus.Succeeded"/>.</summary>
    int SuccessCount { get; }

    /// <summary>Number of items in <see cref="BatchItemStatus.Failed"/>.</summary>
    int FailureCount { get; }

    /// <summary>Number of items in <see cref="BatchItemStatus.Started"/>.</summary>
    int StartedCount { get; }

    /// <summary>Total number of items.</summary>
    int TotalCount { get; }
}

/// <summary>
/// Result of a parallel (and future map) operation. Aggregates the per-branch
/// outcomes, completion bookkeeping, and convenience accessors.
/// </summary>
/// <typeparam name="T">The per-branch/per-item result type.</typeparam>
/// <remarks>
/// The result is reconstructed from per-branch checkpoints — the aggregate is
/// never serialized as a single blob in user T. Per-branch results live on
/// <c>ParallelBranch</c> child-context checkpoints; this type assembles them.
/// </remarks>
public interface IBatchResult<T> : IBatchResult
{
    /// <summary>
    /// All items, in original index order.
    /// </summary>
    IReadOnlyList<IBatchItem<T>> All { get; }

    /// <summary>
    /// Items whose <see cref="IBatchItem{T}.Status"/> is
    /// <see cref="BatchItemStatus.Succeeded"/>, in original index order.
    /// </summary>
    IReadOnlyList<IBatchItem<T>> Succeeded { get; }

    /// <summary>
    /// Items whose <see cref="IBatchItem{T}.Status"/> is
    /// <see cref="BatchItemStatus.Failed"/>, in original index order.
    /// </summary>
    IReadOnlyList<IBatchItem<T>> Failed { get; }

    /// <summary>
    /// Items that were not dispatched when the batch resolved (a
    /// <see cref="CompletionConfig"/> short-circuit fired before they were started),
    /// in original index order.
    /// </summary>
    IReadOnlyList<IBatchItem<T>> Started { get; }

    /// <summary>
    /// Returns the results of every successful item, in original index order.
    /// </summary>
    /// <remarks>
    /// Items in <see cref="Failed"/> or <see cref="Started"/> are skipped — this
    /// method never throws on partial-failure batches. Use
    /// <see cref="ThrowIfError"/> if you want a strict-success accessor.
    /// </remarks>
    IReadOnlyList<T> GetResults();

    /// <summary>
    /// Returns the errors for every failed item, in original index order.
    /// </summary>
    IReadOnlyList<DurableExecutionException> GetErrors();

    /// <summary>
    /// Throws the first failed item's <see cref="IBatchItem{T}.Error"/> if any
    /// item failed; no-op otherwise.
    /// </summary>
    /// <exception cref="DurableExecutionException">
    /// The first failed item's error.
    /// </exception>
    void ThrowIfError();
}
