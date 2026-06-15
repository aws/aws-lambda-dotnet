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
/// <see cref="Flat"/> is reserved for a forthcoming optimisation that uses
/// virtual contexts to reduce checkpoint volume by ~30%. The .NET SDK currently
/// throws <see cref="System.NotSupportedException"/> when <see cref="Flat"/> is
/// supplied; the enum value is kept stable so opting in becomes non-breaking.
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
    /// Branches use virtual contexts sharing the parent. Reduces checkpoint
    /// cost at the expense of less granular execution traces.
    /// </summary>
    /// <remarks>
    /// Not yet implemented in the .NET SDK; passing this value throws
    /// <see cref="System.NotSupportedException"/>.
    /// </remarks>
    Flat
}
