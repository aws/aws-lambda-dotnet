using Amazon.Lambda.APIGatewayEvents;
using System;
using System.Collections.Generic;
using System.Text;
using System.Web;
using Microsoft.AspNetCore.Http;

namespace Amazon.Lambda.TestTool
{
    /// <summary>
    /// Translates ASP.NET Core HTTP requests to API Gateway Proxy Requests for REST API and HTTP API v1.
    /// </summary>
    public class ApiGatewayProxyRequestTranslator : IApiGatewayRequestTranslator
    {
        private readonly IHttpRequestUtility _httpRequestUtility;

        /// <summary>
        /// Initializes a new instance of the <see cref="ApiGatewayProxyRequestTranslator"/> class.
        /// </summary>
        /// <param name="httpRequestUtility">The HTTP request utility used for extracting request details.</param>
        public ApiGatewayProxyRequestTranslator(IHttpRequestUtility httpRequestUtility)
        {
            _httpRequestUtility = httpRequestUtility;
        }

        /// <summary>
        /// Translates an ASP.NET Core HTTP request to an API Gateway Proxy Request.
        /// </summary>
        /// <param name="request">The ASP.NET Core HTTP request to translate.</param>
        /// <param name="pathParameters">The path parameters extracted from the request URL.</param>
        /// <param name="resource">The API Gateway resource path.</param>
        /// <returns>An <see cref="APIGatewayProxyRequest"/> object representing the translated request.</returns>
        public object TranslateFromHttpRequest(HttpRequest request, IDictionary<string, string> pathParameters, string resource)
        {
            var (headers, multiValueHeaders) = _httpRequestUtility.ExtractHeaders(request.Headers);
            var (queryStringParameters, multiValueQueryStringParameters) = _httpRequestUtility.ExtractQueryStringParameters(request.Query);

            var proxyRequest = new APIGatewayProxyRequest
            {
                Resource = resource,
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
}
