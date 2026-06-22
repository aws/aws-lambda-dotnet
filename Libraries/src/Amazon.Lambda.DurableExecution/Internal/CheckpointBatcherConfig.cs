// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

namespace Amazon.Lambda.DurableExecution.Internal;

/// <summary>
/// Tunables for <see cref="CheckpointBatcher"/>.
/// </summary>
internal sealed class CheckpointBatcherConfig
{
    /// <summary>
    /// How long the worker waits for additional items to coalesce into a single
    /// batch before flushing. Default <see cref="TimeSpan.Zero"/> = flush as soon
    /// as the queue drains. Increase to reduce API calls when many checkpoints
    /// are emitted concurrently (e.g. parallel branches, future Map operation).
    /// </summary>
    public TimeSpan FlushInterval { get; init; } = TimeSpan.Zero;

    /// <summary>
    /// Maximum operations per batch. Service-side limit is 200.
    /// </summary>
    public int MaxBatchOperations { get; init; } = 200;

    /// <summary>
    /// Maximum batch size in bytes. Service-side request limit is ~750 KB.
    /// </summary>
    /// <remarks>
    /// Enforced by the worker: it flushes the current batch before adding an item
    /// that would push the estimated request size over this cap, and sends a lone
    /// item that already exceeds the cap by itself. The per-update estimate plus a
    /// fixed request-prefix reserve approximate the real wire size conservatively.
    /// </remarks>
    public int MaxBatchBytes { get; init; } = 750 * 1024;
}
