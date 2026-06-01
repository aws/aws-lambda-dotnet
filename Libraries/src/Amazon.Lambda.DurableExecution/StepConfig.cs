// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

namespace Amazon.Lambda.DurableExecution;

/// <summary>
/// Configuration for step execution.
/// </summary>
public sealed class StepConfig
{
    /// <summary>
    /// Retry strategy for failed steps. When null (default), failures are not retried.
    /// </summary>
    public IRetryStrategy? RetryStrategy { get; set; }

    /// <summary>
    /// Controls whether a step may re-execute if the Lambda is re-invoked mid-attempt.
    /// Default is <see cref="StepSemantics.AtLeastOncePerRetry"/>.
    /// </summary>
    public StepSemantics Semantics { get; set; } = StepSemantics.AtLeastOncePerRetry;
}
