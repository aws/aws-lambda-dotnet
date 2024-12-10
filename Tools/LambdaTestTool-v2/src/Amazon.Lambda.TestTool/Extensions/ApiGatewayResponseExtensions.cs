namespace Amazon.Lambda.TestTool.Extensions;

using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.TestTool.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

/// <summary>
/// Provides extension methods for converting API Gateway responses to HttpResponse objects.
/// </summary>
public static class ApiGatewayResponseExtensions
{

    private const string InternalServerErrorMessage = "{\"message\":\"Internal Server Error\"}";

    /// <summary>
    /// Converts an APIGatewayProxyResponse to an HttpResponse.
    /// </summary>
    /// <param name="apiResponse">The API Gateway proxy response to convert.</param>
    /// <returns>An HttpResponse representing the API Gateway response.</returns>
    public static HttpResponse ToHttpResponse(this APIGatewayProxyResponse apiResponse, ApiGatewayEmulatorMode emulatorMode)
    {
        var httpContext = new DefaultHttpContext();
        var response = httpContext.Response;

        SetResponseHeaders(response, apiResponse.Headers, apiResponse.MultiValueHeaders, emulatorMode);
        SetResponseBody(response, apiResponse.Body, apiResponse.IsBase64Encoded, emulatorMode);
        SetContentTypeAndStatusCode(response, apiResponse.Headers, apiResponse.MultiValueHeaders, apiResponse.StatusCode, emulatorMode);

        return response;
    }

    /// <summary>
    /// Converts an APIGatewayHttpApiV2ProxyResponse to an HttpResponse.
    /// </summary>
    /// <param name="apiResponse">The API Gateway HTTP API v2 proxy response to convert.</param>
    /// <returns>An HttpResponse representing the API Gateway response.</returns>
    public static HttpResponse ToHttpResponse(this APIGatewayHttpApiV2ProxyResponse apiResponse)
    {
        var httpContext = new DefaultHttpContext();
        var response = httpContext.Response;

        SetResponseHeaders(response, apiResponse.Headers, emulatorMode: ApiGatewayEmulatorMode.HttpV2);
        SetResponseBody(response, apiResponse.Body, apiResponse.IsBase64Encoded, ApiGatewayEmulatorMode.HttpV2);
        SetContentTypeAndStatusCodeV2(response, apiResponse.Headers, apiResponse.Body, apiResponse.StatusCode);

        return response;
    }
    /// <summary>
    /// Sets the response headers on the HttpResponse, including default API Gateway headers based on the emulator mode.
    /// </summary>
    /// <param name="response">The HttpResponse to set headers on.</param>
    /// <param name="headers">The single-value headers to set.</param>
    /// <param name="multiValueHeaders">The multi-value headers to set.</param>
    /// <param name="emulatorMode">The API Gateway emulator mode determining which default headers to include.</param>
    private static void SetResponseHeaders(HttpResponse response, IDictionary<string, string>? headers, IDictionary<string, IList<string>>? multiValueHeaders = null, ApiGatewayEmulatorMode emulatorMode = ApiGatewayEmulatorMode.HttpV2)
    {
        var processedHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Add default API Gateway headers
        var defaultHeaders = GetDefaultApiGatewayHeaders(emulatorMode);
        foreach (var header in defaultHeaders)
        {
            response.Headers[header.Key] = header.Value;
            processedHeaders.Add(header.Key);
        }

        if (multiValueHeaders != null)
        {
            foreach (var header in multiValueHeaders)
            {
                response.Headers[header.Key] = new StringValues(header.Value.ToArray());
                processedHeaders.Add(header.Key);
            }
        }

        if (headers != null)
        {
            foreach (var header in headers)
            {
                if (!processedHeaders.Contains(header.Key))
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
    /// <param name="emulatorMode">The API Gateway emulator mode determining which headers to generate.</param>
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
                headers.Add("x-amz-apigw-id", GenerateApiGwId());
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
    /// Generates a random API Gateway ID for REST API mode.
    /// </summary>
    /// <returns>A string representing a random API Gateway ID in the format used by API Gateway for REST APIs.</returns>
    /// <remarks>
    /// The generated ID is a 12-character string where digits are replaced by letters (A-J), followed by an equals sign.
    private static string GenerateApiGwId()
    {
        return new string(Guid.NewGuid().ToString("N").Take(12).Select(c => char.IsDigit(c) ? (char)(c + 17) : c).ToArray()) + "=";
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
    /// The generated ID is a 14-character string consisting of lowercase letters and numbers, followed by an equals sign.
    private static string GenerateRequestId()
    {
        return Guid.NewGuid().ToString("N").Substring(0, 8) + Guid.NewGuid().ToString("N").Substring(0, 6) + "=";
    }

    /// <summary>
    /// Sets the response body on the HttpResponse.
    /// </summary>
    /// <param name="response">The HttpResponse to set the body on.</param>
    /// <param name="body">The body content.</param>
    /// <param name="isBase64Encoded">Whether the body is Base64 encoded.</param>
    private static void SetResponseBody(HttpResponse response, string? body, bool isBase64Encoded, ApiGatewayEmulatorMode apiGatewayEmulator)
    {
        if (!string.IsNullOrEmpty(body))
        {
            byte[] bodyBytes;
            if (isBase64Encoded && ApiGatewayEmulatorMode.Rest != apiGatewayEmulator)
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
    /// <param name="response">The HttpResponse to set the content type and status code on.</param>
    /// <param name="headers">The single-value headers.</param>
    /// <param name="multiValueHeaders">The multi-value headers.</param>
    /// <param name="statusCode">The status code to set.</param>
    private static void SetContentTypeAndStatusCode(HttpResponse response, IDictionary<string, string>? headers, IDictionary<string, IList<string>>? multiValueHeaders, int statusCode, ApiGatewayEmulatorMode emulatorMode)
    {
        string? contentType = null;

        if (headers != null && headers.TryGetValue("Content-Type", out var headerContentType))
        {
            contentType = headerContentType;
        }
        else if (multiValueHeaders != null && multiValueHeaders.TryGetValue("Content-Type", out var multiValueContentType))
        {
            contentType = multiValueContentType[0];
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
            } else
            {
                SetInternalServerError(response);
            }
        }
    }

    /// <summary>
    /// Sets the content type and status code for API Gateway v2 responses.
    /// </summary>
    /// <param name="response">The HttpResponse to set the content type and status code on.</param>
    /// <param name="headers">The headers.</param>
    /// <param name="body">The response body.</param>
    /// <param name="statusCode">The status code to set.</param>
    private static void SetContentTypeAndStatusCodeV2(HttpResponse response, IDictionary<string, string>? headers, string? body, int statusCode)
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
        // v2 tries to automatically make some assumptions if the body is valid json
        else if (IsValidJson(body))
        {
            // API Gateway 2.0 format version assumptions
            response.StatusCode = 200;
            response.ContentType = "application/json";
            // Note: IsBase64Encoded is assumed to be false, which is already the default behavior
        }
        else
        {
            // if all else fails, v2 will error out
            SetInternalServerError(response);
        }
    }

    /// <summary>
    /// Checks if the given string is valid JSON.
    /// </summary>
    /// <param name="strInput">The string to check.</param>
    /// <returns>True if the string is valid JSON, false otherwise.</returns>
    private static bool IsValidJson(string? strInput)
    {
        if (string.IsNullOrWhiteSpace(strInput)) { return false; }
        strInput = strInput.Trim();
        if ((strInput.StartsWith("{") && strInput.EndsWith("}")) ||
            (strInput.StartsWith("[") && strInput.EndsWith("]")))
        {
            try
            {
                var obj = JsonSerializer.Deserialize<object>(strInput);
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }
        // a regular string is consisered json in api gateway.
        return true;
    }

    /// <summary>
    /// Sets the response to an Internal Server Error (500) with a JSON error message.
    /// </summary>
    /// <param name="response">The HttpResponse to set the error on.</param>
    private static void SetInternalServerError(HttpResponse response)
    {
        response.StatusCode = 500;
        response.ContentType = "application/json";
        var errorBytes = Encoding.UTF8.GetBytes(InternalServerErrorMessage);
        response.Body = new MemoryStream(errorBytes);
        response.ContentLength = errorBytes.Length;
    }
}
