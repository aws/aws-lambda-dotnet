// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.AspNetCoreServer.Internal;
using Amazon.Lambda.Core;
using Amazon.Lambda.TestUtilities;

using Microsoft.AspNetCore.Http.Features;

using Xunit;

namespace Amazon.Lambda.AspNetCoreServer.Test
{
    [RequiresPreviewFeatures]
    public class ResponseStreamingPropertyTests
    {
        // -----------------------------------------------------------------------
        // Shared test infrastructure
        // -----------------------------------------------------------------------

        private class PropertyTestStreamingFunction : APIGatewayHttpApiV2ProxyFunction<TestWebApp.Startup>
        {
            public InvokeFeatures CapturedFeatures { get; private set; }
            public MemoryStream CapturedLambdaStream { get; private set; }
            public bool MarshallResponseCalled { get; private set; }

            public PropertyTestStreamingFunction()
                : base(StartupMode.FirstRequest)
            {
                EnableResponseStreaming = true;
            }

            public void PublicMarshallRequest(InvokeFeatures features,
                APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context)
                => MarshallRequest(features, request, context);

            protected override void PostMarshallItemsFeatureFeature(
                IItemsFeature aspNetCoreItemFeature,
                APIGatewayHttpApiV2ProxyRequest lambdaRequest,
                ILambdaContext lambdaContext)
            {
                CapturedFeatures = aspNetCoreItemFeature as InvokeFeatures;
                base.PostMarshallItemsFeatureFeature(aspNetCoreItemFeature, lambdaRequest, lambdaContext);
            }

            [RequiresPreviewFeatures]
            protected override Stream CreateLambdaResponseStream(
                Amazon.Lambda.Core.ResponseStreaming.HttpResponseStreamPrelude prelude)
            {
                var ms = new MemoryStream();
                CapturedLambdaStream = ms;
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

        private class StandalonePreludeBuilder : APIGatewayHttpApiV2ProxyFunction
        {
            public StandalonePreludeBuilder() : base(StartupMode.FirstRequest) { }

            public Amazon.Lambda.Core.ResponseStreaming.HttpResponseStreamPrelude
                InvokeBuildStreamingPrelude(IHttpResponseFeature responseFeature)
                => BuildStreamingPrelude(responseFeature);
        }

        private static APIGatewayHttpApiV2ProxyRequest MakeRequest(
            string method = "GET", string path = "/api/values",
            Dictionary<string, string> headers = null, string body = null)
            => new APIGatewayHttpApiV2ProxyRequest
            {
                RequestContext = new APIGatewayHttpApiV2ProxyRequest.ProxyRequestContext
                {
                    Http = new APIGatewayHttpApiV2ProxyRequest.HttpDescription { Method = method, Path = path },
                    Stage = "$default"
                },
                RawPath = path,
                Headers = headers ?? new Dictionary<string, string> { ["accept"] = "application/json" },
                Body = body
            };


        public static IEnumerable<object[]> RequestMarshallingCases() =>
        [
            ["GET",    "/api/values",   null,                                                    null],
            ["POST",   "/api/values",   new Dictionary<string,string>{["content-type"]="application/json"}, "{\"k\":\"v\"}"],
            ["PUT",    "/api/items/42", new Dictionary<string,string>{["x-custom-header"]="abc"}, null],
            ["DELETE", "/api/items/1",  null,                                                    null],
            ["PATCH",  "/api/values",   new Dictionary<string,string>{["accept"]="text/html"},   null],
        ];

        [Theory]
        [MemberData(nameof(RequestMarshallingCases))]
        public void Property1_RequestMarshalling_IdenticalInStreamingAndBufferedModes(
            string method, string path, Dictionary<string, string> headers, string body)
        {
            var function = new PropertyTestStreamingFunction();
            var context = new TestLambdaContext();

            // Warm up so the host is started
            function.FunctionHandlerAsync(MakeRequest(), context).GetAwaiter().GetResult();

            var request = MakeRequest(method, path, headers, body);
            function.FunctionHandlerAsync(request, context).GetAwaiter().GetResult();
            var streamingReq = (IHttpRequestFeature)function.CapturedFeatures;

            var bufferedFeatures = new InvokeFeatures();
            function.PublicMarshallRequest(bufferedFeatures, request, context);
            var bufferedReq = (IHttpRequestFeature)bufferedFeatures;

            Assert.NotNull(streamingReq);
            Assert.Equal(bufferedReq.Method, streamingReq.Method);
            Assert.Equal(bufferedReq.Path, streamingReq.Path);
            Assert.Equal(bufferedReq.PathBase, streamingReq.PathBase);
            Assert.Equal(bufferedReq.QueryString, streamingReq.QueryString);
            Assert.Equal(bufferedReq.Scheme, streamingReq.Scheme);

            foreach (var key in bufferedReq.Headers.Keys)
            {
                Assert.True(streamingReq.Headers.ContainsKey(key),
                    $"Streaming features missing header '{key}'");
                Assert.Equal(bufferedReq.Headers[key], streamingReq.Headers[key]);
            }
        }


        public static IEnumerable<object[]> BufferedModeCases() =>
        [
            ["GET",    "/api/values",   null,  null],
            ["POST",   "/api/values",   null,  "{\"key\":\"value\"}"],
            ["PUT",    "/api/items/5",  null,  null],
            ["DELETE", "/api/items/5",  null,  null],
            ["GET",    "/api/values",   new Dictionary<string,string>{["accept"]="text/html"}, null],
        ];

        [Theory]
        [MemberData(nameof(BufferedModeCases))]
        public void Property2_BufferedMode_Unaffected(
            string method, string path, Dictionary<string, string> headers, string body)
        {
            // Use a fresh function with streaming OFF
            var function = new PropertyTestStreamingFunction();
            function.EnableResponseStreaming = false;
            var context = new TestLambdaContext();

            var response = function.FunctionHandlerAsync(MakeRequest(method, path, headers, body), context)
                .GetAwaiter().GetResult();

            Assert.NotNull(response);
            Assert.True(function.MarshallResponseCalled, "MarshallResponse must be called in buffered mode");
            Assert.IsType<APIGatewayHttpApiV2ProxyResponse>(response);
            Assert.True(response.StatusCode >= 100 && response.StatusCode <= 599,
                $"Status code {response.StatusCode} out of valid range");
        }


        public static IEnumerable<object[]> PreludeStatusAndHeaderCases() =>
        [
            // (statusCode, headerKey, headerValues[])
            [0,   "accept",          new[] { "application/json" }],
            [200, "content-type",    new[] { "text/plain" }],
            [201, "x-request-id",    new[] { "abc-123" }],
            [404, "cache-control",   new[] { "no-cache", "no-store" }],
            [500, "x-custom-header", new[] { "val1", "val2", "val3" }],
        ];

        [Theory]
        [MemberData(nameof(PreludeStatusAndHeaderCases))]
        public void Property3_Prelude_StatusCodeAndNonCookieHeaders_Correct(
            int statusCode, string headerKey, string[] headerValues)
        {
            var builder = new StandalonePreludeBuilder();
            var features = new InvokeFeatures();
            var rf = (IHttpResponseFeature)features;
            rf.StatusCode = statusCode;
            rf.Headers[headerKey] = new Microsoft.Extensions.Primitives.StringValues(headerValues);

            var prelude = builder.InvokeBuildStreamingPrelude(rf);

            int expectedStatus = statusCode == 0 ? 200 : statusCode;
            Assert.Equal((System.Net.HttpStatusCode)expectedStatus, prelude.StatusCode);

            Assert.True(prelude.MultiValueHeaders.ContainsKey(headerKey),
                $"Header '{headerKey}' missing from MultiValueHeaders");
            Assert.Equal(headerValues, prelude.MultiValueHeaders[headerKey].ToArray());

            Assert.False(prelude.MultiValueHeaders.ContainsKey("Set-Cookie"));
            Assert.False(prelude.MultiValueHeaders.ContainsKey("set-cookie"));
        }


        public static IEnumerable<object[]> SetCookieCases() =>
        [
            [new[] { "session=abc; Path=/" }],
            [new[] { "a=1; Path=/", "b=2; Path=/" }],
            [new[] { "x=foo; Path=/", "y=bar; Path=/", "z=baz; Path=/" }],
        ];

        [Theory]
        [MemberData(nameof(SetCookieCases))]
        public void Property4_SetCookieHeaders_MovedToCookies_AbsentFromMultiValueHeaders(string[] cookies)
        {
            var builder = new StandalonePreludeBuilder();
            var features = new InvokeFeatures();
            var rf = (IHttpResponseFeature)features;
            rf.StatusCode = 200;
            rf.Headers["Set-Cookie"] = new Microsoft.Extensions.Primitives.StringValues(cookies);
            rf.Headers["content-type"] = "application/json";

            var prelude = builder.InvokeBuildStreamingPrelude(rf);

            foreach (var cookie in cookies)
                Assert.Contains(cookie, prelude.Cookies);

            Assert.False(prelude.MultiValueHeaders.ContainsKey("Set-Cookie"),
                "Set-Cookie must not appear in MultiValueHeaders");
            Assert.False(prelude.MultiValueHeaders.ContainsKey("set-cookie"),
                "set-cookie must not appear in MultiValueHeaders");

            Assert.True(prelude.MultiValueHeaders.ContainsKey("content-type"));
        }


        public static IEnumerable<object[]> BodyBytesCases() =>
        [
            [new[] { new byte[] { 1, 2, 3 } }],
            [new[] { new byte[] { 10, 20 }, new byte[] { 30, 40, 50 } }],
            [new[] { new byte[] { 0xFF }, new byte[] { 0x00 }, new byte[] { 0xAB, 0xCD } }],
            [new[] { Encoding.UTF8.GetBytes("hello "), Encoding.UTF8.GetBytes("world") }],
        ];

        [Theory]
        [MemberData(nameof(BodyBytesCases))]
        public async Task Property5_BodyBytes_ForwardedToLambdaResponseStream_InOrder(byte[][] chunks)
        {
            var lambdaStream = new MemoryStream();
            var invokeFeatures = new InvokeFeatures();
            var feature = new StreamingResponseBodyFeature(
                (IHttpResponseFeature)invokeFeatures,
                () => Task.FromResult<Stream>(lambdaStream));

            await feature.StartAsync();

            foreach (var chunk in chunks)
                await feature.Stream.WriteAsync(chunk, 0, chunk.Length);

            lambdaStream.Position = 0;
            var actual = lambdaStream.ToArray();
            var expected = chunks.SelectMany(c => c).ToArray();

            Assert.Equal(expected, actual);
        }


        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(5)]
        public async Task Property6_OnStartingCallbacks_FireBeforeFirstByte(int cbCount)
        {
            int sequenceCounter = 0;
            var callbackSequences = new List<int>();
            int firstWriteSequence = -1;

            var trackingStream = new WriteTrackingStream(() => firstWriteSequence = sequenceCounter++);
            var invokeFeatures = new InvokeFeatures();
            var responseFeature = (IHttpResponseFeature)invokeFeatures;

            for (int i = 0; i < cbCount; i++)
            {
                responseFeature.OnStarting(_ =>
                {
                    callbackSequences.Add(sequenceCounter++);
                    return Task.CompletedTask;
                }, null);
            }

            var feature = new StreamingResponseBodyFeature(
                responseFeature,
                () => Task.FromResult<Stream>(trackingStream));

            await feature.StartAsync();
            var bytes = new byte[] { 1, 2, 3 };
            await feature.Stream.WriteAsync(bytes, 0, bytes.Length);

            Assert.Equal(cbCount, callbackSequences.Count);
            Assert.True(firstWriteSequence >= 0, "No write reached the lambda stream");
            foreach (var seq in callbackSequences)
                Assert.True(seq < firstWriteSequence,
                    $"Callback (seq={seq}) did not fire before first write (seq={firstWriteSequence})");
        }


        public static IEnumerable<object[]> FileRangeCases() =>
        [
            // (fileBytes, offset, count)  — null count means read to end
            [new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, 0L,  (long?)8L],
            [new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, 2L,  (long?)4L],
            [new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, 0L,  (long?)null],
            [new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, 5L,  (long?)null],
            [new byte[] { 0xAA, 0xBB, 0xCC, 0xDD }, 1L,  (long?)2L],
        ];

        [Theory]
        [MemberData(nameof(FileRangeCases))]
        public async Task Property7_SendFileAsync_WritesCorrectByteRange(
            byte[] fileBytes, long offset, long? count)
        {
            var lambdaStream = new MemoryStream();
            var invokeFeatures = new InvokeFeatures();
            var feature = new StreamingResponseBodyFeature(
                (IHttpResponseFeature)invokeFeatures,
                () => Task.FromResult<Stream>(lambdaStream));

            var tempFile = Path.GetTempFileName();
            try
            {
                await File.WriteAllBytesAsync(tempFile, fileBytes);
                await feature.SendFileAsync(tempFile, offset, count);

                lambdaStream.Position = 0;
                var actual = lambdaStream.ToArray();

                long actualCount = count ?? (fileBytes.Length - offset);
                var expected = fileBytes.Skip((int)offset).Take((int)actualCount).ToArray();

                Assert.Equal(expected, actual);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }


        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(5)]
        public void Property8_OnCompletedCallbacks_FireAfterStreamClose(int cbCount)
        {
            int sequenceCounter = 0;
            var completedSequences = new List<int>();
            int streamClosedSequence = -1;

            var function = new OnCompletedTrackingFunction(
                cbCount: cbCount,
                completedSequences: completedSequences,
                getAndIncrementCounter: () => sequenceCounter++,
                onStreamClosed: () => streamClosedSequence = sequenceCounter++);

            var context = new TestLambdaContext();
            var request = MakeRequest();

            function.FunctionHandlerAsync(request, context).GetAwaiter().GetResult();

            Assert.Equal(cbCount, completedSequences.Count);
            Assert.True(streamClosedSequence >= 0, "Stream was never closed");
            foreach (var seq in completedSequences)
                Assert.True(seq > streamClosedSequence,
                    $"OnCompleted callback (seq={seq}) fired before stream closed (seq={streamClosedSequence})");
        }

        // -----------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------

        private class WriteTrackingStream : MemoryStream
        {
            private readonly Action _onFirstWrite;
            private bool _fired;

            public WriteTrackingStream(Action onFirstWrite) => _onFirstWrite = onFirstWrite;

            public override void Write(byte[] buffer, int offset, int count)
            {
                FireOnce();
                base.Write(buffer, offset, count);
            }

            public override Task WriteAsync(byte[] buffer, int offset, int count,
                CancellationToken cancellationToken)
            {
                FireOnce();
                return base.WriteAsync(buffer, offset, count, cancellationToken);
            }

            private void FireOnce()
            {
                if (!_fired) { _fired = true; _onFirstWrite?.Invoke(); }
            }
        }

        private class OnCompletedTrackingFunction : APIGatewayHttpApiV2ProxyFunction<TestWebApp.Startup>
        {
            private readonly int _cbCount;
            private readonly List<int> _completedSequences;
            private readonly Func<int> _getAndIncrementCounter;
            private readonly Action _onStreamClosed;

            public OnCompletedTrackingFunction(
                int cbCount,
                List<int> completedSequences,
                Func<int> getAndIncrementCounter,
                Action onStreamClosed)
                : base(StartupMode.FirstRequest)
            {
                EnableResponseStreaming = true;
                _cbCount = cbCount;
                _completedSequences = completedSequences;
                _getAndIncrementCounter = getAndIncrementCounter;
                _onStreamClosed = onStreamClosed;
            }

            protected override void PostMarshallItemsFeatureFeature(
                IItemsFeature aspNetCoreItemFeature,
                APIGatewayHttpApiV2ProxyRequest lambdaRequest,
                ILambdaContext lambdaContext)
            {
                var responseFeature = (IHttpResponseFeature)aspNetCoreItemFeature;
                for (int i = 0; i < _cbCount; i++)
                {
                    responseFeature.OnCompleted(_ =>
                    {
                        _completedSequences.Add(_getAndIncrementCounter());
                        return Task.CompletedTask;
                    }, null);
                }
                base.PostMarshallItemsFeatureFeature(aspNetCoreItemFeature, lambdaRequest, lambdaContext);
            }

            [RequiresPreviewFeatures]
            protected override Stream CreateLambdaResponseStream(
                Amazon.Lambda.Core.ResponseStreaming.HttpResponseStreamPrelude prelude)
            {
                return new CloseTrackingStream(_onStreamClosed);
            }
        }

        private class CloseTrackingStream : MemoryStream
        {
            private readonly Action _onClose;
            private bool _closed;

            public CloseTrackingStream(Action onClose) => _onClose = onClose;

            protected override void Dispose(bool disposing)
            {
                if (!_closed) { _closed = true; _onClose?.Invoke(); }
                base.Dispose(disposing);
            }

            public override ValueTask DisposeAsync()
            {
                if (!_closed) { _closed = true; _onClose?.Invoke(); }
                return base.DisposeAsync();
            }
        }
    }
}
