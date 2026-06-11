// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

namespace Amazon.Lambda.DurableExecution;

/// <summary>
/// One item inside an <see cref="IBatchResult{T}"/> — the outcome of a single
/// branch (parallel) or item (map).
/// </summary>
/// <typeparam name="T">The branch/item result type.</typeparam>
public interface IBatchItem<T>
{
    /// <summary>
    /// Zero-based position in the original branches/items list. Stable across
    /// replays.
    /// </summary>
    int Index { get; }

    /// <summary>
    /// Optional human-readable name for this branch/item.
    /// Surfaces on the wire <c>OperationUpdate.Name</c> field for observability.
    /// </summary>
    string? Name { get; }

    /// <summary>
    /// Status of this item at the moment the batch resolved.
    /// </summary>
    BatchItemStatus Status { get; }

    /// <summary>
    /// The branch/item result. Populated only when <see cref="Status"/> is
    /// <see cref="BatchItemStatus.Succeeded"/>.
    /// </summary>
    T? Result { get; }

    /// <summary>
    /// The branch/item failure. Populated only when <see cref="Status"/> is
    /// <see cref="BatchItemStatus.Failed"/>.
    /// </summary>
    DurableExecutionException? Error { get; }
}
