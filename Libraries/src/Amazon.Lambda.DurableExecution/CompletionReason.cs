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
    /// <see cref="CompletionConfig.ToleratedFailurePercentage"/> was exceeded.
    /// The batch is considered failed and surfaces a
    /// <see cref="ParallelException"/> when awaited.
    /// </summary>
    FailureToleranceExceeded
}
