// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Serialization;

namespace Amazon.Lambda.TestTool.Models;

/// <summary>
/// Represents the configuration of a Lambda function
/// </summary>
public class ApiGatewayRouteConfig
{
    /// <summary>
    /// The name of the Lambda function
    /// </summary>
    public required string LambdaResourceName { get; set; }

    /// <summary>
    /// The endpoint of the local Lambda Runtime API
    /// </summary>
    public string? Endpoint { get; set; }

    /// <summary>
    /// The HTTP Method for the API Gateway endpoint
    /// </summary>
    public required string HttpMethod { get; set; }

    /// <summary>
    /// The API Gateway HTTP Path of the Lambda function
    /// </summary>
    public required string Path { get; set; }

    /// <summary>
    /// The integration used for this route. Defaults to <see cref="ApiGatewayIntegrationType.Lambda"/>.
    /// When <see cref="ApiGatewayIntegrationType.Http"/>, the request is proxied to <see cref="Endpoint"/> instead of invoking a Lambda.
    /// Serialized as a string in route-config JSON (for example <c>"Http"</c>).
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ApiGatewayIntegrationType IntegrationType { get; set; } = ApiGatewayIntegrationType.Lambda;
}
