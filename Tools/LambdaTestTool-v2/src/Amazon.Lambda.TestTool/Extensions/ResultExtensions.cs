// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.TestTool.Models;

namespace Amazon.Lambda.TestTool.Extensions;

/// <summary>
/// A class for common API Gateway responses.
/// </summary>
public static class ApiGatewayResults
{
    /// <summary>
    /// Returns a 'Not Found' for HTTP API Gateway mode and 'Missing Authentication Token' for Rest.
    /// </summary>
    public static IResult RouteNotFound(HttpContext context, ApiGatewayEmulatorMode emulatorMode)
    {
        if (emulatorMode == ApiGatewayEmulatorMode.Rest)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.Headers.Append("x-amzn-errortype", "MissingAuthenticationTokenException");
            return Results.Json(new { message = "Missing Authentication Token" });
        }
        else
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return Results.Json(new { message = "Not Found" });
        }
    }
}
