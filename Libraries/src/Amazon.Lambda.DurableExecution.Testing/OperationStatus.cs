// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

namespace Amazon.Lambda.DurableExecution.Testing;

/// <summary>
/// String constants matching the wire-format operation statuses.
/// Uses a static class instead of an enum so values stay in lockstep
/// with <see cref="OperationStatuses"/> from the runtime package.
/// </summary>
/// <remarks>
/// Each constant is defined in terms of the runtime <see cref="OperationStatuses"/>
/// counterpart rather than a hand-copied literal, so the two cannot silently drift:
/// if the runtime renames or removes a status, this class fails to compile.
/// </remarks>
public static class OperationStatus
{
    /// <summary>The operation has started.</summary>
    public const string Started = OperationStatuses.Started;

    /// <summary>The operation completed successfully.</summary>
    public const string Succeeded = OperationStatuses.Succeeded;

    /// <summary>The operation failed.</summary>
    public const string Failed = OperationStatuses.Failed;

    /// <summary>The operation is pending.</summary>
    public const string Pending = OperationStatuses.Pending;

    /// <summary>The operation timed out.</summary>
    public const string TimedOut = OperationStatuses.TimedOut;

    /// <summary>The operation was cancelled.</summary>
    public const string Cancelled = OperationStatuses.Cancelled;

    /// <summary>The operation was stopped.</summary>
    public const string Stopped = OperationStatuses.Stopped;

    /// <summary>The operation is ready to resume.</summary>
    public const string Ready = OperationStatuses.Ready;
}
