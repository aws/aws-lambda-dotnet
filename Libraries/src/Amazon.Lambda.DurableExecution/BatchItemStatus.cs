// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

namespace Amazon.Lambda.DurableExecution;

/// <summary>
/// Status of an individual item in a <see cref="IBatchResult{T}"/>.
/// </summary>
/// <remarks>
/// Mirrors the wire-state of the per-branch checkpoint at the moment the batch
/// resolved. Items that finished produce <see cref="Succeeded"/> or
/// <see cref="Failed"/>; items that were not dispatched because a
/// <see cref="CompletionConfig"/> short-circuit fired are reported as
/// <see cref="Started"/>.
/// </remarks>
public enum BatchItemStatus
{
    /// <summary>
    /// The branch ran to completion and produced a result.
    /// </summary>
    Succeeded,

    /// <summary>
    /// The branch ran to completion and threw.
    /// </summary>
    Failed,

    /// <summary>
    /// The branch was not dispatched before the batch's <see cref="CompletionConfig"/>
    /// resolved (e.g., <see cref="CompletionConfig.FirstSuccessful"/> short-circuited
    /// before this branch was started), or no per-branch checkpoint exists on replay.
    /// </summary>
    Started
}
