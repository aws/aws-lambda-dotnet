// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

namespace Amazon.Lambda.DurableExecution.Testing;

/// <summary>
/// Thrown by the cloud test runner when the cloud invocation fails in a way
/// specific to the test harness (e.g., missing DurableExecutionArn in the response).
/// </summary>
public sealed class CloudTestException : Exception
{
    /// <summary>
    /// Creates a new cloud test exception.
    /// </summary>
    public CloudTestException(string message) : base(message) { }

    /// <summary>
    /// Creates a new cloud test exception with an inner exception.
    /// </summary>
    public CloudTestException(string message, Exception innerException) : base(message, innerException) { }
}
