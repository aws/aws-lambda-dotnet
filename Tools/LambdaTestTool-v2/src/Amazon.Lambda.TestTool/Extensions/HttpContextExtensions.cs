namespace Amazon.Lambda.TestTool.Extensions;

using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.TestTool.Models;
using Amazon.Lambda.TestTool.Services;
using Amazon.Lambda.TestTool.Utilities;
using System.Text;
using System.Web;
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
    public static APIGatewayHttpApiV2ProxyRequest ToApiGatewayHttpV2Request(
        this HttpContext context,
        ApiGatewayRouteConfig apiGatewayRouteConfig)
    {
        var request = context.Request;

        var pathParameters = RouteTemplateUtility.ExtractPathParameters(apiGatewayRouteConfig.Path, request.Path);

        // Format 2.0 doesn't have multiValueHeaders or multiValueQueryStringParameters fields. Duplicate headers are combined with commas and included in the headers field.
        var (_, allHeaders) = HttpRequestUtility.ExtractHeaders(request.Headers);
        var headers = allHeaders.ToDictionary(
            kvp => kvp.Key,
            kvp => string.Join(",", kvp.Value)
        );

        // Duplicate query strings are combined with commas and included in the queryStringParameters field.
        var (_, allQueryParams) = HttpRequestUtility.ExtractQueryStringParameters(request.Query);
        var queryStringParameters = allQueryParams.ToDictionary(
            kvp => kvp.Key,
            kvp => string.Join(",", kvp.Value)
        );

        var httpApiV2ProxyRequest = new APIGatewayHttpApiV2ProxyRequest
        {
            RouteKey = $"{request.Method} {apiGatewayRouteConfig.Path}",
            RawPath = request.Path.Value, // this should be decoded value
            Body = HttpRequestUtility.ReadRequestBody(request),
            IsBase64Encoded = false,
            RequestContext = new ProxyRequestContext
            {
                Http = new HttpDescription
                {
                    Method = request.Method,
                    Path = request.Path.Value, // this should be decoded value
                    Protocol = request.Protocol
                },
                RouteKey = $"{request.Method} {apiGatewayRouteConfig.Path}"
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
            httpApiV2ProxyRequest.Body = Convert.ToBase64String(Encoding.UTF8.GetBytes(httpApiV2ProxyRequest.Body));
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
    public static APIGatewayProxyRequest ToApiGatewayRequest(
        this HttpContext context,
        ApiGatewayRouteConfig apiGatewayRouteConfig)
    {
        var request = context.Request;

        var pathParameters = RouteTemplateUtility.ExtractPathParameters(apiGatewayRouteConfig.Path, request.Path);

        var (headers, multiValueHeaders) = HttpRequestUtility.ExtractHeaders(request.Headers);
        var (queryStringParameters, multiValueQueryStringParameters) = HttpRequestUtility.ExtractQueryStringParameters(request.Query);

        var proxyRequest = new APIGatewayProxyRequest
        {
            Resource = apiGatewayRouteConfig.Path,
            Path = request.Path.Value,
            HttpMethod = request.Method,
            Body = HttpRequestUtility.ReadRequestBody(request),
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
            // this should be decoded value
            proxyRequest.PathParameters = pathParameters;
        }

        if (HttpRequestUtility.IsBinaryContent(request.ContentType))
        {
            proxyRequest.Body = Convert.ToBase64String(Encoding.UTF8.GetBytes(proxyRequest.Body));
            proxyRequest.IsBase64Encoded = true;
        }

        return proxyRequest;
    }
}
