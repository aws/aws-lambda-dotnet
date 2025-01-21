// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.TestTool.Models;
using Amazon.Lambda.TestTool.Utilities;
using Microsoft.Extensions.Primitives;
using System.Text;

namespace Amazon.Lambda.TestTool.Extensions;

/// <summary>
/// Provides extension methods for converting API Gateway responses to <see cref="HttpResponse"/> objects.
/// </summary>
public static class ApiGatewayResponseExtensions
{
    /// <summary>
    /// Converts an <see cref="APIGatewayProxyResponse"/> to an <see cref="HttpResponse"/>.
    /// </summary>
    /// <param name="apiResponse">The API Gateway proxy response to convert.</param>
    /// <param name="httpContext">The <see cref="HttpContext"/> to use for the conversion.</param>
    /// <param name="emulatorMode">The <see cref="ApiGatewayEmulatorMode"/> to use for the conversion.</param>
    /// <returns>An <see cref="HttpResponse"/> representing the API Gateway response.</returns>
    public static async Task ToHttpResponseAsync(this APIGatewayProxyResponse apiResponse, HttpContext httpContext, ApiGatewayEmulatorMode emulatorMode)
    {
        var response = httpContext.Response;
        response.Clear();

        if (apiResponse.StatusCode == 0)
        {
            await SetErrorResponse(response, emulatorMode);
            return;
        }

        SetResponseHeaders(response, apiResponse.Headers, emulatorMode, apiResponse.MultiValueHeaders);
        response.StatusCode = apiResponse.StatusCode;
        await SetResponseBodyAsync(response, apiResponse.Body, apiResponse.IsBase64Encoded);
    }

    /// <summary>
    /// Converts an <see cref="APIGatewayHttpApiV2ProxyResponse"/> to an <see cref="HttpResponse"/>.
    /// </summary>
    /// <param name="apiResponse">The API Gateway HTTP API v2 proxy response to convert.</param>
    /// <param name="httpContext">The <see cref="HttpContext"/> to use for the conversion.</param>
    public static async Task ToHttpResponseAsync(this APIGatewayHttpApiV2ProxyResponse apiResponse, HttpContext httpContext)
    {
        var response = httpContext.Response;

        if (apiResponse.StatusCode == 0)
        {
            await SetErrorResponse(response, ApiGatewayEmulatorMode.HttpV2);
            return;
        }

        SetResponseHeaders(response, apiResponse.Headers, ApiGatewayEmulatorMode.HttpV2);
        response.StatusCode = apiResponse.StatusCode;
        await SetResponseBodyAsync(response, apiResponse.Body, apiResponse.IsBase64Encoded);
    }

    /// <summary>
    /// Sets the error response when the status code is 0 or an error occurs.
    /// </summary>
    /// <param name="response">The <see cref="HttpResponse"/> to set the error on.</param>
    /// <param name="emulatorMode">The <see cref="ApiGatewayEmulatorMode"/> determining the error format.</param>
    private static async Task SetErrorResponse(HttpResponse response, ApiGatewayEmulatorMode emulatorMode)
    {
        // Set default headers first
        var defaultHeaders = GetDefaultApiGatewayHeaders(emulatorMode);
        foreach (var header in defaultHeaders)
        {
            response.Headers[header.Key] = header.Value;
        }

        response.ContentType = "application/json";

        if (emulatorMode == ApiGatewayEmulatorMode.Rest)
        {
            response.StatusCode = 502;
            response.Headers["x-amzn-ErrorType"] = "InternalServerErrorException";
            var errorBytes = Encoding.UTF8.GetBytes("{\"message\": \"Internal server error\"}");
            response.ContentLength = errorBytes.Length;
            await response.Body.WriteAsync(errorBytes, CancellationToken.None);
        }
        else
        {
            response.StatusCode = 500;
            var errorBytes = Encoding.UTF8.GetBytes("{\"message\":\"Internal Server Error\"}");
            response.ContentLength = errorBytes.Length;
            await response.Body.WriteAsync(errorBytes, CancellationToken.None);
        }
    }

    /// <summary>
    /// Sets the response headers on the <see cref="HttpResponse"/>, including default API Gateway headers based on the emulator mode.
    /// </summary>
    /// <param name="response">The <see cref="HttpResponse"/> to set headers on.</param>
    /// <param name="headers">The single-value headers to set.</param>
    /// <param name="emulatorMode">The <see cref="ApiGatewayEmulatorMode"/> determining which default headers to include.</param>
    /// <param name="multiValueHeaders">The multi-value headers to set.</param>
    private static void SetResponseHeaders(HttpResponse response, IDictionary<string, string>? headers, ApiGatewayEmulatorMode emulatorMode, IDictionary<string, IList<string>>? multiValueHeaders = null)
    {
        // Set content type first based on headers
        SetContentType(response, headers, multiValueHeaders, emulatorMode);

        // Add default API Gateway headers
        var defaultHeaders = GetDefaultApiGatewayHeaders(emulatorMode);
        foreach (var header in defaultHeaders)
        {
            response.Headers[header.Key] = header.Value;
        }

        if (multiValueHeaders != null)
        {
            foreach (var header in multiValueHeaders)
            {
                if (header.Key != "Content-Type") // Skip Content-Type as it's already handled
                {
                    response.Headers[header.Key] = new StringValues(header.Value.ToArray());
                }
            }
        }

        if (headers != null)
        {
            foreach (var header in headers)
            {
                if (header.Key != "Content-Type") // Skip Content-Type as it's already handled
                {
                    if (!response.Headers.ContainsKey(header.Key))
                    {
                        response.Headers[header.Key] = header.Value;
                    }
                    else
                    {
                        response.Headers.Append(header.Key, header.Value);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Sets the content type for the response based on headers and emulator mode.
    /// </summary>
    /// <param name="response">The <see cref="HttpResponse"/> to set the content type on.</param>
    /// <param name="headers">The single-value headers.</param>
    /// <param name="multiValueHeaders">The multi-value headers.</param>
    /// <param name="emulatorMode">The <see cref="ApiGatewayEmulatorMode"/> determining the default content type.</param>
    private static void SetContentType(HttpResponse response, IDictionary<string, string>? headers, IDictionary<string, IList<string>>? multiValueHeaders, ApiGatewayEmulatorMode emulatorMode)
    {
        string? contentType = null;

        if (headers != null && headers.TryGetValue("Content-Type", out var headerContentType))
        {
            contentType = headerContentType;
        }
        else if (multiValueHeaders != null && multiValueHeaders.TryGetValue("Content-Type", out var multiValueContentType))
        {
            contentType = multiValueContentType.FirstOrDefault();
        }

        response.ContentType = contentType ?? GetDefaultContentType(emulatorMode);
    }

    /// <summary>
    /// Gets the default content type for the specified emulator mode.
    /// </summary>
    /// <param name="emulatorMode">The <see cref="ApiGatewayEmulatorMode"/> determining the default content type.</param>
    /// <returns>The default content type string.</returns>
    private static string GetDefaultContentType(ApiGatewayEmulatorMode emulatorMode)
    {
        return emulatorMode switch
        {
            ApiGatewayEmulatorMode.Rest => "application/json",
            ApiGatewayEmulatorMode.HttpV1 => "text/plain; charset=utf-8",
            ApiGatewayEmulatorMode.HttpV2 => "text/plain; charset=utf-8",
            _ => throw new ArgumentException($"Unsupported emulator mode: {emulatorMode}")
        };
    }

    /// <summary>
    /// Generates default API Gateway headers based on the specified emulator mode.
    /// </summary>
    /// <param name="emulatorMode">The <see cref="ApiGatewayEmulatorMode"/> determining which headers to generate.</param>
    /// <returns>A dictionary of default headers appropriate for the specified emulator mode.</returns>
    private static Dictionary<string, string> GetDefaultApiGatewayHeaders(ApiGatewayEmulatorMode emulatorMode)
    {
        var headers = new Dictionary<string, string>
        {
            { "Date", DateTime.UtcNow.ToString("r") },
            { "Connection", "keep-alive" }
        };

        switch (emulatorMode)
        {
            case ApiGatewayEmulatorMode.Rest:
                headers.Add("x-amzn-RequestId", Guid.NewGuid().ToString("D"));
                headers.Add("x-amz-apigw-id", HttpRequestUtility.GenerateRequestId());
                headers.Add("X-Amzn-Trace-Id", HttpRequestUtility.GenerateTraceId());
                break;
            case ApiGatewayEmulatorMode.HttpV1:
            case ApiGatewayEmulatorMode.HttpV2:
                headers.Add("Apigw-Requestid", HttpRequestUtility.GenerateRequestId());
                break;
        }

        return headers;
    }

    /// <summary>
    /// Sets the response body on the <see cref="HttpResponse"/>.
    /// </summary>
    /// <param name="response">The <see cref="HttpResponse"/> to set the body on.</param>
    /// <param name="body">The body content.</param>
    /// <param name="isBase64Encoded">Whether the body is Base64 encoded.</param>
    private static async Task SetResponseBodyAsync(HttpResponse response, string? body, bool isBase64Encoded)
    {
        if (string.IsNullOrEmpty(body))
        {
            return;
        }

        byte[] bodyBytes = isBase64Encoded
            ? Convert.FromBase64String(body)
            : Encoding.UTF8.GetBytes(body);

        response.ContentLength = bodyBytes.Length;
        await response.Body.WriteAsync(bodyBytes, CancellationToken.None);
    }
}
