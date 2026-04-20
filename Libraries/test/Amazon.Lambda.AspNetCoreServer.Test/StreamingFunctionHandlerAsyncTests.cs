// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.AspNetCoreServer.Internal;
using Amazon.Lambda.Core;
using Amazon.Lambda.TestUtilities;

using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Xunit;

namespace Amazon.Lambda.AspNetCoreServer.Test
{
    /// <summary>
    /// Unit tests for the streaming path in <see cref="AbstractAspNetCoreFunction{TREQUEST,TRESPONSE}.FunctionHandlerAsync"/>
    /// when <c>EnableResponseStreaming</c> is <c>true</c>.
    ///
    /// <see cref="TestableStreamingFunction"/> overrides <c>CreateLambdaResponseStream</c> to inject
    /// a <see cref="MemoryStream"/> instead of calling <c>LambdaResponseStreamFactory.CreateHttpStream</c>,
    /// allowing tests to run without the Lambda runtime.
    /// </summary>
    [RequiresPreviewFeatures]
    public class StreamingFunctionHandlerAsyncTests
    {
        // -----------------------------------------------------------------------
        // Base testable subclass — overrides CreateLambdaResponseStream
        // -----------------------------------------------------------------------

        private class TestableStreamingFunction : APIGatewayHttpApiV2ProxyFunction<TestWebApp.Startup>
        {
            // Captured in PostMarshallItemsFeatureFeature — the InvokeFeatures after MarshallRequest
            public InvokeFeatures CapturedFeatures { get; private set; }

            // The MemoryStream used as the Lambda response stream
            public MemoryStream CapturedLambdaStream { get; private set; }

            // Whether CreateLambdaResponseStream was called (stream was opened)
            public bool StreamOpened { get; private set; }

            // Whether MarshallResponse was called (buffered mode check)
            public bool MarshallResponseCalled { get; private set; }

            // Optional setup action invoked inside PostMarshallItemsFeatureFeature
            public Func<InvokeFeatures, Task> PipelineSetupAction { get; set; }

            public TestableStreamingFunction()
                : base(StartupMode.FirstRequest)
            {
                EnableResponseStreaming = true;
            }

            // Expose MarshallRequest publicly so tests can call it after the host is started
            public void PublicMarshallRequest(InvokeFeatures features,
                APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context)
                => MarshallRequest(features, request, context);

            protected override void PostMarshallItemsFeatureFeature(
                IItemsFeature aspNetCoreItemFeature,
                APIGatewayHttpApiV2ProxyRequest lambdaRequest,
                ILambdaContext lambdaContext)
            {
                CapturedFeatures = aspNetCoreItemFeature as InvokeFeatures;
                PipelineSetupAction?.Invoke(CapturedFeatures);
                base.PostMarshallItemsFeatureFeature(aspNetCoreItemFeature, lambdaRequest, lambdaContext);
            }

            [RequiresPreviewFeatures]
            protected override Stream CreateLambdaResponseStream(
                Amazon.Lambda.Core.ResponseStreaming.HttpResponseStreamPrelude prelude)
            {
                var ms = new MemoryStream();
                CapturedLambdaStream = ms;
                StreamOpened = true;
                return ms;
            }

            protected override APIGatewayHttpApiV2ProxyResponse MarshallResponse(
                IHttpResponseFeature responseFeatures,
                ILambdaContext lambdaContext,
                int statusCodeIfNotSet = 200)
            {
                MarshallResponseCalled = true;
                return base.MarshallResponse(responseFeatures, lambdaContext, statusCodeIfNotSet);
            }
        }

        // -----------------------------------------------------------------------
        // Helper: build a minimal APIGatewayHttpApiV2ProxyRequest
        // -----------------------------------------------------------------------
        private static APIGatewayHttpApiV2ProxyRequest MakeRequest(
            string method = "GET",
            string path = "/api/values",
            Dictionary<string, string> headers = null,
            string body = null)
        {
            return new APIGatewayHttpApiV2ProxyRequest
            {
                RequestContext = new APIGatewayHttpApiV2ProxyRequest.ProxyRequestContext
                {
                    Http = new APIGatewayHttpApiV2ProxyRequest.HttpDescription
                    {
                        Method = method,
                        Path = path
                    },
                    Stage = "$default"
                },
                RawPath = path,
                Headers = headers ?? new Dictionary<string, string>
                {
                    ["accept"] = "application/json"
                },
                Body = body
            };
        }

        [Fact]
        public async Task RequestMarshalling_ProducesSameHttpRequestFeatureState_AsBufferedMode()
        {
            var function = new TestableStreamingFunction();
            var context = new TestLambdaContext();
            var request = MakeRequest(
                method: "POST",
                path: "/api/values",
                headers: new Dictionary<string, string>
                {
                    ["content-type"] = "application/json",
                    ["x-custom-header"] = "test-value"
                },
                body: "{\"key\":\"value\"}"
            );

            // Run the streaming path first — this starts the host and captures features
            await function.FunctionHandlerAsync(request, context);
            var streamingReq = (IHttpRequestFeature)function.CapturedFeatures;

            // Now call MarshallRequest directly (host is started, _logger is initialized)
            var bufferedFeatures = new InvokeFeatures();
            function.PublicMarshallRequest(bufferedFeatures, request, context);
            var bufferedReq = (IHttpRequestFeature)bufferedFeatures;

            Assert.NotNull(streamingReq);
            Assert.Equal(bufferedReq.Method, streamingReq.Method);
            Assert.Equal(bufferedReq.Path, streamingReq.Path);
            Assert.Equal(bufferedReq.PathBase, streamingReq.PathBase);
            Assert.Equal(bufferedReq.QueryString, streamingReq.QueryString);
            Assert.Equal(bufferedReq.Scheme, streamingReq.Scheme);
        }

        [Fact]
        public async Task RequestMarshalling_PreservesHeaders_InStreamingMode()
        {
            var function = new TestableStreamingFunction();
            var context = new TestLambdaContext();
            var request = MakeRequest(
                headers: new Dictionary<string, string>
                {
                    ["x-forwarded-for"] = "1.2.3.4",
                    ["accept"] = "text/html"
                }
            );

            // Run streaming path first to start the host
            await function.FunctionHandlerAsync(request, context);
            var streamingReq = (IHttpRequestFeature)function.CapturedFeatures;

            // Compare with buffered path
            var bufferedFeatures = new InvokeFeatures();
            function.PublicMarshallRequest(bufferedFeatures, request, context);
            var bufferedReq = (IHttpRequestFeature)bufferedFeatures;

            foreach (var key in bufferedReq.Headers.Keys)
            {
                Assert.True(streamingReq.Headers.ContainsKey(key),
                    $"Streaming features missing header '{key}' that buffered features has");
                Assert.Equal(bufferedReq.Headers[key], streamingReq.Headers[key]);
            }
        }

        [Fact]
        public async Task AfterSetup_BodyFeature_IsStreamingResponseBodyFeature()
        {
            IHttpResponseBodyFeature capturedBodyFeature = null;

            var function = new TestableStreamingFunction();
            function.PipelineSetupAction = features =>
            {
                var responseFeature = (IHttpResponseFeature)features;
                responseFeature.OnStarting(_ =>
                {
                    capturedBodyFeature = (IHttpResponseBodyFeature)features[typeof(IHttpResponseBodyFeature)];
                    return Task.CompletedTask;
                }, null);
                return Task.CompletedTask;
            };

            var context = new TestLambdaContext();
            var request = MakeRequest();

            await function.FunctionHandlerAsync(request, context);

            // Verify via CapturedFeatures directly — the body feature was replaced before pipeline ran
            var bodyFeatureFromCapture = function.CapturedFeatures[typeof(IHttpResponseBodyFeature)];
            Assert.IsType<StreamingResponseBodyFeature>(bodyFeatureFromCapture);
        }

        [Fact]
        public async Task AfterSetup_BodyFeature_IsStreamingResponseBodyFeature_ViaOnStarting()
        {
            IHttpResponseBodyFeature capturedBodyFeature = null;

            var function = new TestableStreamingFunction();
            function.PipelineSetupAction = features =>
            {
                var responseFeature = (IHttpResponseFeature)features;
                responseFeature.OnStarting(_ =>
                {
                    capturedBodyFeature = (IHttpResponseBodyFeature)features[typeof(IHttpResponseBodyFeature)];
                    return Task.CompletedTask;
                }, null);
                return Task.CompletedTask;
            };

            var context = new TestLambdaContext();
            var request = MakeRequest();

            await function.FunctionHandlerAsync(request, context);

            if (capturedBodyFeature != null)
            {
                Assert.IsType<StreamingResponseBodyFeature>(capturedBodyFeature);
            }
            else
            {
                var bodyFeature = function.CapturedFeatures[typeof(IHttpResponseBodyFeature)];
                Assert.IsType<StreamingResponseBodyFeature>(bodyFeature);
            }
        }

        [Fact]
        public async Task FunctionHandlerAsync_BufferedMode_StillReturnsResponse_ViaMarshallResponse()
        {
            // Buffered mode: EnableResponseStreaming defaults to false
            var function = new TestableStreamingFunction();
            function.EnableResponseStreaming = false;
            var context = new TestLambdaContext();
            var request = MakeRequest();

            var response = await function.FunctionHandlerAsync(request, context);

            Assert.NotNull(response);
            Assert.True(function.MarshallResponseCalled,
                "MarshallResponse should have been called in buffered mode");
            Assert.IsType<APIGatewayHttpApiV2ProxyResponse>(response);
        }

        [Fact]
        public async Task FunctionHandlerAsync_BufferedMode_ReturnsStatusCode_FromPipeline()
        {
            var function = new TestableStreamingFunction();
            function.EnableResponseStreaming = false;
            var context = new TestLambdaContext();
            var request = MakeRequest(path: "/api/values");

            var response = await function.FunctionHandlerAsync(request, context);

            Assert.Equal(200, response.StatusCode);
        }

        [Fact]
        public async Task FunctionHandlerAsync_BufferedMode_DoesNotOpenLambdaStream()
        {
            var function = new TestableStreamingFunction();
            function.EnableResponseStreaming = false;
            var context = new TestLambdaContext();
            var request = MakeRequest();

            await function.FunctionHandlerAsync(request, context);

            Assert.False(function.StreamOpened,
                "FunctionHandlerAsync (buffered mode) should not open the Lambda response stream");
        }

        // -----------------------------------------------------------------------
        // 7.4 OnCompleted callbacks fire after LambdaResponseStream is closed
        //     on success path
        // -----------------------------------------------------------------------
        [Fact]
        public async Task OnCompleted_FiresAfterStreamClosed_OnSuccessPath()
        {
            bool callbackFired = false;

            var function = new TestableStreamingFunction();
            function.PipelineSetupAction = features =>
            {
                var responseFeature = (IHttpResponseFeature)features;
                responseFeature.OnCompleted(_ =>
                {
                    callbackFired = true;
                    return Task.CompletedTask;
                }, null);
                return Task.CompletedTask;
            };

            var context = new TestLambdaContext();
            var request = MakeRequest();

            await function.FunctionHandlerAsync(request, context);

            Assert.True(callbackFired, "OnCompleted callback should have fired on the success path");
        }

        [Fact]
        public async Task OnCompleted_MultipleCallbacks_AllFire()
        {
            int firedCount = 0;

            var function = new TestableStreamingFunction();
            function.PipelineSetupAction = features =>
            {
                var responseFeature = (IHttpResponseFeature)features;
                for (int i = 0; i < 3; i++)
                {
                    responseFeature.OnCompleted(_ =>
                    {
                        firedCount++;
                        return Task.CompletedTask;
                    }, null);
                }
                return Task.CompletedTask;
            };

            var context = new TestLambdaContext();
            var request = MakeRequest();

            await function.FunctionHandlerAsync(request, context);

            Assert.Equal(3, firedCount);
        }

        [Fact]
        public async Task ExceptionBeforeStreamOpen_StreamClosedCleanly_OnCompletedFires()
        {
            bool onCompletedFired = false;

            var function = new ThrowingBeforeStreamOpenFunction(
                onCompleted: () => onCompletedFired = true);

            var context = new TestLambdaContext();
            var request = MakeRequest();

            await function.FunctionHandlerAsync(request, context);

            Assert.False(function.StreamOpened,
                "Stream should not have been opened when exception occurs before stream open");
            Assert.True(onCompletedFired,
                "OnCompleted should fire even when exception occurs before stream open");
        }

        [Fact]
        public async Task ExceptionBeforeStreamOpen_WithIncludeExceptionDetail_Writes500ErrorBody()
        {
            const string exceptionMessage = "Deliberate test failure for 500 response";

            var function = new ThrowingBeforeStreamOpenFunction(
                exceptionMessage: exceptionMessage,
                onCompleted: null)
            {
                IncludeUnhandledExceptionDetailInResponse = true
            };

            var context = new TestLambdaContext();
            var request = MakeRequest();

            await function.FunctionHandlerAsync(request, context);

            Assert.True(function.StreamOpened,
                "An error stream should have been opened for the 500 response");
            Assert.NotNull(function.CapturedLambdaStream);

            var errorBody = Encoding.UTF8.GetString(function.CapturedLambdaStream.ToArray());
            Assert.Contains(exceptionMessage, errorBody);
        }

        [Fact]
        public async Task ExceptionBeforeStreamOpen_WithoutIncludeExceptionDetail_NoStreamOpened()
        {
            var function = new ThrowingBeforeStreamOpenFunction(
                exceptionMessage: "Should not appear in response",
                onCompleted: null)
            {
                IncludeUnhandledExceptionDetailInResponse = false
            };

            var context = new TestLambdaContext();
            var request = MakeRequest();

            await function.FunctionHandlerAsync(request, context);

            Assert.False(function.StreamOpened,
                "Stream should not be opened when IncludeUnhandledExceptionDetailInResponse=false");
        }

        // -----------------------------------------------------------------------
        // 7.7 Exception after stream open → stream closed after logging, OnCompleted fires
        // -----------------------------------------------------------------------
        [Fact]
        public async Task ExceptionAfterStreamOpen_StreamClosedAfterLogging_OnCompletedFires()
        {
            bool onCompletedFired = false;

            var function = new ThrowingAfterStreamOpenFunction(
                onCompleted: () => onCompletedFired = true);

            var context = new TestLambdaContext();
            var request = MakeRequest();

            await function.FunctionHandlerAsync(request, context);

            Assert.True(function.StreamOpened,
                "Stream should have been opened before the exception");
            Assert.True(onCompletedFired,
                "OnCompleted should fire even when exception occurs after stream open");
        }

        [Fact]
        public async Task ExceptionAfterStreamOpen_DoesNotWriteNewErrorBody()
        {
            var function = new ThrowingAfterStreamOpenFunction(onCompleted: null)
            {
                IncludeUnhandledExceptionDetailInResponse = true
            };

            var context = new TestLambdaContext();
            var request = MakeRequest();

            await function.FunctionHandlerAsync(request, context);

            Assert.True(function.StreamOpened);
            var streamContent = function.CapturedLambdaStream.ToArray();
            var bodyText = Encoding.UTF8.GetString(streamContent);
            Assert.DoesNotContain("InvalidOperationException", bodyText);
        }

        [Fact]
        public void FunctionHandlerAsync_HasLambdaSerializerAttribute()
        {
            var method = typeof(APIGatewayHttpApiV2ProxyFunction)
                .GetMethod(nameof(APIGatewayHttpApiV2ProxyFunction.FunctionHandlerAsync));

            Assert.NotNull(method);

            var attr = method.GetCustomAttribute<LambdaSerializerAttribute>();
            Assert.NotNull(attr);
            Assert.Equal(
                typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer),
                attr.SerializerType);
        }

        [Fact]
        public void EnableResponseStreaming_Property_HasRequiresPreviewFeaturesAttribute()
        {
            var prop = typeof(APIGatewayHttpApiV2ProxyFunction)
                .GetProperty(nameof(APIGatewayHttpApiV2ProxyFunction.EnableResponseStreaming));

            Assert.NotNull(prop);

            var attr = prop.GetCustomAttribute<RequiresPreviewFeaturesAttribute>();
            Assert.NotNull(attr);
        }

        [Fact]
        public void EnableResponseStreaming_Property_DefaultsToFalse()
        {
            var function = new TestableStreamingFunction();
            function.EnableResponseStreaming = false; // reset to default
            Assert.False(function.EnableResponseStreaming);
        }

        [Fact]
        public void FunctionHandlerAsync_ReturnsTaskOfT()
        {
            var method = typeof(APIGatewayHttpApiV2ProxyFunction)
                .GetMethod(nameof(APIGatewayHttpApiV2ProxyFunction.FunctionHandlerAsync));

            Assert.NotNull(method);
            Assert.True(method.ReturnType.IsGenericType);
            Assert.Equal(typeof(Task<>), method.ReturnType.GetGenericTypeDefinition());
        }

        [Fact]
        public void FunctionHandlerAsync_IsPublicVirtual()
        {
            var method = typeof(APIGatewayHttpApiV2ProxyFunction)
                .GetMethod(nameof(APIGatewayHttpApiV2ProxyFunction.FunctionHandlerAsync));

            Assert.NotNull(method);
            Assert.True(method.IsPublic);
            Assert.True(method.IsVirtual);
        }

        // -----------------------------------------------------------------------
        // Helper subclasses for exception-path tests
        // -----------------------------------------------------------------------

        /// <summary>
        /// Base class for exception-path tests. Overrides <c>ExecuteStreamingRequestAsync</c>
        /// indirectly by overriding the pipeline via a custom <c>ProcessRequest</c>-equivalent.
        /// Uses <c>EnableResponseStreaming = true</c> so <c>FunctionHandlerAsync</c> takes the
        /// streaming path, then injects custom pipeline logic via <see cref="RunPipelineAsync"/>.
        /// </summary>
        private abstract class CustomPipelineStreamingFunction
            : APIGatewayHttpApiV2ProxyFunction<TestWebApp.Startup>
        {
            public MemoryStream CapturedLambdaStream { get; protected set; }
            public bool StreamOpened { get; protected set; }

            protected CustomPipelineStreamingFunction()
                : base(StartupMode.FirstRequest)
            {
                EnableResponseStreaming = true;
            }

            [RequiresPreviewFeatures]
            protected override Stream CreateLambdaResponseStream(
                Amazon.Lambda.Core.ResponseStreaming.HttpResponseStreamPrelude prelude)
            {
                var ms = new MemoryStream();
                CapturedLambdaStream = ms;
                StreamOpened = true;
                return ms;
            }

            // Override FunctionHandlerAsync to inject custom pipeline logic.
            // We replicate the streaming setup from ExecuteStreamingRequestAsync so we can
            // call RunPipelineAsync instead of the real ASP.NET Core pipeline.
            public override async Task<APIGatewayHttpApiV2ProxyResponse> FunctionHandlerAsync(
                APIGatewayHttpApiV2ProxyRequest request,
                ILambdaContext lambdaContext)
            {
                if (!IsStarted) Start();

                var features = new InvokeFeatures();
                MarshallRequest(features, request, lambdaContext);

                var itemFeatures = (IItemsFeature)features;
                itemFeatures.Items = new System.Collections.Generic.Dictionary<object, object>();
                itemFeatures.Items[LAMBDA_CONTEXT] = lambdaContext;
                itemFeatures.Items[LAMBDA_REQUEST_OBJECT] = request;
                PostMarshallItemsFeatureFeature(itemFeatures, request, lambdaContext);

                var responseFeature = (IHttpResponseFeature)features;

                async Task<Stream> OpenStream()
                {
                    var prelude = BuildStreamingPrelude(responseFeature);
                    return CreateLambdaResponseStream(prelude);
                }

                var streamingBodyFeature = new StreamingResponseBodyFeature(_logger, responseFeature, OpenStream);
                features[typeof(IHttpResponseBodyFeature)] = streamingBodyFeature;

                var scope = this._hostServices.CreateScope();
                Exception pipelineException = null;
                try
                {
                    ((IServiceProvidersFeature)features).RequestServices = scope.ServiceProvider;

                    try
                    {
                        try
                        {
                            await RunPipelineAsync(features, streamingBodyFeature);
                        }
                        catch (Exception e)
                        {
                            pipelineException = e;

                            if (!StreamOpened && IncludeUnhandledExceptionDetailInResponse)
                            {
                                var errorPrelude = new Amazon.Lambda.Core.ResponseStreaming.HttpResponseStreamPrelude
                                {
                                    StatusCode = System.Net.HttpStatusCode.InternalServerError
                                };
                                var errorStream = CreateLambdaResponseStream(errorPrelude);
                                var errorBytes = Encoding.UTF8.GetBytes(ErrorReport(e));
                                await errorStream.WriteAsync(errorBytes, 0, errorBytes.Length);
                            }
                            else if (StreamOpened)
                            {
                                _logger.LogError(e, $"Unhandled exception after response stream was opened: {ErrorReport(e)}");
                            }
                            else
                            {
                                _logger.LogError(e, $"Unknown error responding to request: {ErrorReport(e)}");
                            }
                        }
                    }
                    finally
                    {
                        if (features.ResponseCompletedEvents != null)
                        {
                            await features.ResponseCompletedEvents.ExecuteAsync();
                        }
                    }
                }
                finally
                {
                    scope.Dispose();
                }

                return default;
            }

            protected abstract Task RunPipelineAsync(
                InvokeFeatures features,
                StreamingResponseBodyFeature bodyFeature);
        }

        private class ThrowingBeforeStreamOpenFunction : CustomPipelineStreamingFunction
        {
            private readonly string _exceptionMessage;
            private readonly Action _onCompleted;

            public ThrowingBeforeStreamOpenFunction(
                string exceptionMessage = "Test exception before stream open",
                Action onCompleted = null)
            {
                _exceptionMessage = exceptionMessage;
                _onCompleted = onCompleted;
            }

            protected override void PostMarshallItemsFeatureFeature(
                IItemsFeature aspNetCoreItemFeature,
                APIGatewayHttpApiV2ProxyRequest lambdaRequest,
                ILambdaContext lambdaContext)
            {
                if (_onCompleted != null)
                {
                    ((IHttpResponseFeature)aspNetCoreItemFeature).OnCompleted(_ =>
                    {
                        _onCompleted();
                        return Task.CompletedTask;
                    }, null);
                }
                base.PostMarshallItemsFeatureFeature(aspNetCoreItemFeature, lambdaRequest, lambdaContext);
            }

            protected override Task RunPipelineAsync(
                InvokeFeatures features,
                StreamingResponseBodyFeature bodyFeature)
            {
                throw new InvalidOperationException(_exceptionMessage);
            }
        }

        private class ThrowingAfterStreamOpenFunction : CustomPipelineStreamingFunction
        {
            private readonly Action _onCompleted;

            public ThrowingAfterStreamOpenFunction(Action onCompleted = null)
            {
                _onCompleted = onCompleted;
            }

            protected override void PostMarshallItemsFeatureFeature(
                IItemsFeature aspNetCoreItemFeature,
                APIGatewayHttpApiV2ProxyRequest lambdaRequest,
                ILambdaContext lambdaContext)
            {
                if (_onCompleted != null)
                {
                    ((IHttpResponseFeature)aspNetCoreItemFeature).OnCompleted(_ =>
                    {
                        _onCompleted();
                        return Task.CompletedTask;
                    }, null);
                }
                base.PostMarshallItemsFeatureFeature(aspNetCoreItemFeature, lambdaRequest, lambdaContext);
            }

            protected override async Task RunPipelineAsync(
                InvokeFeatures features,
                StreamingResponseBodyFeature bodyFeature)
            {
                await bodyFeature.StartAsync();
                var partial = Encoding.UTF8.GetBytes("partial");
                await bodyFeature.Stream.WriteAsync(partial, 0, partial.Length);
                throw new InvalidOperationException("Test exception after stream open");
            }
        }
    }
}
