// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text;
using System.Text.Json;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.RuntimeSupport.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace Amazon.Lambda.AspNetCoreServer.Hosting.Internal;

/// <summary>
/// Contains the plumbing to register a user provided <see cref="Func{HttpClient, Task}"/> inside
/// <see cref="Amazon.Lambda.Core.SnapshotRestore.RegisterBeforeSnapshot"/>.
/// The function is meant to initialize the asp.net and lambda pipelines during
/// <see cref="Core.SnapshotRestore.RegisterBeforeSnapshot"/> and improve the
/// performance gains offered by SnapStart.
/// <para />
/// It works by construction a specialized <see cref="HttpClient" /> that will intercept requests
/// and saved them inside <see cref="LambdaSnapstartInitializerHttpMessageHandler.CapturedHttpRequests" />.
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
    [RequiresUnreferencedCode("Serializes object to json")]
    public void RegisterInitializerRequests(HandlerWrapper handlerWrapper)
    {
        #if NET8_0_OR_GREATER

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
            foreach (var req in LambdaSnapstartInitializerHttpMessageHandler.CapturedHttpRequests)
            {
                var json = JsonSerializer.Serialize(req);

                await SnapstartHelperLambdaRequests.ExecuteSnapstartInitRequests(json, times: 5, handlerWrapper);
            }
        });

        #endif
    }

    /// <inheritdoc cref="ServiceCollectionExtensions.AddAWSLambdaBeforeSnapshotRequest"/>
    internal static BeforeSnapstartRequestRegistrar Registrar = new();

    internal class BeforeSnapstartRequestRegistrar
    {
        private List<Func<HttpClient, Task>> beforeSnapstartFuncs = new();

        public void Register(Func<HttpClient, Task> beforeSnapstartRequest)
        {
            beforeSnapstartFuncs.Add(beforeSnapstartRequest);
        }

        internal async Task Execute(HttpClient client)
        {
            foreach (var f in beforeSnapstartFuncs)
                await f(client);
        }
    }

    private static class SnapstartHelperLambdaRequests
    {
        private static InternalLogger _logger = InternalLogger.GetDefaultLogger();

        private static readonly RuntimeApiHeaders _fakeRuntimeApiHeaders = new(new Dictionary<string, IEnumerable<string>>
        {
            { RuntimeApiHeaders.HeaderAwsRequestId, new List<string>() },
            { RuntimeApiHeaders.HeaderTraceId, new List<string>() },
            { RuntimeApiHeaders.HeaderClientContext, new List<string>() },
            { RuntimeApiHeaders.HeaderCognitoIdentity, new List<string>() },
            { RuntimeApiHeaders.HeaderDeadlineMs, new List<string>() },
            { RuntimeApiHeaders.HeaderInvokedFunctionArn, new List<string>() },
        });

        public static async Task ExecuteSnapstartInitRequests(string jsonRequest, int times, HandlerWrapper handlerWrapper)
        {
            var dummyRequest = new InvocationRequest
            {
                InputStream = new MemoryStream(Encoding.UTF8.GetBytes(jsonRequest)),
                LambdaContext = new LambdaContext(
                    _fakeRuntimeApiHeaders,
                    new LambdaEnvironment(),
                    new SimpleLoggerWriter())
            };

            for (var i = 0; i < times; i++)
            {
                try
                {
                    _ = await handlerWrapper.Handler.Invoke(dummyRequest);
                }
                catch (Exception e)
                {
                    Console.WriteLine("StartAsync: " + e.Message + e.StackTrace);
                    _logger.LogError(e, "StartAsync: Custom Warmup Failure: " + e.Message + e.StackTrace);
                }
            }
        }
    }

    private class LambdaSnapstartInitializerHttpMessageHandler : HttpMessageHandler
    {
        private LambdaEventSource _lambdaEventSource;

        public static Uri BaseUri { get; } = new Uri("http://localhost");

        public static List<object> CapturedHttpRequests { get; } = new();

        public LambdaSnapstartInitializerHttpMessageHandler(LambdaEventSource lambdaEventSource)
        {
            _lambdaEventSource = lambdaEventSource;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Copy request to correct request, ie APIGatewayProxyRequest

            // TODO - IMPLEMENT
            var translatedRequest = new APIGatewayProxyRequest
            {
                Path = request.RequestUri.MakeRelativeUri(BaseUri).ToString(),
                HttpMethod = request.Method.ToString()
            };

            CapturedHttpRequests.Add(translatedRequest);

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
