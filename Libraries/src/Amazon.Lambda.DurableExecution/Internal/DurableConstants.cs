// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

namespace Amazon.Lambda.DurableExecution.Internal;

/// <summary>
/// Size limits for durable-execution payload overflow handling. These are the
/// SDK's chosen overflow *trigger* thresholds for cross-SDK parity (Python/Java
/// use the same 256 KB), not the AWSSDK.Lambda hard field caps (those are 6 MB).
/// </summary>
internal static class DurableConstants
{
    /// <summary>
    /// Serialized-payload byte length above which a concurrent/child-context
    /// operation switches to the <c>ReplayChildren</c> overflow strategy:
    /// strip the inline result from the checkpoint and reconstruct on replay by
    /// re-executing the unit/child bodies. 256 KB (262,144 bytes).
    /// </summary>
    internal const int MaxOperationCheckpointBytes = 256 * 1024;
}
