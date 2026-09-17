// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

namespace Amazon.Lambda.TestTool.Models;

/// <summary>
/// The API Gateway emulator integration used for a route.
/// </summary>
public enum ApiGatewayIntegrationType
{
    /// <summary>
    /// Translate the HTTP request into an API Gateway event and invoke a local Lambda via the Runtime API.
    /// </summary>
    Lambda,

    /// <summary>
    /// Reverse-proxy the HTTP request to <see cref="ApiGatewayRouteConfig.Endpoint"/> without Lambda event wrapping.
    /// </summary>
    Http
}
