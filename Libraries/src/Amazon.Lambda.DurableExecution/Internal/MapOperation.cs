// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using Amazon.Lambda;
using Amazon.Lambda.Core;

namespace Amazon.Lambda.DurableExecution.Internal;

/// <summary>
/// Durable map operation. Processes a collection in parallel, running the
/// user-supplied function once per item — each as a
/// <see cref="ChildContextOperation{TResult}"/>. All orchestration, completion,
/// checkpoint, and replay logic lives in <see cref="ConcurrentOperation{T}"/>;
/// this subclass supplies only the map-specific bits: how to turn an item index
/// into a <c>(name, func)</c> pair (the per-item callback receives the item, its
/// index, and the full source list) and the Map sub-type labels.
/// </summary>
internal sealed class MapOperation<TItem, TResult> : ConcurrentOperation<TResult>
{
    private readonly IReadOnlyList<TItem> _items;
    private readonly Func<IDurableContext, TItem, int, IReadOnlyList<TItem>, CancellationToken, Task<TResult>> _func;
    private readonly Func<TItem, int, string>? _itemNamer;

    public MapOperation(
        string operationId,
        string? name,
        string? parentId,
        IReadOnlyList<TItem> items,
        Func<IDurableContext, TItem, int, IReadOnlyList<TItem>, CancellationToken, Task<TResult>> func,
        MapConfig<TItem> config,
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
        _items = items;
        _func = func;
        _itemNamer = config.ItemNamer;
    }

    protected override int UnitCount => _items.Count;
    protected override string ParentSubType => OperationSubTypes.Map;
    protected override string ChildSubType => OperationSubTypes.MapIteration;
    protected override string OperationNoun => "Map";

    protected override (string? Name, Func<IDurableContext, CancellationToken, Task<TResult>> Func) GetUnit(int index)
    {
        var item = _items[index];
        // Default name is the index — matches the unnamed-branch convention in
        // ParallelAsync. A custom ItemNamer can derive a readable name from the
        // item's content. Naming affects observability only, never replay
        // correlation (child operation IDs are derived from the index).
        var name = _itemNamer is not null
            ? _itemNamer(item, index)
            : index.ToString(CultureInfo.InvariantCulture);

        // Forward the child context's token (caller + workflow-shutdown +
        // cooperative-bail, linked by ChildContextOperation) to the user callback
        // so map items can observe short-circuit just like parallel branches.
        return (name, (ctx, ct) => _func(ctx, item, index, _items, ct));
    }
}
