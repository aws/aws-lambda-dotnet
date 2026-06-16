// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

namespace Amazon.Lambda.DurableExecution.Testing;

/// <summary>
/// String constants matching the wire-format operation statuses.
/// Uses a static class instead of an enum so values stay in lockstep
/// with <see cref="OperationStatuses"/> from the runtime package.
/// </summary>
public static class OperationStatus
{
    /// <summary>The operation has started.</summary>
    public const string Started = "STARTED";

    /// <summary>The operation completed successfully.</summary>
    public const string Succeeded = "SUCCEEDED";

    /// <summary>The operation failed.</summary>
    public const string Failed = "FAILED";

    /// <summary>The operation is pending.</summary>
    public const string Pending = "PENDING";

    /// <summary>The operation timed out.</summary>
    public const string TimedOut = "TIMED_OUT";

    /// <summary>The operation was cancelled.</summary>
    public const string Cancelled = "CANCELLED";

    /// <summary>The operation was stopped.</summary>
    public const string Stopped = "STOPPED";

    /// <summary>The operation is ready to resume.</summary>
    public const string Ready = "READY";
}
