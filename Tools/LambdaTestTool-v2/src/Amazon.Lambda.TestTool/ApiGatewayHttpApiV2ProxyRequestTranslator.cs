using Amazon.Lambda.APIGatewayEvents;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using static Amazon.Lambda.APIGatewayEvents.APIGatewayHttpApiV2ProxyRequest;

namespace Amazon.Lambda.TestTool
{
    /// <summary>
    /// Translates ASP.NET Core HTTP requests to API Gateway HTTP API v2 Proxy Requests.
    /// </summary>
    public class ApiGatewayHttpApiV2ProxyRequestTranslator : IApiGatewayRequestTranslator
    {
        private readonly IHttpRequestUtility _httpRequestUtility;

        /// <summary>
        /// Initializes a new instance of the <see cref="ApiGatewayHttpApiV2ProxyRequestTranslator"/> class.
        /// </summary>
        /// <param name="httpRequestUtility">The HTTP request utility used for extracting request details.</param>
        public ApiGatewayHttpApiV2ProxyRequestTranslator(IHttpRequestUtility httpRequestUtility)
        {
            _httpRequestUtility = httpRequestUtility;
        }

        /// <summary>
        /// Translates an ASP.NET Core HTTP request to an API Gateway HTTP API v2 Proxy Request.
        /// </summary>
        /// <param name="request">The ASP.NET Core HTTP request to translate.</param>
        /// <param name="pathParameters">The path parameters extracted from the request URL.</param>
        /// <param name="resource">The API Gateway resource path.</param>
        /// <returns>An <see cref="APIGatewayHttpApiV2ProxyRequest"/> object representing the translated request.</returns>
        public object TranslateFromHttpRequest(HttpRequest request, IDictionary<string, string> pathParameters, string resource)
        {
            var (headers, _) = _httpRequestUtility.ExtractHeaders(request.Headers);
            var (queryStringParameters, _) = _httpRequestUtility.ExtractQueryStringParameters(request.Query);

            var httpApiV2ProxyRequest = new APIGatewayHttpApiV2ProxyRequest
            {
                RouteKey = $"{request.Method} {resource}",
                RawPath = request.Path,
                RawQueryString = request.QueryString.Value,
                Cookies = request.Cookies.Select(c => $"{c.Key}={c.Value}").ToArray(),
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
                    RouteKey = $"{request.Method} {resource}"
                },
                Version = "2.0"
            };

            if (_httpRequestUtility.IsBinaryContent(request.ContentType))
            {
                httpApiV2ProxyRequest.Body = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(httpApiV2ProxyRequest.Body));
                httpApiV2ProxyRequest.IsBase64Encoded = true;
            }

            return httpApiV2ProxyRequest;
        }
    }
}
