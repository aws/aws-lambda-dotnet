// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

namespace Amazon.Lambda.TestTool.Extensions;

using System.Web;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.TestTool.Models;
using Amazon.Lambda.TestTool.Utilities;
using static Amazon.Lambda.APIGatewayEvents.APIGatewayHttpApiV2ProxyRequest;

/// <summary>
/// Provides extension methods to translate an <see cref="HttpContext"/> to different types of API Gateway requests.
/// </summary>
public static class HttpContextExtensions
{
    /// <summary>
    /// Translates an <see cref="HttpContext"/> to an <see cref="APIGatewayHttpApiV2ProxyRequest"/>.
    /// </summary>
    /// <param name="context">The <see cref="HttpContext"/> to be translated.</param>
    /// <param name="apiGatewayRouteConfig">The configuration of the API Gateway route, including the HTTP method, path, and other metadata.</param>
    /// <returns>An <see cref="APIGatewayHttpApiV2ProxyRequest"/> object representing the translated request.</returns>
    public static async Task<APIGatewayHttpApiV2ProxyRequest> ToApiGatewayHttpV2Request(
        this HttpContext context,
        ApiGatewayRouteConfig apiGatewayRouteConfig)
    {
        var request = context.Request;
        var currentTime = DateTimeOffset.UtcNow;
        var body = await HttpRequestUtility.ReadRequestBody(request);
        var contentLength = HttpRequestUtility.CalculateContentLength(request, body);

        var pathParameters = RouteTemplateUtility.ExtractPathParameters(apiGatewayRouteConfig.Path, request.Path);

        // Format 2.0 doesn't have multiValueHeaders or multiValueQueryStringParameters fields. Duplicate headers are combined with commas and included in the headers field.
        // 2.0 also lowercases all header keys
        var (_, allHeaders) = HttpRequestUtility.ExtractHeaders(request.Headers, true);
        var headers = allHeaders.ToDictionary(
            kvp => kvp.Key,
            kvp => string.Join(", ", kvp.Value)
        );

        // Duplicate query strings are combined with commas and included in the queryStringParameters field.
        var (_, allQueryParams) = HttpRequestUtility.ExtractQueryStringParameters(request.Query);
        var queryStringParameters = allQueryParams.ToDictionary(
            kvp => kvp.Key,
            kvp => string.Join(",", kvp.Value)
        );

        string userAgent = request.Headers.UserAgent.ToString();

        if (!headers.ContainsKey("content-length"))
        {
            headers["content-length"] = contentLength.ToString();
        }

        if (!headers.ContainsKey("content-type"))
        {
            headers["content-type"] = "text/plain; charset=utf-8";
        }

        var httpApiV2ProxyRequest = new APIGatewayHttpApiV2ProxyRequest
        {
            RouteKey = $"{request.Method} {apiGatewayRouteConfig.Path}",
            RawPath = request.Path.Value, // this should be decoded value
            Body = body,
            IsBase64Encoded = false,
            RequestContext = new ProxyRequestContext
            {
                Http = new HttpDescription
                {
                    Method = request.Method,
                    Path = request.Path.Value, // this should be decoded value
                    Protocol = !string.IsNullOrEmpty(request.Protocol) ? request.Protocol : "HTTP/1.1", // defaults to http 1.1 if not provided
                    UserAgent = userAgent
                },
                Time = currentTime.ToString("dd/MMM/yyyy:HH:mm:ss") + " +0000",
                TimeEpoch = currentTime.ToUnixTimeMilliseconds(),
                RequestId = HttpRequestUtility.GenerateRequestId(),
                RouteKey = $"{request.Method} {apiGatewayRouteConfig.Path}",
            },
            Version = "2.0"
        };

        if (request.Cookies.Any())
        {
            httpApiV2ProxyRequest.Cookies = request.Cookies.Select(c => $"{c.Key}={c.Value}").ToArray();
        }

        if (headers.Any())
        {
            httpApiV2ProxyRequest.Headers = headers;
        }

        httpApiV2ProxyRequest.RawQueryString = string.Empty; // default is empty string

        if (queryStringParameters.Any())
        {
            // this should be decoded value
            httpApiV2ProxyRequest.QueryStringParameters = queryStringParameters;

            // this should be the url encoded value and not include the "?"
            // e.g. key=%2b%2b%2b
            httpApiV2ProxyRequest.RawQueryString = HttpUtility.UrlPathEncode(request.QueryString.Value?.Substring(1));

        }

        if (pathParameters.Any())
        {
            // this should be decoded value
            httpApiV2ProxyRequest.PathParameters = pathParameters;
        }

        if (HttpRequestUtility.IsBinaryContent(request.ContentType))
        {
            // we already converted it when we read the body so we dont need to re-convert it
            httpApiV2ProxyRequest.IsBase64Encoded = true;
        }

        return httpApiV2ProxyRequest;
    }

    /// <summary>
    /// Translates an <see cref="HttpContext"/> to an <see cref="APIGatewayProxyRequest"/>.
    /// </summary>
    /// <param name="context">The <see cref="HttpContext"/> to be translated.</param>
    /// <param name="apiGatewayRouteConfig">The configuration of the API Gateway route, including the HTTP method, path, and other metadata.</param>
    /// <returns>An <see cref="APIGatewayProxyRequest"/> object representing the translated request.</returns>
    public static async Task<APIGatewayProxyRequest> ToApiGatewayRequest(
        this HttpContext context,
        ApiGatewayRouteConfig apiGatewayRouteConfig,
        ApiGatewayEmulatorMode emulatorMode)
    {
        var request = context.Request;
        var body = await HttpRequestUtility.ReadRequestBody(request);
        var contentLength = HttpRequestUtility.CalculateContentLength(request, body);

        var pathParameters = RouteTemplateUtility.ExtractPathParameters(apiGatewayRouteConfig.Path, request.Path);

        var (headers, multiValueHeaders) = HttpRequestUtility.ExtractHeaders(request.Headers);
        var (queryStringParameters, multiValueQueryStringParameters) = HttpRequestUtility.ExtractQueryStringParameters(request.Query);

        if (!headers.ContainsKey("content-length") && emulatorMode != ApiGatewayEmulatorMode.Rest) // rest doesnt set content-length by default
        {
            headers["content-length"] = contentLength.ToString();
            multiValueHeaders["content-length"] = [contentLength.ToString()];
        }

        if (!headers.ContainsKey("content-type"))
        {
            headers["content-type"] = "text/plain; charset=utf-8";
            multiValueHeaders["content-type"] = ["text/plain; charset=utf-8"];
        }


        if (HttpRequestUtility.IsBinaryContent(request.ContentType) && emulatorMode == ApiGatewayEmulatorMode.Rest) // Rest mode with binary content never sends content length
        {
            headers.Remove("content-length");
            multiValueHeaders.Remove("content-length");
        }

        // This is the decoded value
        var path = request.Path.Value;

        if (emulatorMode == ApiGatewayEmulatorMode.HttpV1 || emulatorMode == ApiGatewayEmulatorMode.Rest) // rest and httpv1 uses the encoded value for path an
        {
            path = request.Path.ToUriComponent();
        }

        if (emulatorMode == ApiGatewayEmulatorMode.Rest) // rest uses encoded value for the path params
        {
            var encodedPathParameters = pathParameters.ToDictionary(
                    kvp => kvp.Key,
                    kvp => Uri.EscapeUriString(kvp.Value)); // intentionally using EscapeURiString over EscapeDataString since EscapeURiString correctly handles reserved characters :/?#[]@!$&'()*+,;= in this case
            pathParameters = encodedPathParameters;
        }

        var proxyRequest = new APIGatewayProxyRequest
        {
            Resource = apiGatewayRouteConfig.Path,
            Path = path,
            HttpMethod = request.Method,
            Body = body,
            IsBase64Encoded = false
        };

        if (headers.Any())
        {
            proxyRequest.Headers = headers;
        }

        if (multiValueHeaders.Any())
        {
            proxyRequest.MultiValueHeaders = multiValueHeaders;
        }

        if (queryStringParameters.Any())
        {
            // this should be decoded value
            proxyRequest.QueryStringParameters = queryStringParameters;
        }

        if (multiValueQueryStringParameters.Any())
        {
            // this should be decoded value
            proxyRequest.MultiValueQueryStringParameters = multiValueQueryStringParameters;
        }

        if (pathParameters.Any())
        {
            proxyRequest.PathParameters = pathParameters;
        }

        if (HttpRequestUtility.IsBinaryContent(request.ContentType))
        {
            proxyRequest.IsBase64Encoded = true;
        }

        return proxyRequest;
    }
}
