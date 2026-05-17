// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0
#if NET8_0_OR_GREATER
using System.Text.Json;

namespace Amazon.Lambda.Core;

/// <summary>
/// The options that can be overriden for structured logging.
/// </summary>
public class StructuredLoggingOptions
{
    /// <summary>
    /// Override the default JsonSerializerOptions used by the Lambda runtime for serializing object parameters in structured logs.
    /// </summary>
    public JsonSerializerOptions OverrideSerializerOptions { get; set; }
}
#endif
