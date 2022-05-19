using Amazon.Lambda.APIGatewayEvents;
using System.Net;
using System.Web;

namespace Amazon.Lambda.Annotations.Testing
{
    internal static class APIGatewayRequests
    {
        public static HttpResponseMessage ToHttpResponse(APIGatewayProxyResponse lambdaResponse, HttpRequestMessage request)
        {
            var response = new HttpResponseMessage
            {
                Content = new StringContent(lambdaResponse.Body),
                StatusCode = (HttpStatusCode)lambdaResponse.StatusCode,
                RequestMessage = request
            };

            if (lambdaResponse.MultiValueHeaders != null)
            {
                foreach (var keyValue in lambdaResponse.MultiValueHeaders)
                {
                    response.Headers.TryAddWithoutValidation(keyValue.Key, keyValue.Value);
                    response.Content.Headers.TryAddWithoutValidation(keyValue.Key, keyValue.Value);
                }
            }

            if (lambdaResponse.Headers != null)
            {
                foreach (var keyValue in lambdaResponse.Headers)
                {
                    response.Headers.TryAddWithoutValidation(keyValue.Key, keyValue.Value.Split(','));
                    response.Content.Headers.TryAddWithoutValidation(keyValue.Key, keyValue.Value.Split(','));
                }
            }

            return response;
        }
        public static HttpResponseMessage ToHttpResponse(APIGatewayHttpApiV2ProxyResponse lambdaResponse, HttpRequestMessage request)
        {
            var response = new HttpResponseMessage
            {
                Content = new StringContent(lambdaResponse.Body),
                StatusCode = (HttpStatusCode)lambdaResponse.StatusCode,
                RequestMessage = request
            };

            if (lambdaResponse.Headers != null)
            {
                foreach (var keyValue in lambdaResponse.Headers)
                {
                    response.Headers.TryAddWithoutValidation(keyValue.Key, keyValue.Value.Split(','));
                    response.Content.Headers.TryAddWithoutValidation(keyValue.Key, keyValue.Value.Split(','));
                }
            }

            return response;
        }

        public static async Task<APIGatewayHttpApiV2ProxyRequest> ToHttpApiV2Request(HttpRequestMessage request, Dictionary<string, string> pathParameters)
        {
            var queryValues = HttpUtility.ParseQueryString(request.RequestUri.Query);

            return new APIGatewayHttpApiV2ProxyRequest
            {
                Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(),
                PathParameters = pathParameters,
                Headers = request.Headers.ToDictionary(header => header.Key, header => string.Join(",", header.Value)),
                RawQueryString = request.RequestUri.Query,
                QueryStringParameters = queryValues.AllKeys.ToDictionary(key => key, key => queryValues.Get(key)),
                RawPath = request.RequestUri.AbsolutePath,
                RequestContext = new APIGatewayHttpApiV2ProxyRequest.ProxyRequestContext()
            };
        }

        public static async Task<APIGatewayProxyRequest> ToRestApiRequest(HttpRequestMessage request, Dictionary<string, string> pathParameters)
        {
            var queryValues = HttpUtility.ParseQueryString(request.RequestUri.Query);

            return new APIGatewayProxyRequest
            {
                Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(),
                PathParameters = pathParameters,
                Headers = request.Headers.ToDictionary(header => header.Key, header => string.Join(",", header.Value)),
                MultiValueHeaders = request.Headers.ToDictionary(header => header.Key, header => (IList<string>)header.Value.ToList()),
                MultiValueQueryStringParameters = queryValues.AllKeys.ToDictionary(key => key, key => (IList<string>)queryValues.GetValues(key).ToList()),
                QueryStringParameters = queryValues.AllKeys.ToDictionary(key => key, key => queryValues.Get(key)),
                HttpMethod = request.Method.ToString(),
                Path = request.RequestUri.AbsolutePath,
                RequestContext = new APIGatewayProxyRequest.ProxyRequestContext()
            };
        }
    }
}
