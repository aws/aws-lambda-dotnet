// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda;
using Amazon.Lambda.Core;

namespace Amazon.Lambda.DurableExecution.Internal;

/// <summary>
/// Durable parallel operation. Runs N user-supplied branches concurrently,
/// each as a <see cref="ChildContextOperation{T}"/>. All orchestration,
/// completion, checkpoint, and replay logic lives in
/// <see cref="ConcurrentOperation{T}"/>; this subclass supplies only the
/// branch-specific bits (unit count, per-branch <c>(name, func)</c>, sub-type
/// labels, and the failure-exception factory).
/// </summary>
internal sealed class ParallelOperation<T> : ConcurrentOperation<T>
{
    private readonly IReadOnlyList<DurableBranch<T>> _branches;

    public ParallelOperation(
        string operationId,
        string? name,
        string? parentId,
        IReadOnlyList<DurableBranch<T>> branches,
        ParallelConfig config,
        ILambdaSerializer serializer,
        Func<string, string?, bool, IDurableContext> childContextFactory,
        ExecutionState state,
        TerminationManager termination,
        WorkflowCancellation workflowCancellation,
        string durableExecutionArn,
        CheckpointBatcher? batcher = null)
        : base(operationId, name, parentId, config.CompletionConfig, config.MaxConcurrency,
            serializer, childContextFactory, state, termination, workflowCancellation,
            durableExecutionArn, batcher,
            isVirtual: config.NestingType == NestingType.Flat)
    {
        _branches = branches;
    }

    protected override int UnitCount => _branches.Count;
    protected override string ParentSubType => OperationSubTypes.Parallel;
    protected override string ChildSubType => OperationSubTypes.ParallelBranch;
    protected override string OperationNoun => "Parallel";

    protected override (string? Name, Func<IDurableContext, CancellationToken, Task<T>> Func) GetUnit(int index)
    {
        var branch = _branches[index];
        return (branch.Name, branch.Func);
    }
}
