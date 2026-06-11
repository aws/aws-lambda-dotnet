// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

namespace Amazon.Lambda.DurableExecution.Internal;

/// <summary>
/// Default <see cref="IBatchItem{T}"/> implementation produced by
/// <see cref="ParallelOperation{T}"/> when assembling the
/// <see cref="IBatchResult{T}"/>.
/// </summary>
internal sealed class BatchItem<T> : IBatchItem<T>
{
    public required int Index { get; init; }
    public required string? Name { get; init; }
    public required BatchItemStatus Status { get; init; }
    public T? Result { get; init; }
    public DurableExecutionException? Error { get; init; }
}
