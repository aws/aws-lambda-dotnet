// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.TestTool.Models;

namespace Amazon.Lambda.TestTool.Services;

/// <summary>
/// A service responsible for returning the <see cref="ApiGatewayRouteConfig"/>
/// of a specific Lambda function.
/// </summary>
public interface IApiGatewayRouteConfigService
{
    /// <summary>
    /// A method to match an HTTP Method and HTTP Path with an existing <see cref="ApiGatewayRouteConfig"/>.
    /// </summary>
    /// <param name="httpMethod">An HTTP Method</param>
    /// <param name="path">An HTTP Path</param>
    /// <returns>An <see cref="ApiGatewayRouteConfig"/> corresponding to Lambda function with an API Gateway HTTP Method and Path.</returns>
    ApiGatewayRouteConfig? GetRouteConfig(string httpMethod, string path);
}
