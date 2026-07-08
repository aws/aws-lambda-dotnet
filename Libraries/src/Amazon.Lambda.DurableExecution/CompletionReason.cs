// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

namespace Amazon.Lambda.DurableExecution;

/// <summary>
/// Why a batch operation (<see cref="IDurableContext.ParallelAsync{T}(IReadOnlyList{System.Func{IDurableContext, System.Threading.CancellationToken, System.Threading.Tasks.Task{T}}}, string?, ParallelConfig?, System.Threading.CancellationToken)"/>
/// or future Map) resolved.
/// </summary>
public enum CompletionReason
{
    /// <summary>
    /// Every branch finished — no <see cref="CompletionConfig"/> short-circuit
    /// was triggered. Branches may be a mix of <see cref="BatchItemStatus.Succeeded"/>
    /// and <see cref="BatchItemStatus.Failed"/>.
    /// </summary>
    AllCompleted,

    /// <summary>
    /// <see cref="CompletionConfig.MinSuccessful"/> branches succeeded; remaining
    /// branches were left in <see cref="BatchItemStatus.Started"/>.
    /// </summary>
    MinSuccessfulReached,

    /// <summary>
    /// <see cref="CompletionConfig.ToleratedFailureCount"/> or
    /// <see cref="CompletionConfig.ToleratedFailurePercentage"/> was exceeded (a
    /// default/empty <see cref="CompletionConfig"/> is fail-fast, so any failure
    /// trips this). The batch is considered failed: <see cref="IBatchResult.HasFailure"/>
    /// is <c>true</c> and <see cref="IBatchResult{T}.ThrowIfError"/> surfaces the
    /// first item error. The operation itself does not throw.
    /// </summary>
    FailureToleranceExceeded
}
