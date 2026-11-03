// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

namespace Amazon.Lambda.DurableExecution.Internal;

/// <summary>
/// Per-invocation settings for <see cref="DurableFunction"/> that tests override.
/// Production code uses the defaults.
/// </summary>
internal sealed class DurableInvocationOptions
{
    /// <summary>
    /// How long checkpoint cleanup may run after the invocation deadline before
    /// the SDK gives up on it. Only applies to Lambda Managed Instances.
    /// </summary>
    public static readonly TimeSpan DefaultCleanupTimeout = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Overrides detection of Lambda Managed Instances. <c>null</c> reads the
    /// <c>AWS_LAMBDA_MAX_CONCURRENCY</c> environment variable.
    /// </summary>
    public bool? IsMultiConcurrency { get; init; }

    /// <summary>See <see cref="DefaultCleanupTimeout"/>.</summary>
    public TimeSpan CleanupTimeout { get; init; } = DefaultCleanupTimeout;
}
