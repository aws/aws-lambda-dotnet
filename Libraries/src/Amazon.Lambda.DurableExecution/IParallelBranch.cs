// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;

namespace Amazon.Lambda.DurableExecution;

/// <summary>
/// A typed handle to a single branch registered on an <see cref="IDurableParallel"/>
/// via <see cref="IDurableParallel.Branch{T}"/>.
/// Unlike the homogeneous <see cref="IDurableContext.ParallelAsync{T}(System.Collections.Generic.IReadOnlyList{System.Func{IDurableContext, System.Threading.CancellationToken, System.Threading.Tasks.Task{T}}}, string?, ParallelConfig?, System.Threading.CancellationToken)"/>
/// API — where every branch shares one result type <c>T</c> — each branch on an
/// <see cref="IDurableParallel"/> declares its own result type, so a single
/// parallel operation can mix, for example, an <c>InventoryReservation</c> branch
/// with a <c>PaymentAuthorization</c> branch.
/// </summary>
/// <remarks>
/// The handle is <c>await</c>-able: <c>await branch</c> yields the branch's typed
/// result once it succeeds, or rethrows the branch's failure (a
/// <see cref="ChildContextException"/>) if it failed. Awaiting a branch that was
/// skipped by the operation's <see cref="CompletionConfig"/> short-circuit (its
/// <see cref="Status"/> is <see cref="BatchItemStatus.Started"/>) throws a
/// <see cref="DurableExecutionException"/> — inspect <see cref="Status"/> before
/// awaiting when a completion policy may skip branches.
/// <para>
/// Typically you await the handle <em>after</em>
/// <see cref="IDurableParallel.CompleteAsync(System.Threading.CancellationToken)"/>
/// has sealed and resolved the operation, mirroring the Java SDK's
/// <c>future.get()</c> after the <c>try</c>-with-resources block. A branch may
/// still be awaited earlier; the await simply completes when the branch does.
/// </para>
/// </remarks>
/// <typeparam name="T">The branch's result type.</typeparam>
public interface IParallelBranch<T>
{
    /// <summary>
    /// The branch name supplied at registration. Surfaces on the wire
    /// <c>OperationUpdate.Name</c> field and in execution traces.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Zero-based registration order of this branch within its parallel
    /// operation. Stable across replays. The branch's deterministic operation ID
    /// is derived from the <em>one-based</em> position (<c>hash("{parentId}-{Index+1}")</c>),
    /// so the first branch (<c>Index</c> 0) uses suffix 1.
    /// </summary>
    int Index { get; }

    /// <summary>
    /// The branch's outcome. <see cref="BatchItemStatus.Started"/> until the
    /// branch settles (and permanently for a branch skipped by a completion-policy
    /// short-circuit), then <see cref="BatchItemStatus.Succeeded"/> or
    /// <see cref="BatchItemStatus.Failed"/>.
    /// </summary>
    BatchItemStatus Status { get; }

    /// <summary>
    /// Enables <c>await branch</c>. Yields the branch's typed result on success,
    /// rethrows its <see cref="ChildContextException"/> on failure, or throws a
    /// <see cref="DurableExecutionException"/> if the branch was skipped.
    /// </summary>
    /// <returns>An awaiter over the branch's result.</returns>
    TaskAwaiter<T> GetAwaiter();
}
