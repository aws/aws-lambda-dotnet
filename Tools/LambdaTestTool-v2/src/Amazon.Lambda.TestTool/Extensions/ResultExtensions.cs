// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Text;
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
    /// <param name="context">The <see cref="HttpContext"/> to update.</param>
    /// <param name="emulatorMode">The API Gateway Emulator mode.</param>
    /// <returns></returns>
    public static async Task RouteNotFoundAsync(HttpContext context, ApiGatewayEmulatorMode emulatorMode)
    {
        if (emulatorMode == ApiGatewayEmulatorMode.Rest)
        {
            const string message = "{\"message\":\"Missing Authentication Token\"}";
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.Headers.Append("x-amzn-errortype", "MissingAuthenticationTokenException");
            await context.Response.Body.WriteAsync(UTF8Encoding.UTF8.GetBytes(message));
        }
        else
        {
            const string message = "{\"message\":\"Not Found\"}";
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.Body.WriteAsync(UTF8Encoding.UTF8.GetBytes(message));
        }
    }
}
