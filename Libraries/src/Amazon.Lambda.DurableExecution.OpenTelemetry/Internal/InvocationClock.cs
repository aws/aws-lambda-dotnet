// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace Amazon.Lambda.DurableExecution.OpenTelemetry;

/// <summary>
/// The time of an invocation: the wall clock read once at the start, advanced by the monotonic clock. Spans of one
/// invocation then never overlap or collapse because the wall clock was adjusted while it ran, and they still line
/// up with spans the application creates, which read the wall clock.
/// </summary>
internal sealed class InvocationClock
{
    private readonly DateTimeOffset _wallStart = DateTimeOffset.UtcNow;
    private readonly long _monotonicStart = Stopwatch.GetTimestamp();

    internal DateTimeOffset Now => _wallStart + Stopwatch.GetElapsedTime(_monotonicStart);
}
