// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

namespace Amazon.Lambda.DurableExecution;

/// <summary>
/// Controls how branches in a parallel/map operation are represented in the
/// checkpoint graph.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Nested"/> is the default — each branch produces a full <c>CONTEXT</c>
/// operation visible in execution traces.
/// </para>
/// <para>
/// <see cref="Flat"/> uses virtual contexts to reduce checkpoint volume (no
/// per-branch <c>CONTEXT</c> operation): each branch's result or error is
/// recorded inline on the parent parallel/map operation's payload instead.
/// </para>
/// </remarks>
public enum NestingType
{
    /// <summary>
    /// Each branch creates a full isolated <c>CONTEXT</c> operation. Higher
    /// observability in execution traces but more checkpoint operations
    /// (default).
    /// </summary>
    Nested,

    /// <summary>
    /// Branches run in virtual contexts that emit no <c>CONTEXT</c> checkpoint
    /// of their own — per-branch results/errors are recorded inline on the
    /// parent operation's payload. Reduces checkpoint cost at the expense of
    /// less granular execution traces. Branch operations inside a flat branch
    /// (steps, waits) still checkpoint, re-parented to the parallel/map
    /// operation.
    /// </summary>
    Flat
}
