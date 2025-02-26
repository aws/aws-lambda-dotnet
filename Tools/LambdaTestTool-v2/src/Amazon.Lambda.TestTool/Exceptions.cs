// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

namespace Amazon.Lambda.TestTool;

/// <summary>
/// Contains constant string values for AWS Lambda exception types.
/// </summary>
public class Exceptions
{
    /// <summary>
    /// Exception thrown when the request payload size exceeds AWS Lambda's limits.
    /// This occurs when the request payload is larger than 6 MB for synchronous invocations.
    /// </summary>
    public const string RequestEntityTooLargeException = "RequestEntityTooLargeException";
}
