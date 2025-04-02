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
            // Construct specialized HttpClient that will intercept requests and saved them inside
            // LambdaSnapstartInitializerHttpMessageHandler.CapturedHttpRequests.
            //
            // They will be processed later by SnapstartHelperLambdaRequests.ExecuteSnapstartInitRequests which will
            // route them correctly through a simulated lambda pipeline.
            var messageHandlerThatCollectsRequests = new LambdaSnapstartInitializerHttpMessageHandler(_lambdaEventSource);

            var httpClientThatCollectsRequests = new HttpClient(messageHandlerThatCollectsRequests);
            httpClientThatCollectsRequests.BaseAddress = LambdaSnapstartInitializerHttpMessageHandler.BaseUri;

            // "Invoke" each registered request function.  Requests will be captured inside.
            // LambdaSnapstartInitializerHttpMessageHandler.CapturedHttpRequests.
            await Registrar.Execute(httpClientThatCollectsRequests);

            // Request are now in CapturedHttpRequests.  Serialize each one into a json object
            // and execute the request through the lambda pipeline (ie handlerWrapper).
            foreach (var json in LambdaSnapstartInitializerHttpMessageHandler.CapturedHttpRequestsJson)
            {
                await SnapstartHelperLambdaRequests.ExecuteSnapstartInitRequests(json, times: 5, handlerWrapper);
            }
        });
    }

    
    /// <inheritdoc cref="ServiceCollectionExtensions.AddAWSLambdaBeforeSnapshotRequest"/>
    internal static BeforeSnapstartRequestRegistrar Registrar = new();

    internal class BeforeSnapstartRequestRegistrar
    {
        private readonly List<Func<HttpClient, Task>> _beforeSnapstartFuncs = new();

        public void Register(Func<HttpClient, Task> beforeSnapstartRequest)
        {
            _beforeSnapstartFuncs.Add(beforeSnapstartRequest);
        }

        internal async Task Execute(HttpClient client)
        {
            foreach (var f in _beforeSnapstartFuncs)
                await f(client);
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
    }

    private class LambdaSnapstartInitializerHttpMessageHandler : HttpMessageHandler
    {
        private readonly LambdaEventSource _lambdaEventSource;

        public static Uri BaseUri { get; } = new Uri("http://localhost");

        public static List<string> CapturedHttpRequestsJson { get; } = new();

        public LambdaSnapstartInitializerHttpMessageHandler(LambdaEventSource lambdaEventSource)
        {
            _lambdaEventSource = lambdaEventSource;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (null == request.RequestUri)
                return new HttpResponseMessage(HttpStatusCode.OK);

            var duckRequest = new
            {
                Body = await ReadContent(request),
                Headers = request.Headers
                    .ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value.FirstOrDefault(),
                        StringComparer.OrdinalIgnoreCase),
                HttpMethod = request.Method.ToString(),
                Path = "/" + BaseUri.MakeRelativeUri(request.RequestUri),
                RawQuery = request.RequestUri?.Query,
                Query = QueryHelpers.ParseNullableQuery(request.RequestUri?.Query)
            };
            
            string translatedRequestJson = _lambdaEventSource switch
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
                    $"Unknown {nameof(LambdaEventSource)}: {Enum.GetName(_lambdaEventSource)}")
            };

            // NOTE: Any object added to CapturedHttpRequests must have it's type added
            // to the 
            CapturedHttpRequestsJson.Add(translatedRequestJson);

            return new HttpResponseMessage(HttpStatusCode.OK);
        }

        private async Task<string> ReadContent(HttpRequestMessage r)
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
