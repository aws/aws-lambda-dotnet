// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

namespace Amazon.Lambda.DurableExecution;

/// <summary>
/// Identifying information passed to an <see cref="IDurableResultSerializer"/> when a
/// durable operation's result is (de)serialized. It lets a context-aware serializer
/// derive a stable, collision-free external location (for example a file path) for the
/// value being stored.
/// </summary>
/// <remarks>
/// The two values are stable for the lifetime of a durable execution — they do not
/// change across the multiple Lambda invocations (replays) of that execution.
/// </remarks>
public readonly struct DurableSerializationContext
{
    /// <summary>
    /// Stable identifier of the operation whose result is being (de)serialized (the
    /// durable operation id; for Map/Parallel units, a per-unit id derived from it).
    /// Unique within a single durable execution.
    /// </summary>
    public string EntityId { get; }

    /// <summary>
    /// ARN of the durable execution. Used to avoid collisions between the stored
    /// results of different executions.
    /// </summary>
    public string DurableExecutionArn { get; }

    /// <summary>Creates a new <see cref="DurableSerializationContext"/>.</summary>
    /// <param name="entityId">The per-operation entity id.</param>
    /// <param name="durableExecutionArn">The durable execution ARN.</param>
    public DurableSerializationContext(string entityId, string durableExecutionArn)
    {
        EntityId = entityId;
        DurableExecutionArn = durableExecutionArn;
    }
}
