// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0
#if NET8_0_OR_GREATER
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

using CsCheck;

using Microsoft.AspNetCore.Http.Features;

using Xunit;

namespace Amazon.Lambda.AspNetCoreServer.Test
{
    /// <summary>
    /// Property-based tests for the ASP.NET Core response streaming feature.
    /// Each property runs a minimum of 100 iterations using CsCheck.
    /// </summary>
    [RequiresPreviewFeatures]
    public class ResponseStreamingPropertyTests
    {
        // -----------------------------------------------------------------------
        // Shared test infrastructure — mirrors TestableStreamingFunction from
        // StreamingFunctionHandlerAsyncTests.cs
        // -----------------------------------------------------------------------

        private class PropertyTestStreamingFunction : APIGatewayHttpApiV2ProxyFunction<TestWebApp.Startup>
        {
            public InvokeFeatures CapturedFeatures { get; private set; }
            public MemoryStream CapturedLambdaStream { get; private set; }
            public bool MarshallResponseCalled { get; private set; }

            public PropertyTestStreamingFunction()
                : base(StartupMode.FirstRequest) { }

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

        // Exposes BuildStreamingPrelude without starting a host
        private class StandalonePreludeBuilder : APIGatewayHttpApiV2ProxyFunction
        {
            public StandalonePreludeBuilder() : base(StartupMode.FirstRequest) { }

            public Amazon.Lambda.Core.ResponseStreaming.HttpResponseStreamPrelude
                InvokeBuildStreamingPrelude(IHttpResponseFeature responseFeature)
                => BuildStreamingPrelude(responseFeature);
        }

        // -----------------------------------------------------------------------
        // Generators
        // -----------------------------------------------------------------------

        // HTTP methods used in generated requests
        private static readonly string[] HttpMethods = { "GET", "POST", "PUT", "DELETE", "PATCH", "HEAD", "OPTIONS" };

        // Header names that are safe to use in generated requests (no Set-Cookie)
        private static readonly string[] SafeHeaderNames =
        {
            "accept", "content-type", "x-custom-header", "x-request-id",
            "authorization", "cache-control", "x-forwarded-for", "user-agent"
        };

        /// <summary>Generator for a valid HTTP method string.</summary>
        private static readonly Gen<string> GenHttpMethod =
            Gen.Int[0, HttpMethods.Length - 1].Select(i => HttpMethods[i]);

        /// <summary>Generator for a simple URL path segment (no special chars).</summary>
        private static readonly Gen<string> GenPathSegment =
            Gen.String[Gen.Char['a', 'z'], 1, 12];

        /// <summary>Generator for a URL path like /seg1/seg2.</summary>
        private static readonly Gen<string> GenPath =
            Gen.Int[1, 3].SelectMany(depth =>
                Gen.Array(Gen.String[Gen.Char['a', 'z'], 1, 8], depth, depth)
                   .Select(segs => "/" + string.Join("/", segs)));

        /// <summary>Generator for a simple ASCII string body (may be empty).</summary>
        private static readonly Gen<string> GenBody =
            Gen.String[Gen.Char['a', 'z'], 0, 64].Select(s => s.Length == 0 ? null : s);

        /// <summary>Generator for a single header value (printable ASCII, no control chars).</summary>
        private static readonly Gen<string> GenHeaderValue =
            Gen.String[Gen.Char[' ', '~'], 1, 32]
               .Where(s => !s.Contains('\r') && !s.Contains('\n') && !s.Contains(':'));

        /// <summary>Generator for a small dictionary of safe (non-Set-Cookie) request headers.</summary>
        private static readonly Gen<Dictionary<string, string>> GenSafeHeaders =
            Gen.Int[0, 3].SelectMany(count =>
            {
                if (count == 0) return Gen.Const(new Dictionary<string, string>());
                return Gen.Array(
                    Gen.Int[0, SafeHeaderNames.Length - 1].SelectMany(i =>
                        GenHeaderValue.Select(v => (SafeHeaderNames[i], v))),
                    count, count)
                .Select(pairs =>
                {
                    var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var (k, v) in pairs) d[k] = v;
                    return d;
                });
            });

        /// <summary>Generator for a complete APIGatewayHttpApiV2ProxyRequest.</summary>
        private static readonly Gen<APIGatewayHttpApiV2ProxyRequest> GenRequest =
            GenHttpMethod.SelectMany(method =>
            GenPath.SelectMany(path =>
            GenSafeHeaders.SelectMany(headers =>
            GenBody.Select(body =>
                new APIGatewayHttpApiV2ProxyRequest
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
                    Headers = headers.Count > 0 ? headers : new Dictionary<string, string> { ["accept"] = "application/json" },
                    Body = body
                }))));

        // -----------------------------------------------------------------------
        // Property 1: Request marshalling identical in streaming and buffered modes
        // Feature: aspnetcore-response-streaming, Property 1: Request marshalling is identical in streaming and buffered modes
        // Validates: Requirements 1.2
        // -----------------------------------------------------------------------

        [Fact]
        public void Property1_RequestMarshalling_IdenticalInStreamingAndBufferedModes()
        {
            // Feature: aspnetcore-response-streaming, Property 1: Request marshalling is identical in streaming and buffered modes
            var function = new PropertyTestStreamingFunction();
            var context = new TestLambdaContext();

            // Warm up the host with a single streaming call so it is started
            var warmupRequest = new APIGatewayHttpApiV2ProxyRequest
            {
                RequestContext = new APIGatewayHttpApiV2ProxyRequest.ProxyRequestContext
                {
                    Http = new APIGatewayHttpApiV2ProxyRequest.HttpDescription { Method = "GET", Path = "/api/values" },
                    Stage = "$default"
                },
                RawPath = "/api/values",
                Headers = new Dictionary<string, string> { ["accept"] = "application/json" }
            };
            function.StreamingFunctionHandlerAsync(warmupRequest, context).GetAwaiter().GetResult();

            GenRequest.Sample(request =>
            {
                // Streaming path — captures features via PostMarshallItemsFeatureFeature
                function.StreamingFunctionHandlerAsync(request, context).GetAwaiter().GetResult();
                var streamingReq = (IHttpRequestFeature)function.CapturedFeatures;

                // Buffered path — call MarshallRequest directly (host already started)
                var bufferedFeatures = new InvokeFeatures();
                function.PublicMarshallRequest(bufferedFeatures, request, context);
                var bufferedReq = (IHttpRequestFeature)bufferedFeatures;

                Assert.NotNull(streamingReq);
                Assert.Equal(bufferedReq.Method, streamingReq.Method);
                Assert.Equal(bufferedReq.Path, streamingReq.Path);
                Assert.Equal(bufferedReq.PathBase, streamingReq.PathBase);
                Assert.Equal(bufferedReq.QueryString, streamingReq.QueryString);
                Assert.Equal(bufferedReq.Scheme, streamingReq.Scheme);

                // All headers present in buffered mode must also be present in streaming mode
                foreach (var key in bufferedReq.Headers.Keys)
                {
                    Assert.True(streamingReq.Headers.ContainsKey(key),
                        $"Streaming features missing header '{key}'");
                    Assert.Equal(bufferedReq.Headers[key], streamingReq.Headers[key]);
                }
            }, iter: 100);
        }

        // -----------------------------------------------------------------------
        // Property 2: Buffered mode unaffected
        // Feature: aspnetcore-response-streaming, Property 2: Buffered mode is unaffected
        // Validates: Requirements 1.4, 8.1, 8.3
        // -----------------------------------------------------------------------

        [Fact]
        public void Property2_BufferedMode_Unaffected()
        {
            // Feature: aspnetcore-response-streaming, Property 2: Buffered mode is unaffected
            var function = new PropertyTestStreamingFunction();
            var context = new TestLambdaContext();

            GenRequest.Sample(request =>
            {
                function.MarshallResponseCalled = false; // reset via reflection not needed — field is set per call

                var response = function.FunctionHandlerAsync(request, context).GetAwaiter().GetResult();

                Assert.NotNull(response);
                Assert.True(function.MarshallResponseCalled,
                    "MarshallResponse must be called in buffered mode");
                Assert.IsType<APIGatewayHttpApiV2ProxyResponse>(response);
                Assert.True(response.StatusCode >= 100 && response.StatusCode <= 599,
                    $"Status code {response.StatusCode} out of valid range");
            }, iter: 100);
        }

        // -----------------------------------------------------------------------
        // Property 3: Prelude status code and non-cookie headers correct
        // Feature: aspnetcore-response-streaming, Property 3: Prelude contains correct status code and all non-cookie headers
        // Validates: Requirements 2.1, 2.2
        // -----------------------------------------------------------------------

        // Generator for status codes 100–599 plus 0 (which should default to 200)
        private static readonly Gen<int> GenStatusCode =
            Gen.Frequency(
                (1, Gen.Const(0)),                // 0 → should default to 200
                (9, Gen.Int[100, 599]));           // valid HTTP status codes

        // Generator for a header name (safe, no Set-Cookie)
        private static readonly Gen<string> GenNonCookieHeaderName =
            Gen.Int[0, SafeHeaderNames.Length - 1].Select(i => SafeHeaderNames[i]);

        // Generator for a list of header values (1–3 values)
        private static readonly Gen<string[]> GenHeaderValues =
            Gen.Int[1, 3].SelectMany(count =>
                Gen.Array(GenHeaderValue, count, count));

        // Generator for a non-Set-Cookie header dictionary
        private static readonly Gen<Dictionary<string, string[]>> GenNonCookieHeaders =
            Gen.Int[0, 4].SelectMany(count =>
            {
                if (count == 0) return Gen.Const(new Dictionary<string, string[]>());
                return Gen.Array(
                    GenNonCookieHeaderName.SelectMany(name =>
                        GenHeaderValues.Select(vals => (name, vals))),
                    count, count)
                .Select(pairs =>
                {
                    var d = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
                    foreach (var (k, v) in pairs) d[k] = v;
                    return d;
                });
            });

        [Fact]
        public void Property3_Prelude_StatusCodeAndNonCookieHeaders_Correct()
        {
            // Feature: aspnetcore-response-streaming, Property 3: Prelude contains correct status code and all non-cookie headers
            var builder = new StandalonePreludeBuilder();

            GenStatusCode.SelectMany(sc => GenNonCookieHeaders.Select(hdrs => (sc, hdrs)))
                .Sample(((int statusCode, Dictionary<string, string[]> headers) input) =>
                {
                    var (statusCode, headers) = input;

                    var features = new InvokeFeatures();
                    var rf = (IHttpResponseFeature)features;
                    rf.StatusCode = statusCode;
                    foreach (var kvp in headers)
                        rf.Headers[kvp.Key] = new Microsoft.Extensions.Primitives.StringValues(kvp.Value);

                    var prelude = builder.InvokeBuildStreamingPrelude(rf);

                    // Status code: 0 → 200, otherwise exact match
                    int expectedStatus = statusCode == 0 ? 200 : statusCode;
                    Assert.Equal((System.Net.HttpStatusCode)expectedStatus, prelude.StatusCode);

                    // All non-Set-Cookie headers must appear in MultiValueHeaders with values preserved
                    foreach (var kvp in headers)
                    {
                        Assert.True(prelude.MultiValueHeaders.ContainsKey(kvp.Key),
                            $"Header '{kvp.Key}' missing from MultiValueHeaders");
                        Assert.Equal(kvp.Value, prelude.MultiValueHeaders[kvp.Key].ToArray());
                    }

                    // No Set-Cookie in MultiValueHeaders
                    Assert.False(prelude.MultiValueHeaders.ContainsKey("Set-Cookie"));
                    Assert.False(prelude.MultiValueHeaders.ContainsKey("set-cookie"));
                }, iter: 100);
        }

        // -----------------------------------------------------------------------
        // Property 4: Set-Cookie headers moved to Cookies
        // Feature: aspnetcore-response-streaming, Property 4: Set-Cookie headers moved to Cookies
        // Validates: Requirements 2.3
        // -----------------------------------------------------------------------

        // Generator for a single Set-Cookie value like "name=value; Path=/"
        private static readonly Gen<string> GenCookieValue =
            Gen.String[Gen.Char['a', 'z'], 1, 8].SelectMany(name =>
            Gen.String[Gen.Char['a', 'z'], 1, 8].Select(value =>
                $"{name}={value}; Path=/"));

        // Generator for a list of 1–5 Set-Cookie values
        private static readonly Gen<List<string>> GenCookieValues =
            Gen.Int[1, 5].SelectMany(count =>
                Gen.List(GenCookieValue, count, count));

        [Fact]
        public void Property4_SetCookieHeaders_MovedToCookies_AbsentFromMultiValueHeaders()
        {
            // Feature: aspnetcore-response-streaming, Property 4: Set-Cookie headers moved to Cookies
            var builder = new StandalonePreludeBuilder();

            GenCookieValues.SelectMany(cookies =>
                GenNonCookieHeaders.Select(otherHeaders => (cookies, otherHeaders)))
            .Sample(((List<string> cookies, Dictionary<string, string[]> otherHeaders) input) =>
            {
                var (cookies, otherHeaders) = input;

                var features = new InvokeFeatures();
                var rf = (IHttpResponseFeature)features;
                rf.StatusCode = 200;
                rf.Headers["Set-Cookie"] = new Microsoft.Extensions.Primitives.StringValues(cookies.ToArray());
                foreach (var kvp in otherHeaders)
                    rf.Headers[kvp.Key] = new Microsoft.Extensions.Primitives.StringValues(kvp.Value);

                var prelude = builder.InvokeBuildStreamingPrelude(rf);

                // All Set-Cookie values must be in Cookies
                foreach (var cookie in cookies)
                    Assert.Contains(cookie, prelude.Cookies);

                // Set-Cookie must NOT appear in MultiValueHeaders
                Assert.False(prelude.MultiValueHeaders.ContainsKey("Set-Cookie"),
                    "Set-Cookie must not appear in MultiValueHeaders");
                Assert.False(prelude.MultiValueHeaders.ContainsKey("set-cookie"),
                    "set-cookie must not appear in MultiValueHeaders");

                // Other headers must still be present
                foreach (var kvp in otherHeaders)
                    Assert.True(prelude.MultiValueHeaders.ContainsKey(kvp.Key),
                        $"Non-cookie header '{kvp.Key}' missing from MultiValueHeaders");
            }, iter: 100);
        }

        // -----------------------------------------------------------------------
        // Property 5: Body bytes forwarded to LambdaResponseStream
        // Feature: aspnetcore-response-streaming, Property 5: Body bytes forwarded to LambdaResponseStream
        // Validates: Requirements 3.2, 4.1
        // -----------------------------------------------------------------------

        // Generator for a single non-empty byte array
        private static readonly Gen<byte[]> GenByteArray =
            Gen.Int[1, 64].SelectMany(len =>
                Gen.Array(Gen.Byte, len, len));

        // Generator for a sequence of 1–5 byte arrays
        private static readonly Gen<List<byte[]>> GenByteArraySequence =
            Gen.Int[1, 5].SelectMany(count =>
                Gen.List(GenByteArray, count, count));

        [Fact]
        public void Property5_BodyBytes_ForwardedToLambdaResponseStream_InOrder()
        {
            // Feature: aspnetcore-response-streaming, Property 5: Body bytes forwarded to LambdaResponseStream
            GenByteArraySequence.Sample(async chunks =>
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
            }, iter: 100);
        }

        // -----------------------------------------------------------------------
        // Property 6: OnStarting callbacks fire before first byte
        // Feature: aspnetcore-response-streaming, Property 6: OnStarting callbacks fire before first byte
        // Validates: Requirements 4.3
        // -----------------------------------------------------------------------

        // Generator for a list of 1–5 callback labels (just ints for ordering)
        private static readonly Gen<int> GenCallbackCount =
            Gen.Int[1, 5];

        [Fact]
        public void Property6_OnStartingCallbacks_FireBeforeFirstByte()
        {
            // Feature: aspnetcore-response-streaming, Property 6: OnStarting callbacks fire before first byte
            GenCallbackCount.SelectMany(cbCount => GenByteArray.Select(bytes => (cbCount, bytes)))
            .Sample(async ((int cbCount, byte[] bytes) input) =>
            {
                var (cbCount, bytes) = input;

                int sequenceCounter = 0;
                var callbackSequences = new List<int>();
                int firstWriteSequence = -1;

                var trackingStream = new WriteTrackingStream(() => firstWriteSequence = sequenceCounter++);
                var invokeFeatures = new InvokeFeatures();
                var responseFeature = (IHttpResponseFeature)invokeFeatures;

                // Register N OnStarting callbacks, each recording their sequence number
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
                await feature.Stream.WriteAsync(bytes, 0, bytes.Length);

                // All callbacks must have fired
                Assert.Equal(cbCount, callbackSequences.Count);

                // All callback sequence numbers must be less than the first write sequence
                Assert.True(firstWriteSequence >= 0, "No write reached the lambda stream");
                foreach (var seq in callbackSequences)
                    Assert.True(seq < firstWriteSequence,
                        $"Callback (seq={seq}) did not fire before first write (seq={firstWriteSequence})");
            }, iter: 100);
        }

        // -----------------------------------------------------------------------
        // Property 7: SendFileAsync writes correct file byte range
        // Feature: aspnetcore-response-streaming, Property 7: SendFileAsync writes file contents
        // Validates: Requirements 4.5
        // -----------------------------------------------------------------------

        // Generator for file content + offset + count
        private static readonly Gen<(byte[] fileBytes, long offset, long? count)> GenFileRange =
            Gen.Int[4, 64].SelectMany(fileLen =>
            Gen.Array(Gen.Byte, fileLen, fileLen).SelectMany(fileBytes =>
            Gen.Long[0, fileLen - 1].SelectMany(offset =>
            Gen.Frequency(
                (1, Gen.Const<long?>(null)),                                    // read to end
                (2, Gen.Long[0, fileLen - offset].Select(c => (long?)c)))       // specific count
            .Select(count => (fileBytes, offset, count)))));

        [Fact]
        public void Property7_SendFileAsync_WritesCorrectByteRange()
        {
            // Feature: aspnetcore-response-streaming, Property 7: SendFileAsync writes file contents
            GenFileRange.Sample(async ((byte[] fileBytes, long offset, long? count) input) =>
            {
                var (fileBytes, offset, count) = input;

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

                    // Compute expected slice
                    long actualCount = count ?? (fileBytes.Length - offset);
                    var expected = fileBytes.Skip((int)offset).Take((int)actualCount).ToArray();

                    Assert.Equal(expected, actual);
                }
                finally
                {
                    File.Delete(tempFile);
                }
            }, iter: 100);
        }

        // -----------------------------------------------------------------------
        // Property 8: OnCompleted callbacks fire after stream close
        // Feature: aspnetcore-response-streaming, Property 8: OnCompleted callbacks fire after stream close
        // Validates: Requirements 5.4
        // -----------------------------------------------------------------------

        [Fact]
        public void Property8_OnCompletedCallbacks_FireAfterStreamClose()
        {
            // Feature: aspnetcore-response-streaming, Property 8: OnCompleted callbacks fire after stream close
            GenCallbackCount.Sample(cbCount =>
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
                var request = new APIGatewayHttpApiV2ProxyRequest
                {
                    RequestContext = new APIGatewayHttpApiV2ProxyRequest.ProxyRequestContext
                    {
                        Http = new APIGatewayHttpApiV2ProxyRequest.HttpDescription
                        {
                            Method = "GET",
                            Path = "/api/values"
                        },
                        Stage = "$default"
                    },
                    RawPath = "/api/values",
                    Headers = new Dictionary<string, string> { ["accept"] = "application/json" }
                };

                function.StreamingFunctionHandlerAsync(request, context).GetAwaiter().GetResult();

                Assert.Equal(cbCount, completedSequences.Count);
                Assert.True(streamClosedSequence >= 0, "Stream was never closed");
                foreach (var seq in completedSequences)
                    Assert.True(seq > streamClosedSequence,
                        $"OnCompleted callback (seq={seq}) fired before stream closed (seq={streamClosedSequence})");
            }, iter: 100);
        }

        // -----------------------------------------------------------------------
        // Helper: stream that fires a callback on the first write
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

        // -----------------------------------------------------------------------
        // Helper: function that tracks OnCompleted ordering vs stream close
        // -----------------------------------------------------------------------

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
                // Return a stream that fires _onStreamClosed when disposed
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
#endif
