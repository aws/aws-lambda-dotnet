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
    /// <returns>An <see cref="APIGatewayHttpApiV2ProxyRequest"/> object representing the translated request.</returns>
    public static APIGatewayHttpApiV2ProxyRequest ToApiGatewayHttpV2Request(
        this HttpContext context,
        ApiGatewayRouteConfig apiGatewayRouteConfig)
    {
        var request = context.Request;

        var pathParameters = RouteTemplateUtility.ExtractPathParameters(apiGatewayRouteConfig.Path, request.Path);

        var (headers, _) = HttpRequestUtility.ExtractHeaders(request.Headers);
        var (queryStringParameters, _) = HttpRequestUtility.ExtractQueryStringParameters(request.Query);

        var httpApiV2ProxyRequest = new APIGatewayHttpApiV2ProxyRequest
        {
            RouteKey = $"{request.Method} {apiGatewayRouteConfig.Path}",
            RawPath = request.Path,
            RawQueryString = request.QueryString.Value,
            Cookies = request.Cookies.Select(c => $"{c.Key}={c.Value}").ToArray(),
            Headers = headers,
            QueryStringParameters = queryStringParameters,
            PathParameters = pathParameters ?? new Dictionary<string, string>(),
            Body = HttpRequestUtility.ReadRequestBody(request),
            IsBase64Encoded = false,
            RequestContext = new ProxyRequestContext
            {
                Http = new HttpDescription
                {
                    Method = request.Method,
                    Path = request.Path,
                    Protocol = request.Protocol
                },
                RouteKey = $"{request.Method} {apiGatewayRouteConfig.Path}"
            },
            Version = "2.0"
        };

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
            Path = HttpUtility.UrlEncode(request.Path),
            HttpMethod = request.Method,
            Headers = headers,
            MultiValueHeaders = multiValueHeaders,
            QueryStringParameters = queryStringParameters,
            MultiValueQueryStringParameters = multiValueQueryStringParameters,
            PathParameters = pathParameters ?? new Dictionary<string, string>(),
            Body = HttpRequestUtility.ReadRequestBody(request),
            IsBase64Encoded = false
        };

        if (HttpRequestUtility.IsBinaryContent(request.ContentType))
        {
            proxyRequest.Body = Convert.ToBase64String(Encoding.UTF8.GetBytes(proxyRequest.Body));
            proxyRequest.IsBase64Encoded = true;
        }

        return proxyRequest;
    }
}
