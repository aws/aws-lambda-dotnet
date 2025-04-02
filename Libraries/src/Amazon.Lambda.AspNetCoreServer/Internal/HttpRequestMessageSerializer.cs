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
using Microsoft.AspNetCore.WebUtilities;

#if NET8_0_OR_GREATER
namespace Amazon.Lambda.AspNetCoreServer.Internal
{
    /// <summary>
    /// Helper class for converting a <see cref="HttpRequestMessage"/> to a known
    /// lambda request type like: <see cref="APIGatewayHttpApiV2ProxyRequest"/>,
    /// <see cref="ApplicationLoadBalancerRequest"/>, or <see cref="APIGatewayProxyRequest"/>.
    /// <para />
    /// Object is returned as a serialized string.
    /// <para />
    /// This is intended for internal use to support Snapstart initialization.  Not all properties
    /// may be full set.
    /// </summary>
    public class HttpRequestMessageSerializer
    {
        private static readonly Uri _baseUri = new Uri("http://localhost");

        public static async Task<string> SerializeToJson<TRequest>(HttpRequestMessage request)
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

            var duckRequest = new
            {
                Body = await ReadContent(request),
                Headers = request.Headers
                    .ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value.FirstOrDefault(),
                        StringComparer.OrdinalIgnoreCase),
                HttpMethod = request.Method.ToString(),
                Path = "/" + _baseUri.MakeRelativeUri(request.RequestUri),
                RawQuery = request.RequestUri?.Query,
                Query = QueryHelpers.ParseNullableQuery(request.RequestUri?.Query)
            };

            string translatedRequestJson = typeof(TRequest) switch
            {
                var t when t == typeof(ApplicationLoadBalancerRequest) =>
                    JsonSerializer.Serialize(
                        new ApplicationLoadBalancerRequest
                        {
                            Body = duckRequest.Body,
                            Headers = duckRequest.Headers,
                            Path = duckRequest.Path,
                            HttpMethod = duckRequest.HttpMethod,
                            QueryStringParameters = duckRequest.Query?.ToDictionary(k => k.Key, v => v.Value.ToString())
                        },
                        LambdaRequestTypeClasses.Default.ApplicationLoadBalancerRequest),
                var t when t == typeof(APIGatewayHttpApiV2ProxyRequest) =>
                    JsonSerializer.Serialize(
                        new APIGatewayHttpApiV2ProxyRequest
                        {
                            Body = duckRequest.Body,
                            Headers = duckRequest.Headers,
                            RawPath = duckRequest.Path,
                            RequestContext = new APIGatewayHttpApiV2ProxyRequest.ProxyRequestContext
                            {
                                Http = new APIGatewayHttpApiV2ProxyRequest.HttpDescription
                                {
                                    Method = duckRequest.HttpMethod,
                                    Path = duckRequest.Path
                                }
                            },
                            QueryStringParameters = duckRequest.Query?.ToDictionary(k => k.Key, v => v.Value.ToString()),
                            RawQueryString = duckRequest.RawQuery
                        },
                        LambdaRequestTypeClasses.Default.APIGatewayHttpApiV2ProxyRequest),
                var t when t == typeof(APIGatewayProxyRequest) =>
                    JsonSerializer.Serialize(
                        new APIGatewayProxyRequest
                        {
                            Body = duckRequest.Body,
                            Headers = duckRequest.Headers,
                            Path = duckRequest.Path,
                            HttpMethod = duckRequest.HttpMethod,
                            RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
                            {
                                HttpMethod = duckRequest.HttpMethod
                            },
                            QueryStringParameters = duckRequest.Query?.ToDictionary(k => k.Key, v => v.Value.ToString())
                        },
                        LambdaRequestTypeClasses.Default.APIGatewayProxyRequest),
                _ => throw new NotImplementedException(
                    $"Unknown request type: {typeof(TRequest).FullName}")
            };

            return translatedRequestJson;
        }

        /// <summary>
        /// Specialized Deserializer that uses the AOT Compatible
        /// <see cref="LambdaRequestTypeClasses"/> to deserialize common
        /// Request types.
        /// </summary>
        public static TRequest Deserialize<TRequest>(string json)
        {
            return typeof(TRequest) switch
            {
                var t when t == typeof(ApplicationLoadBalancerRequest) =>
                    JsonSerializer.Deserialize<TRequest>(json, LambdaRequestTypeClasses.Default.ApplicationLoadBalancerRequest),
                var t when t == typeof(APIGatewayHttpApiV2ProxyRequest) =>
                    JsonSerializer.Deserialize<TRequest>(json, LambdaRequestTypeClasses.Default.APIGatewayHttpApiV2ProxyRequest),
                var t when t == typeof(APIGatewayProxyRequest) =>
                JsonSerializer.Deserialize<TRequest>(json, LambdaRequestTypeClasses.Default.APIGatewayProxyRequest),
                _ => throw new NotImplementedException(
                    $"Unknown request type: {typeof(TRequest).FullName}")
            };
        }

        private static async Task<string> ReadContent(HttpRequestMessage r)
        {
            if (r.Content == null)
                return string.Empty;

            return await r.Content.ReadAsStringAsync();
        }

        [JsonSourceGenerationOptions(WriteIndented = true)]
        [JsonSerializable(typeof(ApplicationLoadBalancerRequest))]
        [JsonSerializable(typeof(APIGatewayHttpApiV2ProxyRequest))]
    [JsonSerializable(typeof(APIGatewayHttpApiV2ProxyRequest.ProxyRequestContext))]
    [JsonSerializable(typeof(APIGatewayHttpApiV2ProxyRequest.ProxyRequestAuthentication))]
    [JsonSerializable(typeof(APIGatewayHttpApiV2ProxyRequest.ProxyRequestClientCert))]
    [JsonSerializable(typeof(APIGatewayHttpApiV2ProxyRequest.ClientCertValidity))]
    [JsonSerializable(typeof(APIGatewayHttpApiV2ProxyRequest.HttpDescription))]
    [JsonSerializable(typeof(APIGatewayHttpApiV2ProxyRequest.AuthorizerDescription))] 
    [JsonSerializable(typeof(APIGatewayHttpApiV2ProxyRequest.AuthorizerDescription.IAMDescription))]
    [JsonSerializable(typeof(APIGatewayHttpApiV2ProxyRequest.AuthorizerDescription.CognitoIdentityDescription))]
    [JsonSerializable(typeof(APIGatewayHttpApiV2ProxyRequest.AuthorizerDescription.JwtDescription))]
        [JsonSerializable(typeof(APIGatewayProxyRequest))]
        [JsonSerializable(typeof(APIGatewayProxyRequest.ClientCertValidity))]
        [JsonSerializable(typeof(APIGatewayProxyRequest.ProxyRequestClientCert))]
        [JsonSerializable(typeof(APIGatewayProxyRequest.ProxyRequestContext))]
        [JsonSerializable(typeof(APIGatewayProxyRequest.RequestIdentity))]
        internal partial class LambdaRequestTypeClasses : JsonSerializerContext
        {
        }
    }
}
#endif
