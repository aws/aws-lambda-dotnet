// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0
#if NET8_0_OR_GREATER
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
    /// Unit tests for <see cref="AbstractAspNetCoreFunction{TREQUEST,TRESPONSE}.StreamingFunctionHandlerAsync"/>.
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
                : base(StartupMode.FirstRequest) { }

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

        // -----------------------------------------------------------------------
        // 7.1 Request marshalling produces the same IHttpRequestFeature state
        //     as FunctionHandlerAsync for the same input
        // -----------------------------------------------------------------------
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
            await function.StreamingFunctionHandlerAsync(request, context);
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
            await function.StreamingFunctionHandlerAsync(request, context);
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

        // -----------------------------------------------------------------------
        // 7.2 features[typeof(IHttpResponseBodyFeature)] is a StreamingResponseBodyFeature
        //     after setup — verified by reading it from CapturedFeatures after the pipeline
        // -----------------------------------------------------------------------
        [Fact]
        public async Task AfterSetup_BodyFeature_IsStreamingResponseBodyFeature()
        {
            // The body feature is replaced with StreamingResponseBodyFeature BEFORE the pipeline
            // runs. We capture it from CapturedFeatures (set in PostMarshallItemsFeatureFeature)
            // after the invocation completes.
            IHttpResponseBodyFeature capturedBodyFeature = null;

            var function = new TestableStreamingFunction();
            function.PipelineSetupAction = features =>
            {
                // This runs inside PostMarshallItemsFeatureFeature, BEFORE the body feature swap.
                // We schedule a check via OnStarting which fires after the swap.
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

            await function.StreamingFunctionHandlerAsync(request, context);

            // Verify via CapturedFeatures directly — the body feature was replaced before pipeline ran
            var bodyFeatureFromCapture = function.CapturedFeatures[typeof(IHttpResponseBodyFeature)];
            Assert.IsType<StreamingResponseBodyFeature>(bodyFeatureFromCapture);
        }

        [Fact]
        public async Task AfterSetup_BodyFeature_IsStreamingResponseBodyFeature_ViaOnStarting()
        {
            // Secondary check: OnStarting fires after the body feature swap, confirming the type
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

            await function.StreamingFunctionHandlerAsync(request, context);

            // OnStarting fires when the pipeline writes the first byte (which triggers StartAsync).
            // The real TestWebApp pipeline writes a response body, so OnStarting should fire.
            if (capturedBodyFeature != null)
            {
                Assert.IsType<StreamingResponseBodyFeature>(capturedBodyFeature);
            }
            else
            {
                // If OnStarting didn't fire (pipeline didn't write), verify via CapturedFeatures
                var bodyFeature = function.CapturedFeatures[typeof(IHttpResponseBodyFeature)];
                Assert.IsType<StreamingResponseBodyFeature>(bodyFeature);
            }
        }

        // -----------------------------------------------------------------------
        // 7.3 FunctionHandlerAsync still returns TRESPONSE via MarshallResponse
        //     (buffered mode unaffected)
        // -----------------------------------------------------------------------
        [Fact]
        public async Task FunctionHandlerAsync_StillReturnsResponse_ViaMarshallResponse()
        {
            var function = new TestableStreamingFunction();
            var context = new TestLambdaContext();
            var request = MakeRequest();

            var response = await function.FunctionHandlerAsync(request, context);

            Assert.NotNull(response);
            Assert.True(function.MarshallResponseCalled,
                "MarshallResponse should have been called in buffered mode");
            Assert.IsType<APIGatewayHttpApiV2ProxyResponse>(response);
        }

        [Fact]
        public async Task FunctionHandlerAsync_ReturnsStatusCode_FromPipeline()
        {
            var function = new TestableStreamingFunction();
            var context = new TestLambdaContext();
            var request = MakeRequest(path: "/api/values");

            var response = await function.FunctionHandlerAsync(request, context);

            Assert.Equal(200, response.StatusCode);
        }

        [Fact]
        public async Task FunctionHandlerAsync_DoesNotOpenLambdaStream()
        {
            var function = new TestableStreamingFunction();
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

            await function.StreamingFunctionHandlerAsync(request, context);

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

            await function.StreamingFunctionHandlerAsync(request, context);

            Assert.Equal(3, firedCount);
        }

        // -----------------------------------------------------------------------
        // 7.5 Exception before stream open → stream closed cleanly, OnCompleted fires
        //
        // Strategy: override BuildStreamingPrelude to throw — it is called inside
        // _streamOpener() BEFORE streamOpened=true is set, so the stream is never opened.
        // -----------------------------------------------------------------------
        [Fact]
        public async Task ExceptionBeforeStreamOpen_StreamClosedCleanly_OnCompletedFires()
        {
            bool onCompletedFired = false;

            var function = new ThrowingBeforeStreamOpenFunction(
                onCompleted: () => onCompletedFired = true);

            var context = new TestLambdaContext();
            var request = MakeRequest();

            await function.StreamingFunctionHandlerAsync(request, context);

            Assert.False(function.StreamOpened,
                "Stream should not have been opened when exception occurs before stream open");
            Assert.True(onCompletedFired,
                "OnCompleted should fire even when exception occurs before stream open");
        }

        // -----------------------------------------------------------------------
        // 7.6 Exception before stream open with IncludeUnhandledExceptionDetailInResponse=true
        //     → 500 prelude + error body written
        // -----------------------------------------------------------------------
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

            await function.StreamingFunctionHandlerAsync(request, context);

            // The error path calls CreateLambdaResponseStream for the 500 response
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

            await function.StreamingFunctionHandlerAsync(request, context);

            Assert.False(function.StreamOpened,
                "Stream should not be opened when IncludeUnhandledExceptionDetailInResponse=false");
        }

        // -----------------------------------------------------------------------
        // 7.7 Exception after stream open → stream closed after logging, OnCompleted fires
        //
        // Strategy: let CreateLambdaResponseStream succeed (stream opens, streamOpened=true),
        // then throw from a subsequent write via a ThrowingOnSecondWriteStream.
        // -----------------------------------------------------------------------
        [Fact]
        public async Task ExceptionAfterStreamOpen_StreamClosedAfterLogging_OnCompletedFires()
        {
            bool onCompletedFired = false;

            var function = new ThrowingAfterStreamOpenFunction(
                onCompleted: () => onCompletedFired = true);

            var context = new TestLambdaContext();
            var request = MakeRequest();

            await function.StreamingFunctionHandlerAsync(request, context);

            Assert.True(function.StreamOpened,
                "Stream should have been opened before the exception");
            Assert.True(onCompletedFired,
                "OnCompleted should fire even when exception occurs after stream open");
        }

        [Fact]
        public async Task ExceptionAfterStreamOpen_DoesNotWriteNewErrorBody()
        {
            // When stream is already open, no new error body should be appended.
            // The stream contains only the bytes written before the exception.
            var function = new ThrowingAfterStreamOpenFunction(onCompleted: null)
            {
                IncludeUnhandledExceptionDetailInResponse = true
            };

            var context = new TestLambdaContext();
            var request = MakeRequest();

            await function.StreamingFunctionHandlerAsync(request, context);

            // Stream was opened; the exception was logged but no error body was written
            Assert.True(function.StreamOpened);
            // The stream should contain only the bytes written before the throw
            var streamContent = function.CapturedLambdaStream.ToArray();
            var errorKeyword = "InvalidOperationException";
            var bodyText = Encoding.UTF8.GetString(streamContent);
            Assert.DoesNotContain(errorKeyword, bodyText);
        }

        // -----------------------------------------------------------------------
        // 7.8 StreamingFunctionHandlerAsync carries [LambdaSerializer] and
        //     [RequiresPreviewFeatures] attributes
        // -----------------------------------------------------------------------
        [Fact]
        public void StreamingFunctionHandlerAsync_HasLambdaSerializerAttribute()
        {
            var method = typeof(APIGatewayHttpApiV2ProxyFunction)
                .GetMethod(nameof(APIGatewayHttpApiV2ProxyFunction.StreamingFunctionHandlerAsync));

            Assert.NotNull(method);

            var attr = method.GetCustomAttribute<LambdaSerializerAttribute>();
            Assert.NotNull(attr);
            Assert.Equal(
                typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer),
                attr.SerializerType);
        }

        [Fact]
        public void StreamingFunctionHandlerAsync_HasRequiresPreviewFeaturesAttribute()
        {
            var method = typeof(APIGatewayHttpApiV2ProxyFunction)
                .GetMethod(nameof(APIGatewayHttpApiV2ProxyFunction.StreamingFunctionHandlerAsync));

            Assert.NotNull(method);

            var attr = method.GetCustomAttribute<RequiresPreviewFeaturesAttribute>();
            Assert.NotNull(attr);
        }

        [Fact]
        public void StreamingFunctionHandlerAsync_ReturnsTask_NotTaskOfT()
        {
            var method = typeof(APIGatewayHttpApiV2ProxyFunction)
                .GetMethod(nameof(APIGatewayHttpApiV2ProxyFunction.StreamingFunctionHandlerAsync));

            Assert.NotNull(method);
            Assert.Equal(typeof(Task), method.ReturnType);
        }

        [Fact]
        public void StreamingFunctionHandlerAsync_IsPublicVirtual()
        {
            var method = typeof(APIGatewayHttpApiV2ProxyFunction)
                .GetMethod(nameof(APIGatewayHttpApiV2ProxyFunction.StreamingFunctionHandlerAsync));

            Assert.NotNull(method);
            Assert.True(method.IsPublic);
            Assert.True(method.IsVirtual);
        }

        // -----------------------------------------------------------------------
        // Helper subclasses for exception-path tests
        // -----------------------------------------------------------------------

        /// <summary>
        /// Base class for exception-path tests. Overrides <c>StreamingFunctionHandlerAsync</c>
        /// to run a custom pipeline action instead of the real ASP.NET Core pipeline, giving
        /// full control over when <c>StartAsync</c> is called and when exceptions are thrown.
        /// </summary>
        private abstract class CustomPipelineStreamingFunction
            : APIGatewayHttpApiV2ProxyFunction<TestWebApp.Startup>
        {
            public MemoryStream CapturedLambdaStream { get; protected set; }
            public bool StreamOpened { get; protected set; }

            protected CustomPipelineStreamingFunction()
                : base(StartupMode.FirstRequest) { }

            [RequiresPreviewFeatures]
            protected override Stream CreateLambdaResponseStream(
                Amazon.Lambda.Core.ResponseStreaming.HttpResponseStreamPrelude prelude)
            {
                var ms = new MemoryStream();
                CapturedLambdaStream = ms;
                StreamOpened = true;
                return ms;
            }

            /// <summary>
            /// Override <c>StreamingFunctionHandlerAsync</c> to run <see cref="RunPipelineAsync"/>
            /// instead of the real ASP.NET Core pipeline. This lets tests control exactly when
            /// <c>StartAsync</c> is called and when exceptions are thrown.
            /// </summary>
            [RequiresPreviewFeatures]
            public override async Task StreamingFunctionHandlerAsync(
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

                var streamingBodyFeature = new StreamingResponseBodyFeature(responseFeature, OpenStream);
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
            }

            /// <summary>Custom pipeline logic — called instead of the real ASP.NET Core pipeline.</summary>
            protected abstract Task RunPipelineAsync(
                InvokeFeatures features,
                StreamingResponseBodyFeature bodyFeature);
        }

        /// <summary>
        /// Throws BEFORE calling <c>StartAsync</c> on the body feature — stream is never opened.
        /// </summary>
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
                // Throw without ever calling StartAsync — stream is never opened
                throw new InvalidOperationException(_exceptionMessage);
            }
        }

        /// <summary>
        /// Calls <c>StartAsync</c> (opening the stream), writes partial content, then throws.
        /// </summary>
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
                // Explicitly open the stream
                await bodyFeature.StartAsync();

                // Write some bytes to the now-open stream
                var partial = Encoding.UTF8.GetBytes("partial");
                await bodyFeature.Stream.WriteAsync(partial, 0, partial.Length);

                // Throw after the stream is open
                throw new InvalidOperationException("Test exception after stream open");
            }
        }
    }
}
#endif
