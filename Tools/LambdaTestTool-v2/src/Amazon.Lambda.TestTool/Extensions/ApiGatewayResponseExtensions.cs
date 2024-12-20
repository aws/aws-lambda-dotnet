// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.TestTool.Models;
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
    /// <param name="context">The <see cref="HttpContext"/> to use for the conversion.</param>
    /// <param name="emulatorMode">The <see cref="ApiGatewayEmulatorMode"/> to use for the conversion.</param>
    /// <returns>An <see cref="HttpResponse"/> representing the API Gateway response.</returns>
    public static void ToHttpResponse(this APIGatewayProxyResponse apiResponse, HttpContext httpContext, ApiGatewayEmulatorMode emulatorMode)
    {
        var response = httpContext.Response;
        response.Clear();

        SetResponseHeaders(response, apiResponse.Headers, emulatorMode, apiResponse.MultiValueHeaders);
        SetResponseBody(response, apiResponse.Body, apiResponse.IsBase64Encoded);
        SetContentTypeAndStatusCodeV1(response, apiResponse.Headers, apiResponse.MultiValueHeaders, apiResponse.StatusCode, emulatorMode);
    }

    /// <summary>
    /// Converts an <see cref="APIGatewayHttpApiV2ProxyResponse"/> to an <see cref="HttpResponse"/>.
    /// </summary>
    /// <param name="apiResponse">The API Gateway HTTP API v2 proxy response to convert.</param>
    /// <param name="context">The <see cref="HttpContext"/> to use for the conversion.</param>
    public static void ToHttpResponse(this APIGatewayHttpApiV2ProxyResponse apiResponse, HttpContext httpContext)
    {
        var response = httpContext.Response;
        response.Clear();

        SetResponseHeaders(response, apiResponse.Headers, ApiGatewayEmulatorMode.HttpV2);
        SetResponseBody(response, apiResponse.Body, apiResponse.IsBase64Encoded);
        SetContentTypeAndStatusCodeV2(response, apiResponse.Headers, apiResponse.StatusCode);
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
                response.Headers[header.Key] = new StringValues(header.Value.ToArray());
            }
        }

        if (headers != null)
        {
            foreach (var header in headers)
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
                headers.Add("x-amz-apigw-id", GenerateRequestId());
                headers.Add("X-Amzn-Trace-Id", GenerateTraceId());
                break;
            case ApiGatewayEmulatorMode.HttpV1:
            case ApiGatewayEmulatorMode.HttpV2:
                headers.Add("Apigw-Requestid", GenerateRequestId());
                break;
        }

        return headers;
    }

    /// <summary>
    /// Generates a random X-Amzn-Trace-Id for REST API mode.
    /// </summary>
    /// <returns>A string representing a random X-Amzn-Trace-Id in the format used by API Gateway for REST APIs.</returns>
    /// <remarks>
    /// The generated trace ID includes:
    /// - A root ID with a timestamp and two partial GUIDs
    /// - A parent ID
    /// - A sampling decision (always set to 0 in this implementation)
    /// - A lineage identifier
    /// </remarks>
    private static string GenerateTraceId()
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString("x");
        var guid1 = Guid.NewGuid().ToString("N");
        var guid2 = Guid.NewGuid().ToString("N");
        return $"Root=1-{timestamp}-{guid1.Substring(0, 12)}{guid2.Substring(0, 12)};Parent={Guid.NewGuid().ToString("N").Substring(0, 16)};Sampled=0;Lineage=1:{Guid.NewGuid().ToString("N").Substring(0, 8)}:0";
    }

    /// <summary>
    /// Generates a random API Gateway request ID for HTTP API v1 and v2.
    /// </summary>
    /// <returns>A string representing a random request ID in the format used by API Gateway for HTTP APIs.</returns>
    /// <remarks>
    /// The generated ID is a 15-character string consisting of lowercase letters and numbers, followed by an equals sign.
    /// </remarks>
    private static string GenerateRequestId()
    {
        return Guid.NewGuid().ToString("N").Substring(0, 8) + Guid.NewGuid().ToString("N").Substring(0, 7) + "=";
    }

    /// <summary>
    /// Sets the response body on the <see cref="HttpResponse"/>.
    /// </summary>
    /// <param name="response">The <see cref="HttpResponse"/> to set the body on.</param>
    /// <param name="body">The body content.</param>
    /// <param name="isBase64Encoded">Whether the body is Base64 encoded.</param>
    private static void SetResponseBody(HttpResponse response, string? body, bool isBase64Encoded)
    {
        if (!string.IsNullOrEmpty(body))
        {
            byte[] bodyBytes;
            if (isBase64Encoded)
            {
                bodyBytes = Convert.FromBase64String(body);
            }
            else
            {
                bodyBytes = Encoding.UTF8.GetBytes(body);
            }

            response.Body = new MemoryStream(bodyBytes);
            response.ContentLength = bodyBytes.Length;
        }
    }

    /// <summary>
    /// Sets the content type and status code for API Gateway v1 responses.
    /// </summary>
    /// <param name="response">The <see cref="HttpResponse"/> to set the content type and status code on.</param>
    /// <param name="headers">The single-value headers.</param>
    /// <param name="multiValueHeaders">The multi-value headers.</param>
    /// <param name="statusCode">The status code to set.</param>
    /// <param name="emulatorMode">The <see cref="ApiGatewayEmulatorMode"/> being used.</param>
    private static void SetContentTypeAndStatusCodeV1(HttpResponse response, IDictionary<string, string>? headers, IDictionary<string, IList<string>>? multiValueHeaders, int statusCode, ApiGatewayEmulatorMode emulatorMode)
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

        if (contentType != null)
        {
            response.ContentType = contentType;
        }
        else
        {
            if (emulatorMode == ApiGatewayEmulatorMode.HttpV1)
            {
                response.ContentType = "text/plain; charset=utf-8";
            }
            else if (emulatorMode == ApiGatewayEmulatorMode.Rest)
            {
                response.ContentType = "application/json";
            }
            else
            {
                throw new ArgumentException("This function should only be called for ApiGatewayEmulatorMode.HttpV1 or ApiGatewayEmulatorMode.Rest");
            }
        }

        if (statusCode != 0)
        {
            response.StatusCode = statusCode;
        }
        else
        {
            if (emulatorMode == ApiGatewayEmulatorMode.Rest) // rest api text for this message/error code is slightly different
            {
                response.StatusCode = 502;
                response.ContentType = "application/json";
                var errorBytes = Encoding.UTF8.GetBytes("{\"message\": \"Internal server error\"}");
                response.Body = new MemoryStream(errorBytes);
                response.ContentLength = errorBytes.Length;
                response.Headers["x-amzn-ErrorType"] = "InternalServerErrorException";
            }
            else
            {
                response.StatusCode = 500;
                response.ContentType = "application/json";
                var errorBytes = Encoding.UTF8.GetBytes("{\"message\":\"Internal Server Error\"}");
                response.Body = new MemoryStream(errorBytes);
                response.ContentLength = errorBytes.Length;
            }
        }
    }

    /// <summary>
    /// Sets the content type and status code for API Gateway v2 responses.
    /// </summary>
    /// <param name="response">The <see cref="HttpResponse"/> to set the content type and status code on.</param>
    /// <param name="headers">The headers.</param>
    /// <param name="statusCode">The status code to set.</param>
    private static void SetContentTypeAndStatusCodeV2(HttpResponse response, IDictionary<string, string>? headers, int statusCode)
    {
        if (headers != null && headers.TryGetValue("Content-Type", out var contentType))
        {
            response.ContentType = contentType;
        }
        else
        {
            response.ContentType = "text/plain; charset=utf-8"; // api gateway v2 defaults to this content type if none is provided
        }

        if (statusCode != 0)
        {
            response.StatusCode = statusCode;
        }
        else
        {
            response.StatusCode = 500;
            response.ContentType = "application/json";
            var errorBytes = Encoding.UTF8.GetBytes("{\"message\":\"Internal Server Error\"}");
            response.Body = new MemoryStream(errorBytes);
            response.ContentLength = errorBytes.Length;
        }
    }
}
