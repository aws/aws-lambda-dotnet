#if NET8_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.ApplicationLoadBalancerEvents;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;

namespace Amazon.Lambda.AspNetCoreServer.Internal
{
    /// <summary>
    /// Helper class for converting a <see cref="HttpRequestMessage"/> to a known
    /// lambda request type like: <see cref="APIGatewayHttpApiV2ProxyRequest"/>,
    /// <see cref="ApplicationLoadBalancerRequest"/>, or <see cref="APIGatewayProxyRequest"/>.
    /// <para />
    /// Object is returned as a serialized string.
    /// <para />
    /// This is intended for internal use to support SnapStart initialization.  Not all properties
    /// may be full set.
    /// </summary>
    public class HttpRequestMessageConverter
    {
        private static readonly Uri _baseUri = new Uri("http://localhost");
        
        public static async Task<TRequest> ConvertToLambdaRequest<TRequest>(HttpRequestMessage request)
        {
            if (null == request.RequestUri)
            {
                throw new ArgumentException($"{nameof(HttpRequestMessage.RequestUri)} must be set.", nameof(request));
            }

            if (request.RequestUri.IsAbsoluteUri)
            {
                throw new ArgumentException($"{nameof(HttpRequestMessage.RequestUri)} must be relative.", nameof(request));
            }

            // make request absolut (relative to localhost) otherwise parsing the query will not work
            request.RequestUri = new Uri(_baseUri, request.RequestUri);


            var body = await ReadContent(request);
            var headers = request.Headers
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.FirstOrDefault(),
                    StringComparer.OrdinalIgnoreCase);
            var httpMethod = request.Method.ToString();
            var path = "/" + _baseUri.MakeRelativeUri(request.RequestUri);
            var rawQuery = request.RequestUri?.Query;
            var query = QueryHelpers.ParseNullableQuery(request.RequestUri?.Query);

            if (typeof(TRequest) == typeof(ApplicationLoadBalancerRequest))
            {
                return (TRequest)(object) new ApplicationLoadBalancerRequest
                {
                    Body = body,
                    Headers = headers,
                    Path = path,
                    HttpMethod = httpMethod,
                    QueryStringParameters = query?.ToDictionary(k => k.Key, v => v.Value.ToString())
                };
            }

            if (typeof(TRequest) == typeof(APIGatewayHttpApiV2ProxyRequest))
            {
                return (TRequest)(object)new APIGatewayHttpApiV2ProxyRequest
                {
                    Body = body,
                    Headers = headers,
                    RawPath = path,
                    RequestContext = new APIGatewayHttpApiV2ProxyRequest.ProxyRequestContext
                    {
                        Http = new APIGatewayHttpApiV2ProxyRequest.HttpDescription
                        {
                            Method = httpMethod,
                            Path = path
                        }
                    },
                    QueryStringParameters = query?.ToDictionary(k => k.Key, v => v.Value.ToString()),
                    RawQueryString = rawQuery
                };
            }

            if (typeof(TRequest) == typeof(APIGatewayProxyRequest))
            {
                return (TRequest)(object)new APIGatewayProxyRequest
                {
                    Body = body,
                    Headers = headers,
                    Path = path,
                    HttpMethod = httpMethod,
                    RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
                    {
                        HttpMethod = httpMethod,
                        Path = path
                    },
                    QueryStringParameters = query?.ToDictionary(k => k.Key, v => v.Value.ToString())
                };
            }

            throw new NotImplementedException(
                $"Unknown request type: {typeof(TRequest).FullName}");
        }

        private static async Task<string> ReadContent(HttpRequestMessage r)
        {
            if (r.Content == null)
                return string.Empty;

            return await r.Content.ReadAsStringAsync();
        }
    }
}
#endif
