// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

namespace Amazon.Lambda.DurableExecution;

/// <summary>
/// A named branch for
/// <see cref="IDurableContext.ParallelAsync{T}(IReadOnlyList{DurableBranch{T}}, string?, ParallelConfig?, System.Threading.CancellationToken)"/>.
/// Names appear in execution traces and on the wire <c>OperationUpdate.Name</c>
/// field, and surface on <see cref="IBatchItem{T}.Name"/>.
/// </summary>
/// <typeparam name="T">The branch's result type.</typeparam>
/// <param name="Name">Human-readable branch name. Required.</param>
/// <param name="Func">The user function executed inside the branch's child
/// context. It receives the branch's <see cref="IDurableContext"/> and a
/// <see cref="System.Threading.CancellationToken"/> linking the caller-supplied
/// token with the SDK's workflow-shutdown signal.</param>
public sealed record DurableBranch<T>(string Name, Func<IDurableContext, System.Threading.CancellationToken, Task<T>> Func);
