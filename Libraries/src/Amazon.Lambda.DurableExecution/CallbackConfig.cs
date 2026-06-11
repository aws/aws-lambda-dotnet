// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

namespace Amazon.Lambda.DurableExecution;

/// <summary>
/// Configuration for callback operations created via
/// <see cref="IDurableContext.CreateCallbackAsync{T}(string?, CallbackConfig?, System.Threading.CancellationToken)"/>.
/// </summary>
public class CallbackConfig
{
    private TimeSpan _timeout = TimeSpan.Zero;
    private TimeSpan _heartbeatTimeout = TimeSpan.Zero;

    /// <summary>
    /// Maximum total time the service will wait for the external system to
    /// complete the callback. <see cref="TimeSpan.Zero"/> (default) means no
    /// overall timeout — only <see cref="HeartbeatTimeout"/> applies (if set).
    /// </summary>
    /// <remarks>
    /// The service's timer granularity is 1 second, so values strictly between
    /// <see cref="TimeSpan.Zero"/> and 1 second are rejected to avoid silent
    /// rounding. Use <see cref="TimeSpan.Zero"/> to disable the timeout, or a
    /// value of at least 1 second.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when set to a positive value less than 1 second.
    /// </exception>
    public TimeSpan Timeout
    {
        get => _timeout;
        set
        {
            ValidateTimeout(value, nameof(Timeout));
            _timeout = value;
        }
    }

    /// <summary>
    /// Maximum gap between heartbeat signals from the external system before
    /// the service marks the callback as timed-out.
    /// <see cref="TimeSpan.Zero"/> (default) means no heartbeat timeout.
    /// </summary>
    /// <remarks>
    /// The service's timer granularity is 1 second, so values strictly between
    /// <see cref="TimeSpan.Zero"/> and 1 second are rejected to avoid silent
    /// rounding. Use <see cref="TimeSpan.Zero"/> to disable the heartbeat
    /// timeout, or a value of at least 1 second.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when set to a positive value less than 1 second.
    /// </exception>
    public TimeSpan HeartbeatTimeout
    {
        get => _heartbeatTimeout;
        set
        {
            ValidateTimeout(value, nameof(HeartbeatTimeout));
            _heartbeatTimeout = value;
        }
    }

    private static void ValidateTimeout(TimeSpan value, string paramName)
    {
        // Allow Zero (means "not set"); reject negative; reject sub-second
        // positive values to mirror WaitAsync's behavior and prevent silent
        // rounding-up inside BuildCallbackOptions.
        if (value < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                paramName, value, $"{paramName} must be non-negative.");
        }
        if (value > TimeSpan.Zero && value < TimeSpan.FromSeconds(1))
        {
            throw new ArgumentOutOfRangeException(
                paramName, value,
                $"{paramName} must be at least 1 second (or TimeSpan.Zero to disable).");
        }
    }
}
