// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.DurableExecution.Services;
using SdkOperationUpdate = Amazon.Lambda.Model.OperationUpdate;

namespace Amazon.Lambda.DurableExecution.Testing;

/// <summary>
/// In-memory implementation of <see cref="IDurableServiceClient"/> for local testing.
/// Processes checkpoint updates against the in-memory store and returns operations
/// to the runtime engine.
/// </summary>
internal sealed class InMemoryDurableServiceClient : IDurableServiceClient
{
    private readonly InMemoryOperationStore _store;
    private readonly CheckpointProcessor _processor;
    private readonly Func<int, bool>? _omitCheckpointToken;
    private int _checkpointCalls;

    public InMemoryDurableServiceClient(InMemoryOperationStore store, CheckpointProcessor processor, Func<int, bool>? omitCheckpointToken = null)
    {
        _store = store;
        _processor = processor;
        _omitCheckpointToken = omitCheckpointToken;
    }

    public Task<string?> CheckpointAsync(
        string durableExecutionArn,
        string? checkpointToken,
        IReadOnlyList<SdkOperationUpdate> pendingOperations,
        Action<IReadOnlyList<Operation>>? onNewOperations = null,
        CancellationToken cancellationToken = default)
    {
        if (pendingOperations.Count == 0)
            return Task.FromResult(checkpointToken);

        var (newToken, newOps) = _processor.Process(durableExecutionArn, checkpointToken, pendingOperations);

        if (onNewOperations is not null && newOps.Count > 0)
            onNewOperations(newOps);

        if (_omitCheckpointToken?.Invoke(Interlocked.Increment(ref _checkpointCalls)) == true)
            return Task.FromResult<string?>(null);

        return Task.FromResult<string?>(newToken);
    }

    public Task<(List<Operation> Operations, string? NextMarker)> GetExecutionStateAsync(
        string durableExecutionArn,
        string? checkpointToken,
        string marker,
        CancellationToken cancellationToken = default)
    {
        var allOps = _store.GetAllOperations(durableExecutionArn);
        return Task.FromResult<(List<Operation>, string?)>((allOps.ToList(), null));
    }
}
