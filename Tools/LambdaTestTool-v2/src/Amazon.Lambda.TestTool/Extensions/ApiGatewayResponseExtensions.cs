namespace Amazon.Lambda.TestTool.Extensions;

using Amazon.Lambda.APIGatewayEvents;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using System;
using System.IO;
using System.Text;

/// <summary>
/// Provides extension methods for converting API Gateway responses to HttpResponse objects.
/// </summary>
public static class ApiGatewayResponseExtensions
{
    /// <summary>
    /// Converts an APIGatewayProxyResponse to an HttpResponse.
    /// </summary>
    /// <param name="apiResponse">The API Gateway proxy response to convert.</param>
    /// <returns>An HttpResponse object representing the API Gateway response.</returns>
    public static HttpResponse ToHttpResponse(this APIGatewayProxyResponse apiResponse)
    {
        var httpContext = new DefaultHttpContext();
        var response = httpContext.Response;

        response.StatusCode = apiResponse.StatusCode;

        SetResponseHeaders(response, apiResponse.Headers, apiResponse.MultiValueHeaders);

        SetResponseBody(response, apiResponse.Body, apiResponse.IsBase64Encoded);

        return response;
    }

    /// <summary>
    /// Converts an APIGatewayHttpApiV2ProxyResponse to an HttpResponse.
    /// </summary>
    /// <param name="apiResponse">The API Gateway HTTP API v2 proxy response to convert.</param>
    /// <returns>An HttpResponse object representing the API Gateway response.</returns>
    public static HttpResponse ToHttpResponse(this APIGatewayHttpApiV2ProxyResponse apiResponse)
    {
        var httpContext = new DefaultHttpContext();
        var response = httpContext.Response;

        response.StatusCode = apiResponse.StatusCode;

        SetResponseHeaders(response, apiResponse.Headers);

        if (apiResponse.Cookies != null)
        {
            foreach (var cookie in apiResponse.Cookies)
            {
                response.Headers.Append("Set-Cookie", cookie);
            }
        }

        SetResponseBody(response, apiResponse.Body, apiResponse.IsBase64Encoded);

        return response;
    }

    /// <summary>
    /// Sets the headers on the HttpResponse object.
    /// </summary>
    /// <param name="response">The HttpResponse object to modify.</param>
    /// <param name="headers">The single-value headers to set.</param>
    /// <param name="multiValueHeaders">The multi-value headers to set.</param>
    private static void SetResponseHeaders(HttpResponse response, IDictionary<string, string>? headers, IDictionary<string, IList<string>>? multiValueHeaders = null)
    {
        if (headers != null)
        {
            foreach (var header in headers)
            {
                response.Headers[header.Key] = header.Value;
            }
        }

        if (multiValueHeaders != null)
        {
            foreach (var header in multiValueHeaders)
            {
                response.Headers[header.Key] = new StringValues(header.Value.ToArray());
            }
        }
    }

    /// <summary>
    /// Sets the body of the HttpResponse object.
    /// </summary>
    /// <param name="response">The HttpResponse object to modify.</param>
    /// <param name="body">The body content to set.</param>
    /// <param name="isBase64Encoded">Indicates whether the body is Base64 encoded.</param>
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
        }
    }
}
