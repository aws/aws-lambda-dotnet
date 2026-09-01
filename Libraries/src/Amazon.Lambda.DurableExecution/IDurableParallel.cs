// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

namespace Amazon.Lambda.DurableExecution;

/// <summary>
/// An incremental, branch-oriented parallel operation created by
/// <see cref="IDurableContext.CreateParallel(string?, ParallelConfig?)"/>. Branches
/// are registered one at a time via
/// <see cref="BranchAsync{T}(string, System.Func{IDurableContext, System.Threading.CancellationToken, System.Threading.Tasks.Task{T}})"/>,
/// each with its own result type (heterogeneous), and each begins executing
/// immediately (subject to <see cref="ParallelConfig.MaxConcurrency"/>). Call
/// <see cref="CompleteAsync(System.Threading.CancellationToken)"/> to seal
/// registration, await the branches according to the
/// <see cref="ParallelConfig.CompletionConfig"/>, and obtain the aggregate result.
/// </summary>
/// <remarks>
/// This is an additive alternative to the homogeneous
/// <see cref="IDurableContext.ParallelAsync{T}(System.Collections.Generic.IReadOnlyList{System.Func{IDurableContext, System.Threading.CancellationToken, System.Threading.Tasks.Task{T}}}, string?, ParallelConfig?, System.Threading.CancellationToken)"/>
/// overloads, which accept a complete branch list up front and share one result
/// type. Use <c>CreateParallel</c> when branches return unrelated types, or when
/// branches are discovered incrementally (for example, tool calls derived from a
/// checkpointed plan) and earlier branches should start before later ones are known.
/// <para>
/// <b>Deterministic replay.</b> Branch identity is positional: the n-th
/// <see cref="BranchAsync{T}(string, System.Func{IDurableContext, System.Threading.CancellationToken, System.Threading.Tasks.Task{T}})"/>
/// call reuses the n-th deterministic operation ID. Workflow code must therefore
/// register the same branches in the same order across invocations — produce any
/// dynamic branch list inside a checkpointed <see cref="IDurableContext.StepAsync{T}(System.Func{IStepContext, System.Threading.CancellationToken, System.Threading.Tasks.Task{T}}, string?, StepConfig?, System.Threading.CancellationToken)"/>
/// so replay sees the same set.
/// </para>
/// <para>
/// <b>Disposal.</b> <see cref="System.IAsyncDisposable.DisposeAsync"/> seals and
/// completes the operation if
/// <see cref="CompleteAsync(System.Threading.CancellationToken)"/> was not called,
/// so an <c>await using</c> block always writes the parallel's terminal checkpoint.
/// Calling <see cref="CompleteAsync(System.Threading.CancellationToken)"/>
/// explicitly is recommended so you can capture the aggregate result.
/// </para>
/// </remarks>
public interface IDurableParallel : IAsyncDisposable
{
    /// <summary>
    /// Registers a branch and immediately begins executing it (respecting
    /// <see cref="ParallelConfig.MaxConcurrency"/>). Returns a typed
    /// <see cref="IParallelBranch{T}"/> handle for retrieving the branch's result.
    /// </summary>
    /// <remarks>
    /// The branch runs inside its own child context with a deterministic
    /// operation-ID space; its result is serialized to a checkpoint via the
    /// <see cref="Amazon.Lambda.Core.ILambdaSerializer"/> registered on
    /// <see cref="Amazon.Lambda.Core.ILambdaContext.Serializer"/>. Per-branch
    /// failures are captured on the handle and aggregated into the
    /// <see cref="CompleteAsync(System.Threading.CancellationToken)"/> result — a
    /// branch failure never throws out of this method.
    /// </remarks>
    /// <typeparam name="T">The branch's result type.</typeparam>
    /// <param name="name">
    /// Human-readable branch name. Required; surfaces on
    /// <c>OperationUpdate.Name</c> and must remain stable at a given branch index
    /// across deployments (a drift is a non-deterministic-execution error).
    /// </param>
    /// <param name="func">
    /// The branch body. Receives its own <see cref="IDurableContext"/> and a
    /// <see cref="System.Threading.CancellationToken"/> linking the SDK's
    /// workflow-shutdown signal with the operation's completion-policy
    /// short-circuit, and returns the branch's result.
    /// </param>
    /// <returns>A typed handle for awaiting the branch's result.</returns>
    /// <exception cref="System.InvalidOperationException">
    /// The operation has already been sealed by
    /// <see cref="CompleteAsync(System.Threading.CancellationToken)"/> or disposal.
    /// </exception>
    IParallelBranch<T> BranchAsync<T>(
        string name,
        Func<IDurableContext, CancellationToken, Task<T>> func);

    /// <summary>
    /// Seals registration (no further branches may be added), awaits the
    /// registered branches according to the
    /// <see cref="ParallelConfig.CompletionConfig"/>, checkpoints the aggregate
    /// outcome, and returns it. Idempotent — repeated calls return the same result.
    /// </summary>
    /// <remarks>
    /// Like the homogeneous parallel API, this never throws on per-branch failure:
    /// inspect <see cref="IBatchResult.HasFailure"/> /
    /// <see cref="IBatchResult.CompletionReason"/>, or await individual branch
    /// handles, to observe failures. It does propagate workflow-level errors (for
    /// example <see cref="NonDeterministicExecutionException"/>) and cancellation.
    /// <para>
    /// The <paramref name="cancellationToken"/> governs sealing and awaiting: it
    /// stops this call from waiting further. Because branches begin executing when
    /// they are registered (before <c>CompleteAsync</c> is called), this token is
    /// not retroactively linked into already-running branch bodies — those observe
    /// the SDK's workflow-shutdown signal (and the completion-policy short-circuit)
    /// instead. Dispatched branches always run to a terminal checkpoint so replay
    /// stays deterministic, matching <see cref="IDurableContext.ParallelAsync{T}(System.Collections.Generic.IReadOnlyList{System.Func{IDurableContext, System.Threading.CancellationToken, System.Threading.Tasks.Task{T}}}, string?, ParallelConfig?, System.Threading.CancellationToken)"/>.
    /// </para>
    /// </remarks>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The aggregate <see cref="IBatchResult"/> summarizing branch outcomes.</returns>
    Task<IBatchResult> CompleteAsync(CancellationToken cancellationToken = default);
}
