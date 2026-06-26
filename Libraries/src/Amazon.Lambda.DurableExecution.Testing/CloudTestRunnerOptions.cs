// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.Core;

namespace Amazon.Lambda.DurableExecution.Testing;

/// <summary>
/// Configuration for the cloud durable test runner.
/// </summary>
public sealed record CloudTestRunnerOptions
{
    /// <summary>
    /// Delay before the first poll retry. Subsequent delays grow exponentially up
    /// to <see cref="PollInterval"/>, so the eventual-consistency window right after
    /// an Event invoke is probed quickly without then hammering the service.
    /// Default: 200 milliseconds.
    /// </summary>
    public TimeSpan InitialPollInterval { get; init; } = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// Maximum (steady-state) interval between state-polling calls. Default: 2 seconds.
    /// </summary>
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Wall-clock timeout for polling operations. Default: 5 minutes.
    /// </summary>
    public TimeSpan DefaultTimeout { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Serializer used for payload and result deserialization. When null, uses
    /// <c>DefaultLambdaJsonSerializer</c> from Amazon.Lambda.Serialization.SystemTextJson.
    /// </summary>
    public ILambdaSerializer? Serializer { get; init; }
}
