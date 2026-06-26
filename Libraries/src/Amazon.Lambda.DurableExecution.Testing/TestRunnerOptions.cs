// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.Core;
using Microsoft.Extensions.Logging;

namespace Amazon.Lambda.DurableExecution.Testing;

/// <summary>
/// Configuration for the local durable test runner.
/// </summary>
public sealed record TestRunnerOptions
{
    /// <summary>
    /// When true, both <c>WaitAsync</c> timers and step/WaitForCondition retry
    /// backoffs complete immediately rather than waiting for real wall-clock time,
    /// so a workflow with day-long waits runs in milliseconds. Default: true.
    /// </summary>
    /// <remarks>
    /// The default differs from the JavaScript SDK (where time-skipping is opt-in).
    /// In .NET it defaults to true so the common "drive the whole workflow to
    /// completion" test does not block on real timers; set it to false to assert
    /// on real wait durations.
    /// </remarks>
    public bool SkipTime { get; init; } = true;

    /// <summary>
    /// Maximum number of handler invocations before throwing
    /// <see cref="TestExecutionLimitException"/>. Default: 100.
    /// </summary>
    public int MaxInvocations
    {
        get => _maxInvocations;
        init
        {
            if (value <= 0)
                throw new ArgumentOutOfRangeException(nameof(MaxInvocations), value, "MaxInvocations must be greater than zero.");
            _maxInvocations = value;
        }
    }
    private readonly int _maxInvocations = 100;

    /// <summary>
    /// Wall-clock timeout for a single <c>RunAsync</c> or <c>WaitForResultAsync</c> call.
    /// Default: 30 seconds.
    /// </summary>
    public TimeSpan DefaultTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Serializer used for step result deserialization. When null, uses
    /// <c>DefaultLambdaJsonSerializer</c> from Amazon.Lambda.Serialization.SystemTextJson.
    /// </summary>
    public ILambdaSerializer? Serializer { get; init; }

    /// <summary>
    /// Logger factory for runtime logging during test execution. Optional.
    /// </summary>
    public ILoggerFactory? LoggerFactory { get; init; }

    /// <summary>
    /// The durable execution ARN used in the test context. Override for tests that
    /// assert on ARN values. Default: a synthetic test ARN.
    /// </summary>
    public string DurableExecutionArn { get; init; } = "arn:aws:lambda:us-east-1:123456789012:execution:test-fn:test-execution";
}
