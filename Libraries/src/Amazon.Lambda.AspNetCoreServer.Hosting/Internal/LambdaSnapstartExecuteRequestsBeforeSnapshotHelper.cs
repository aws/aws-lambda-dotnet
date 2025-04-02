// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.ApplicationLoadBalancerEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.RuntimeSupport.Helpers;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace Amazon.Lambda.AspNetCoreServer.Hosting.Internal;

#if NET8_0_OR_GREATER

/// <summary>
/// Contains the plumbing to register a user provided <see cref="Func{HttpClient, Task}"/> inside
/// <see cref="Amazon.Lambda.Core.SnapshotRestore.RegisterBeforeSnapshot"/>.
/// The function is meant to initialize the asp.net and lambda pipelines during
/// <see cref="Core.SnapshotRestore.RegisterBeforeSnapshot"/> and improve the
/// performance gains offered by SnapStart.
/// <para />
/// It works by construction a specialized <see cref="HttpClient" /> that will intercept requests
/// and saved them inside <see cref="LambdaSnapstartInitializerHttpMessageHandler.CapturedHttpRequestsJson" />.
/// <para /> 
/// Intercepted requests are then be processed later by <see cref="SnapstartHelperLambdaRequests.ExecuteSnapstartInitRequests"/>
/// which will route them correctly through a simulated asp.net/lambda pipeline.
/// </summary>
internal class LambdaSnapstartExecuteRequestsBeforeSnapshotHelper
{
    private readonly LambdaEventSource _lambdaEventSource;

    public LambdaSnapstartExecuteRequestsBeforeSnapshotHelper(LambdaEventSource lambdaEventSource)
    {
        _lambdaEventSource = lambdaEventSource;
    }

    /// <inheritdoc cref="RegisterInitializerRequests"/>
    public void RegisterInitializerRequests(HandlerWrapper handlerWrapper)
    {
        Amazon.Lambda.Core.SnapshotRestore.RegisterBeforeSnapshot(async () =>
        {
            foreach (var req in Registrar.GetAllRequests())
            {
                var json = await SnapstartHelperLambdaRequests.SerializeToJson(req, _lambdaEventSource);

                await SnapstartHelperLambdaRequests.ExecuteSnapstartInitRequests(json, times: 5, handlerWrapper);
            }
        });
    }

    
    /// <inheritdoc cref="ServiceCollectionExtensions.AddAWSLambdaBeforeSnapshotRequest"/>
    internal static BeforeSnapstartRequestRegistrar Registrar = new();

    internal class BeforeSnapstartRequestRegistrar
    {
        private readonly List<Func<IEnumerable<HttpRequestMessage>>> _beforeSnapstartRequests = new();

        
        public void Register(Func<IEnumerable<HttpRequestMessage>> beforeSnapstartRequests)
        {
            _beforeSnapstartRequests.Add(beforeSnapstartRequests);
        }

        internal IEnumerable<HttpRequestMessage> GetAllRequests()
        {
            foreach (var batch in _beforeSnapstartRequests)
                foreach (var r in batch())
                    yield return r;
        }
    }

    private class HelperLambdaContext : ILambdaContext, ICognitoIdentity, IClientContext
    {
        private LambdaEnvironment _lambdaEnvironment = new ();

        public string TraceId => string.Empty;
        public string AwsRequestId => string.Empty;
        public IClientContext ClientContext => this;
        public string FunctionName => _lambdaEnvironment.FunctionName;
        public string FunctionVersion => _lambdaEnvironment.FunctionVersion;
        public ICognitoIdentity Identity => this;
        public string InvokedFunctionArn => string.Empty;
        public ILambdaLogger Logger => null;
        public string LogGroupName => _lambdaEnvironment.LogGroupName;
        public string LogStreamName => _lambdaEnvironment.LogStreamName;
        public int MemoryLimitInMB => 128;
        public TimeSpan RemainingTime => TimeSpan.FromMilliseconds(100);
        public string IdentityId { get; }
        public string IdentityPoolId { get; }
        public IDictionary<string, string> Environment { get; } = new Dictionary<string, string>();
        public IClientApplication Client { get; }
        public IDictionary<string, string> Custom { get; } = new Dictionary<string, string>();
    }

    private static class SnapstartHelperLambdaRequests
    {
        private static readonly Uri _baseUri = new Uri("http://localhost");

        public static async Task ExecuteSnapstartInitRequests(string jsonRequest, int times, HandlerWrapper handlerWrapper)
        {
            var dummyRequest = new InvocationRequest(
                new MemoryStream(Encoding.UTF8.GetBytes(jsonRequest)),
                new HelperLambdaContext());

            for (var i = 0; i < times; i++)
            {
                try
                {
                    _ = await handlerWrapper.Handler.Invoke(dummyRequest);
                }
                catch (Exception e)
                {
                    Console.WriteLine("StartAsync: " + e.Message + e.StackTrace);
                }
            }
        }

        public static async Task<string> SerializeToJson(HttpRequestMessage request, LambdaEventSource lambdaType)
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

            string translatedRequestJson = lambdaType switch
            {
                LambdaEventSource.ApplicationLoadBalancer =>
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
                LambdaEventSource.HttpApi =>
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
                LambdaEventSource.RestApi =>
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
                    $"Unknown {nameof(LambdaEventSource)}: {Enum.GetName(lambdaType)}")
            };

            return translatedRequestJson;
        }

        private static async Task<string> ReadContent(HttpRequestMessage r)
        {
            if (r.Content == null)
                return string.Empty;

            return await r.Content.ReadAsStringAsync();
        }
    }
}


[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(ApplicationLoadBalancerRequest))]
[JsonSerializable(typeof(APIGatewayHttpApiV2ProxyRequest))]
[JsonSerializable(typeof(APIGatewayProxyRequest))]
[JsonSerializable(typeof(APIGatewayProxyRequest.ClientCertValidity))]
[JsonSerializable(typeof(APIGatewayProxyRequest.ProxyRequestClientCert))]
[JsonSerializable(typeof(APIGatewayProxyRequest.ProxyRequestContext))]
[JsonSerializable(typeof(APIGatewayProxyRequest.RequestIdentity))]

internal partial class LambdaRequestTypeClasses : JsonSerializerContext
{
}
#endif
