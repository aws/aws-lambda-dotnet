// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

namespace Amazon.Lambda.DurableExecution.Internal;

/// <summary>
/// Default <see cref="IBatchResult{T}"/> implementation. Computes derived views
/// (<see cref="Succeeded"/> / <see cref="Failed"/> / <see cref="Started"/>)
/// eagerly so consumers don't pay for re-filtering on every access.
/// </summary>
internal sealed class BatchResult<T> : IBatchResult<T>
{
    public BatchResult(IReadOnlyList<IBatchItem<T>> all, CompletionReason completionReason)
    {
        All = all;
        CompletionReason = completionReason;

        var succeeded = new List<IBatchItem<T>>();
        var failed = new List<IBatchItem<T>>();
        var started = new List<IBatchItem<T>>();

        foreach (var item in all)
        {
            switch (item.Status)
            {
                case BatchItemStatus.Succeeded: succeeded.Add(item); break;
                case BatchItemStatus.Failed:    failed.Add(item);    break;
                case BatchItemStatus.Started:   started.Add(item);   break;
            }
        }

        Succeeded = succeeded;
        Failed = failed;
        Started = started;
    }

    public IReadOnlyList<IBatchItem<T>> All { get; }
    public IReadOnlyList<IBatchItem<T>> Succeeded { get; }
    public IReadOnlyList<IBatchItem<T>> Failed { get; }
    public IReadOnlyList<IBatchItem<T>> Started { get; }
    public CompletionReason CompletionReason { get; }

    public bool HasFailure => Failed.Count > 0;

    public int SuccessCount => Succeeded.Count;
    public int FailureCount => Failed.Count;
    public int StartedCount => Started.Count;
    public int TotalCount => All.Count;

    public IReadOnlyList<T> GetResults()
    {
        var list = new List<T>(Succeeded.Count);
        foreach (var item in Succeeded)
        {
            // Result is non-null on success items by construction; the BCL-typed
            // index is preserved by walking Succeeded (already in original order).
            list.Add(item.Result!);
        }
        return list;
    }

    public IReadOnlyList<DurableExecutionException> GetErrors()
    {
        var list = new List<DurableExecutionException>(Failed.Count);
        foreach (var item in Failed)
        {
            // Error is non-null on failure items by construction.
            list.Add(item.Error!);
        }
        return list;
    }

    public void ThrowIfError()
    {
        foreach (var item in All)
        {
            if (item.Status == BatchItemStatus.Failed && item.Error != null)
            {
                throw item.Error;
            }
        }
    }
}
