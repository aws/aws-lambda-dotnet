// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

namespace Amazon.Lambda.TestTool.Models;

/// <summary>
/// Configuration options for invoking lambda functions.
/// </summary>
public class LambdaOptions
{
    /// <summary>
    /// Gets or sets the endpoint URL for Lambda function invocations.
    /// </summary>
    /// <value>
    /// A string containing the endpoint URL. Defaults to an empty string.
    /// </value>
    public string Endpoint { get; set; } = string.Empty;
}
