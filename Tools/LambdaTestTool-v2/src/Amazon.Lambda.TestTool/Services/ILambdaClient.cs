// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.Model;

namespace Amazon.Lambda.TestTool.Services;

/// <summary>
/// Represents a client for interacting with AWS Lambda services.
/// </summary>
public interface ILambdaClient
{
    /// <summary>
    /// Invokes a Lambda function asynchronously.
    /// </summary>
    /// <param name="request">The request object containing details for the Lambda function invocation.</param>
    /// <param name="endpoint">The endpoint for the lambda to connect invoke.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the response from the Lambda function invocation.</returns>
    Task<InvokeResponse> InvokeAsync(InvokeRequest request, string endpoint);
}
