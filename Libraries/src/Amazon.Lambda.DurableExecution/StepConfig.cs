// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.Core;

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

    /// <summary>
    /// Optional serializer for this step's result payload. When <c>null</c> (default),
    /// the globally-registered <see cref="ILambdaSerializer"/> on
    /// <see cref="ILambdaContext.Serializer"/> is used. Set this to override how this
    /// step's result is serialized to the checkpoint and deserialized on replay,
    /// without affecting other operations or the handler's input/return value.
    /// </summary>
    /// <remarks>
    /// The serializer is part of the workflow's deterministic definition: it is
    /// re-resolved on every replay, so a step must be able to deserialize a result it
    /// previously serialized. Changing a step's serializer for an in-flight execution in
    /// a way that cannot read the stored payload will break replay.
    /// </remarks>
    public ILambdaSerializer? Serializer { get; set; }
}
