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
    /// Maximum batch size in bytes. Service-side limit is ~750 KB.
    /// </summary>
    /// <remarks>
    /// TODO: not enforced today. The worker only checks <see cref="MaxBatchOperations"/>;
    /// a single oversized item (or a batch whose serialized size exceeds 750 KB)
    /// will be sent to the service and rejected there. Wire this in alongside
    /// the async-flush operations (Map / Parallel / child-context) since those
    /// are the scenarios that can actually fill a batch — today every batch is
    /// 1 item with <see cref="FlushInterval"/> = Zero, so the gap is latent.
    /// </remarks>
    internal int MaxBatchBytes { get; init; } = 750 * 1024;
}
