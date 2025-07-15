// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

namespace Amazon.Lambda.TestTool.Models;

/// <summary>
/// Container representing a Lambda input request.
/// </summary>
public class LambdaRequest
{
    /// <summary>
    /// Name of the request
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Group of the request
    /// </summary>
    public required string Group { get; init; }

    /// <summary>
    /// File name of the request
    /// </summary>
    public required string Filename { get; init; }
}
