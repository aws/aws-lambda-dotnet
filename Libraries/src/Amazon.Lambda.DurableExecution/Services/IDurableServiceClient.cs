// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using SdkOperationUpdate = Amazon.Lambda.Model.OperationUpdate;

namespace Amazon.Lambda.DurableExecution.Services;

/// <summary>
/// Abstraction over the durable execution service RPCs. The production
/// implementation (<see cref="LambdaDurableServiceClient"/>) calls the real
/// AWS Lambda APIs; the testing package injects an in-memory fake.
/// </summary>
internal interface IDurableServiceClient
{
    Task<string?> CheckpointAsync(
        string durableExecutionArn,
        string? checkpointToken,
        IReadOnlyList<SdkOperationUpdate> pendingOperations,
        Action<IReadOnlyList<Operation>>? onNewOperations = null,
        CancellationToken cancellationToken = default);

    Task<(List<Operation> Operations, string? NextMarker)> GetExecutionStateAsync(
        string durableExecutionArn,
        string? checkpointToken,
        string marker,
        CancellationToken cancellationToken = default);
}
