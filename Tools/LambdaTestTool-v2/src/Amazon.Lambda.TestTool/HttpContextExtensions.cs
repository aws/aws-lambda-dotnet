namespace Amazon.Lambda.TestTool;

using Amazon.Lambda.APIGatewayEvents;
using System.Text;
using System.Web;
using static Amazon.Lambda.APIGatewayEvents.APIGatewayHttpApiV2ProxyRequest;

/// <summary>
/// Provides extension methods to translate an <see cref="HttpContext"/> to different types of API Gateway requests.
/// </summary>
public static class HttpContextExtensions
{
    private static IHttpRequestUtility _httpRequestUtility = new HttpRequestUtility();
    private static IRouteConfigurationParser _routeConfigurationParser;

    public static void SetHttpRequestUtility(IHttpRequestUtility httpRequestUtility)
    {
        _httpRequestUtility = httpRequestUtility ?? throw new ArgumentNullException(nameof(httpRequestUtility));
    }

    public static void SetRouteConfigurationParser(IRouteConfigurationParser routeConfigurationParser)
    {
        _routeConfigurationParser = routeConfigurationParser ?? throw new ArgumentNullException(nameof(routeConfigurationParser));
    }

    /// <summary>
    /// Translates an <see cref="HttpContext"/> to an <see cref="APIGatewayHttpApiV2ProxyRequest"/>.
    /// </summary>
    /// <param name="context">The <see cref="HttpContext"/> to be translated.</param>
    /// <returns>An <see cref="APIGatewayHttpApiV2ProxyRequest"/> object representing the translated request.</returns>
    public static APIGatewayHttpApiV2ProxyRequest ToApiGatewayHttpV2Request(
        this HttpContext context)
    {
        var request = context.Request;

        var matchedConfig = _routeConfigurationParser.GetRouteConfig(request.Method, request.Path);
        var pathParameters = _routeConfigurationParser.ExtractPathParameters(matchedConfig, request.Path);

        var (headers, _) = _httpRequestUtility.ExtractHeaders(request.Headers);
        var (queryStringParameters, _) = _httpRequestUtility.ExtractQueryStringParameters(request.Query);

        var httpApiV2ProxyRequest = new APIGatewayHttpApiV2ProxyRequest
        {
            RouteKey = $"{request.Method} {matchedConfig.Path}",
            RawPath = request.Path,
            RawQueryString = request.QueryString.Value,
            Cookies = request.Cookies.Select(c => $"{c.Key}={HttpUtility.UrlEncode(c.Value)}").ToArray(),
            Headers = headers,
            QueryStringParameters = queryStringParameters,
            PathParameters = pathParameters ?? new Dictionary<string, string>(),
            Body = _httpRequestUtility.ReadRequestBody(request),
            IsBase64Encoded = false,
            RequestContext = new ProxyRequestContext
            {
                Http = new HttpDescription
                {
                    Method = request.Method,
                    Path = request.Path,
                    Protocol = request.Protocol
                },
                RouteKey = $"{request.Method} {matchedConfig.Path}"
            },
            Version = "2.0"
        };

        if (_httpRequestUtility.IsBinaryContent(request.ContentType))
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
        this HttpContext context)
    {
        var request = context.Request;

        var matchedConfig = _routeConfigurationParser.GetRouteConfig(request.Method, request.Path);
        var pathParameters = _routeConfigurationParser.ExtractPathParameters(matchedConfig, request.Path);

        var (headers, multiValueHeaders) = _httpRequestUtility.ExtractHeaders(request.Headers);
        var (queryStringParameters, multiValueQueryStringParameters) = _httpRequestUtility.ExtractQueryStringParameters(request.Query);

        var proxyRequest = new APIGatewayProxyRequest
        {
            Resource = matchedConfig.Path,
            Path = HttpUtility.UrlEncode(request.Path),
            HttpMethod = request.Method,
            Headers = headers,
            MultiValueHeaders = multiValueHeaders,
            QueryStringParameters = queryStringParameters,
            MultiValueQueryStringParameters = multiValueQueryStringParameters,
            PathParameters = pathParameters ?? new Dictionary<string, string>(),
            Body = _httpRequestUtility.ReadRequestBody(request),
            IsBase64Encoded = false
        };

        if (_httpRequestUtility.IsBinaryContent(request.ContentType))
        {
            proxyRequest.Body = Convert.ToBase64String(Encoding.UTF8.GetBytes(proxyRequest.Body));
            proxyRequest.IsBase64Encoded = true;
        }

        return proxyRequest;
    }
}
