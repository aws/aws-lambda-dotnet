/*
 * Copyright 2019 Amazon.com, Inc. or its affiliates. All Rights Reserved.
 *
 * Licensed under the Apache License, Version 2.0 (the "License").
 * You may not use this file except in compliance with the License.
 * A copy of the License is located at
 *
 *  http://aws.amazon.com/apache2.0
 *
 * or in the "license" file accompanying this file. This file is distributed
 * on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either
 * express or implied. See the License for the specific language governing
 * permissions and limitations under the License.
 */

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Lambda.RuntimeSupport.Client.ResponseStreaming;
using Xunit;

namespace Amazon.Lambda.RuntimeSupport.UnitTests
{
    /// <summary>
    /// Tests for RuntimeApiClient streaming and buffered behavior.
    /// Validates Properties 7, 8, 10, 13, 18.
    /// </summary>
    public class RuntimeApiClientTests
    {
        private const long MaxResponseSize = 20 * 1024 * 1024;

        /// <summary>
        /// Mock HttpMessageHandler that captures the request for header inspection.
        /// It completes the ResponseStream and returns immediately without reading
        /// the content body, avoiding the SerializeToStreamAsync blocking issue.
        /// </summary>
        private class MockHttpMessageHandler : HttpMessageHandler
        {
            public HttpRequestMessage CapturedRequest { get; private set; }
            private readonly ResponseStream _responseStream;

            public MockHttpMessageHandler(ResponseStream responseStream)
            {
                _responseStream = responseStream;
            }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                CapturedRequest = request;

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            }
        }

        private static RuntimeApiClient CreateClientWithMockHandler(
            ResponseStream stream, out MockHttpMessageHandler handler)
        {
            handler = new MockHttpMessageHandler(stream);
            var httpClient = new HttpClient(handler);
            var envVars = new TestEnvironmentVariables();
            envVars.SetEnvironmentVariable("AWS_LAMBDA_RUNTIME_API", "localhost:9001");
            return new RuntimeApiClient(envVars, httpClient);
        }

        // --- Property 7: Streaming Response Mode Header ---
        // Note: Properties 7, 8, 13 test the HttpClient-based streaming path which is only used on pre-NET8 targets.
        // On NET8+, StartStreamingResponseAsync uses RawStreamingHttpClient (raw TCP) which doesn't go through HttpClient.

#if !NET8_0_OR_GREATER
        /// <summary>
        /// Property 7: Streaming Response Mode Header
        /// For any streaming response, the HTTP request should include
        /// "Lambda-Runtime-Function-Response-Mode: streaming".
        /// **Validates: Requirements 4.1**
        /// </summary>
        [Fact]
        public async Task StartStreamingResponseAsync_IncludesStreamingResponseModeHeader()
        {
            var stream = new ResponseStream(Array.Empty<byte>());
            var client = CreateClientWithMockHandler(stream, out var handler);

            await client.StartStreamingResponseAsync("req-1", null, stream, CancellationToken.None);

            Assert.NotNull(handler.CapturedRequest);
            Assert.True(handler.CapturedRequest.Headers.Contains(StreamingConstants.ResponseModeHeader));
            var values = handler.CapturedRequest.Headers.GetValues(StreamingConstants.ResponseModeHeader).ToList();
            Assert.Single(values);
            Assert.Equal(StreamingConstants.StreamingResponseMode, values[0]);
        }

        // --- Property 8: Chunked Transfer Encoding Header ---

        /// <summary>
        /// Property 8: Chunked Transfer Encoding Header
        /// For any streaming response, the HTTP request should include
        /// "Transfer-Encoding: chunked".
        /// **Validates: Requirements 4.2**
        /// </summary>
        [Fact]
        public async Task StartStreamingResponseAsync_IncludesChunkedTransferEncodingHeader()
        {
            var stream = new ResponseStream(Array.Empty<byte>());
            var client = CreateClientWithMockHandler(stream, out var handler);

            await client.StartStreamingResponseAsync("req-2", null, stream, CancellationToken.None);

            Assert.NotNull(handler.CapturedRequest);
            Assert.True(handler.CapturedRequest.Headers.TransferEncodingChunked);
        }

        // --- Property 13: Trailer Declaration Header ---

        /// <summary>
        /// Property 13: Trailer Declaration Header
        /// For any streaming response, the HTTP request should include a "Trailer" header
        /// declaring the error trailer headers upfront (since we cannot know at request
        /// start whether an error will occur).
        /// **Validates: Requirements 5.4**
        /// </summary>
        [Fact]
        public async Task StartStreamingResponseAsync_DeclaresTrailerHeaderUpfront()
        {
            var stream = new ResponseStream(Array.Empty<byte>());
            var client = CreateClientWithMockHandler(stream, out var handler);

            await client.StartStreamingResponseAsync("req-3", null, stream, CancellationToken.None);

            Assert.NotNull(handler.CapturedRequest);
            Assert.True(handler.CapturedRequest.Headers.Contains("Trailer"));
            var trailerValue = string.Join(", ", handler.CapturedRequest.Headers.GetValues("Trailer"));
            Assert.Contains(StreamingConstants.ErrorTypeTrailer, trailerValue);
            Assert.Contains(StreamingConstants.ErrorBodyTrailer, trailerValue);
        }
#endif

        // --- Property 10: Buffered Responses Exclude Streaming Headers ---

        /// <summary>
        /// Mock HttpMessageHandler that captures the request for buffered response header inspection.
        /// Returns an Accepted (202) response since that's what the InternalRuntimeApiClient expects.
        /// </summary>
        private class BufferedMockHttpMessageHandler : HttpMessageHandler
        {
            public HttpRequestMessage CapturedRequest { get; private set; }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                CapturedRequest = request;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted));
            }
        }

        /// <summary>
        /// Property 10: Buffered Responses Exclude Streaming Headers
        /// For any buffered response (where CreateStream was not called), the HTTP request
        /// should not include "Lambda-Runtime-Function-Response-Mode" or
        /// "Transfer-Encoding: chunked" or "Trailer" headers.
        /// **Validates: Requirements 4.6**
        /// </summary>
        [Fact]
        public async Task SendResponseAsync_BufferedResponse_ExcludesStreamingHeaders()
        {
            var bufferedHandler = new BufferedMockHttpMessageHandler();
            var httpClient = new HttpClient(bufferedHandler);
            var envVars = new TestEnvironmentVariables();
            envVars.SetEnvironmentVariable("AWS_LAMBDA_RUNTIME_API", "localhost:9001");
            var client = new RuntimeApiClient(envVars, httpClient);

            var outputStream = new MemoryStream(new byte[] { 1, 2, 3 });
            await client.SendResponseAsync("req-buffered", outputStream, CancellationToken.None);

            Assert.NotNull(bufferedHandler.CapturedRequest);
            // Buffered responses must not include streaming-specific headers
            Assert.False(bufferedHandler.CapturedRequest.Headers.Contains(StreamingConstants.ResponseModeHeader),
                "Buffered response should not include Lambda-Runtime-Function-Response-Mode header");
            Assert.NotEqual(true, bufferedHandler.CapturedRequest.Headers.TransferEncodingChunked);
            Assert.False(bufferedHandler.CapturedRequest.Headers.Contains("Trailer"),
                "Buffered response should not include Trailer header");
        }

        // --- Argument validation ---

        // --- Cross-wiring: Lambda-Runtime-Invocation-Id echo + 410 handling ---

        /// <summary>
        /// Mock handler that captures the request and returns a configurable status code.
        /// </summary>
        private class ConfigurableMockHttpMessageHandler : HttpMessageHandler
        {
            public HttpRequestMessage CapturedRequest { get; private set; }
            private readonly HttpStatusCode _statusCode;
            private readonly string _body;

            public ConfigurableMockHttpMessageHandler(HttpStatusCode statusCode, string body = null)
            {
                _statusCode = statusCode;
                _body = body;
            }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                CapturedRequest = request;
                var response = new HttpResponseMessage(_statusCode);
                if (_body != null)
                {
                    response.Content = new StringContent(_body);
                }
                return Task.FromResult(response);
            }
        }

        private static RuntimeApiClient CreateClientWith(ConfigurableMockHttpMessageHandler handler)
        {
            var httpClient = new HttpClient(handler);
            var envVars = new TestEnvironmentVariables();
            envVars.SetEnvironmentVariable("AWS_LAMBDA_RUNTIME_API", "localhost:9001");
            return new RuntimeApiClient(envVars, httpClient);
        }

        [Fact]
        public async Task SendResponseAsync_WithInvocationId_EchoesHeader()
        {
            var handler = new ConfigurableMockHttpMessageHandler(HttpStatusCode.Accepted);
            var client = CreateClientWith(handler);

            var outputStream = new MemoryStream(new byte[] { 1, 2, 3 });
            await client.SendResponseAsync("req-1", "inv-uuid-abc", outputStream, CancellationToken.None);

            Assert.NotNull(handler.CapturedRequest);
            Assert.True(handler.CapturedRequest.Headers.Contains("Lambda-Runtime-Invocation-Id"));
            Assert.Equal("inv-uuid-abc",
                handler.CapturedRequest.Headers.GetValues("Lambda-Runtime-Invocation-Id").Single());
        }

        [Fact]
        public async Task SendResponseAsync_WithoutInvocationId_OmitsHeader()
        {
            var handler = new ConfigurableMockHttpMessageHandler(HttpStatusCode.Accepted);
            var client = CreateClientWith(handler);

            var outputStream = new MemoryStream(new byte[] { 1, 2, 3 });
            await client.SendResponseAsync("req-1", null, outputStream, CancellationToken.None);

            Assert.NotNull(handler.CapturedRequest);
            Assert.False(handler.CapturedRequest.Headers.Contains("Lambda-Runtime-Invocation-Id"),
                "Response should not include the invocation id header when none was provided.");
        }

        [Fact]
        public async Task SendResponseAsync_410Gone_ThrowsRuntimeApiInvokeTimeoutException()
        {
            var handler = new ConfigurableMockHttpMessageHandler(HttpStatusCode.Gone,
                "{\"errorMessage\":\"Invoke timeout\",\"errorType\":\"InvokeTimeout\"}");
            var client = CreateClientWith(handler);

            var outputStream = new MemoryStream(new byte[] { 1, 2, 3 });
            var ex = await Assert.ThrowsAsync<RuntimeApiInvokeTimeoutException>(
                () => client.SendResponseAsync("req-timeout", "inv-uuid-abc", outputStream, CancellationToken.None));

            Assert.Equal("req-timeout", ex.AwsRequestId);
        }

        [Fact]
        public async Task ReportInvocationErrorAsync_WithInvocationId_EchoesHeader()
        {
            var handler = new ConfigurableMockHttpMessageHandler(HttpStatusCode.Accepted, "{}");
            var client = CreateClientWith(handler);

            await client.ReportInvocationErrorAsync("req-1", "inv-uuid-abc", new Exception("boom"), CancellationToken.None);

            Assert.NotNull(handler.CapturedRequest);
            Assert.True(handler.CapturedRequest.Headers.Contains("Lambda-Runtime-Invocation-Id"));
            Assert.Equal("inv-uuid-abc",
                handler.CapturedRequest.Headers.GetValues("Lambda-Runtime-Invocation-Id").Single());
        }

        [Fact]
        public async Task ReportInvocationErrorAsync_410Gone_ThrowsRuntimeApiInvokeTimeoutException()
        {
            var handler = new ConfigurableMockHttpMessageHandler(HttpStatusCode.Gone,
                "{\"errorMessage\":\"Invoke timeout\",\"errorType\":\"InvokeTimeout\"}");
            var client = CreateClientWith(handler);

            var ex = await Assert.ThrowsAsync<RuntimeApiInvokeTimeoutException>(
                () => client.ReportInvocationErrorAsync("req-timeout", "inv-uuid-abc", new Exception("boom"), CancellationToken.None));

            Assert.Equal("req-timeout", ex.AwsRequestId);
        }

        // --- Request messages sent to the Runtime API ---

        /// <summary>
        /// Mock handler that captures the request and returns a next invocation with the specified headers.
        /// </summary>
        private class NextInvocationMockHttpMessageHandler : HttpMessageHandler
        {
            public HttpRequestMessage CapturedRequest { get; private set; }
            private readonly (string Name, string Value)[] _headers;

            public NextInvocationMockHttpMessageHandler(params (string Name, string Value)[] headers)
            {
                _headers = headers;
            }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                CapturedRequest = request;
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"key\":\"value\"}")
                };

                foreach (var (name, value) in _headers)
                {
                    response.Headers.TryAddWithoutValidation(name, value);
                }

                return Task.FromResult(response);
            }
        }

        [Fact]
        public async Task GetNextInvocationAsync_SendsExpectedRequest()
        {
            var handler = new NextInvocationMockHttpMessageHandler(("Lambda-Runtime-Aws-Request-Id", "req-1"));
            var httpClient = new HttpClient(handler);
            var envVars = new TestEnvironmentVariables();
            envVars.SetEnvironmentVariable("AWS_LAMBDA_RUNTIME_API", "localhost:9001");
            var client = new RuntimeApiClient(envVars, httpClient);

            using var invocation = await client.GetNextInvocationAsync(CancellationToken.None);

            Assert.NotNull(handler.CapturedRequest);
            Assert.Equal(HttpMethod.Get, handler.CapturedRequest.Method);
            Assert.Equal("http://localhost:9001/2018-06-01/runtime/invocation/next", handler.CapturedRequest.RequestUri.ToString());
            Assert.Equal("application/json", handler.CapturedRequest.Headers.GetValues("Accept").Single());
        }

        [Fact]
        public async Task GetNextInvocationAsync_ReadsRuntimeApiHeadersIgnoringCase()
        {
            var handler = new NextInvocationMockHttpMessageHandler(
                ("lambda-runtime-aws-request-id", "req-1"),
                ("LAMBDA-RUNTIME-INVOKED-FUNCTION-ARN", "arn:aws:lambda:us-east-2:123456789012:function:custom-runtime"),
                ("Lambda-Runtime-Trace-Id", "Root=1-5bef4de7-ad49b0e87f6ef6c87fc2e700;Parent=9a9197af755a6419;Sampled=1"),
                ("lambda-runtime-deadline-ms", "1542409706888"),
                ("Lambda-Runtime-Aws-Tenant-Id", "tenant-1"),
                ("lambda-runtime-invocation-id", "inv-uuid-abc"));
            var httpClient = new HttpClient(handler);
            var envVars = new TestEnvironmentVariables();
            envVars.SetEnvironmentVariable("AWS_LAMBDA_RUNTIME_API", "localhost:9001");
            var client = new RuntimeApiClient(envVars, httpClient);

            using var invocation = await client.GetNextInvocationAsync(CancellationToken.None);

            var context = Assert.IsType<LambdaContext>(invocation.LambdaContext);
            Assert.Equal("req-1", context.AwsRequestId);
            Assert.Equal("arn:aws:lambda:us-east-2:123456789012:function:custom-runtime", context.InvokedFunctionArn);
            Assert.Equal("Root=1-5bef4de7-ad49b0e87f6ef6c87fc2e700;Parent=9a9197af755a6419;Sampled=1", context.TraceId);
            Assert.Equal("tenant-1", context.TenantId);
            Assert.Equal("inv-uuid-abc", context.InvocationId);

            using var reader = new StreamReader(invocation.InputStream);
            Assert.Equal("{\"key\":\"value\"}", await reader.ReadToEndAsync());
        }

        [Fact]
        public async Task SendResponseAsync_SendsExpectedRequest()
        {
            var handler = new ConfigurableMockHttpMessageHandler(HttpStatusCode.Accepted);
            var client = CreateClientWith(handler);

            var outputStream = new MemoryStream(new byte[] { 1, 2, 3 });
            await client.SendResponseAsync("req-1", outputStream, CancellationToken.None);

            Assert.NotNull(handler.CapturedRequest);
            Assert.Equal(HttpMethod.Post, handler.CapturedRequest.Method);
            Assert.Equal("http://localhost:9001/2018-06-01/runtime/invocation/req-1/response", handler.CapturedRequest.RequestUri.ToString());
            Assert.Equal("application/json", handler.CapturedRequest.Headers.GetValues("Accept").Single());
            Assert.Equal("application/json", handler.CapturedRequest.Content.Headers.GetValues("Content-Type").Single());
        }

        [Fact]
        public async Task SendResponseAsync_WithoutOutputStream_SendsExpectedRequest()
        {
            var handler = new ConfigurableMockHttpMessageHandler(HttpStatusCode.Accepted);
            var client = CreateClientWith(handler);

            await client.SendResponseAsync("req-1", null, CancellationToken.None);

            Assert.NotNull(handler.CapturedRequest);
            Assert.Equal(HttpMethod.Post, handler.CapturedRequest.Method);
            Assert.Equal("application/json", handler.CapturedRequest.Headers.GetValues("Accept").Single());
            Assert.Equal("application/json", handler.CapturedRequest.Content.Headers.GetValues("Content-Type").Single());
        }

        [Fact]
        public async Task ReportInvocationErrorAsync_SendsExpectedRequest()
        {
            var handler = new ConfigurableMockHttpMessageHandler(HttpStatusCode.Accepted, "{}");
            var client = CreateClientWith(handler);

            await client.ReportInvocationErrorAsync("req-1", new Exception("boom"), CancellationToken.None);

            Assert.NotNull(handler.CapturedRequest);
            Assert.Equal(HttpMethod.Post, handler.CapturedRequest.Method);
            Assert.Equal("http://localhost:9001/2018-06-01/runtime/invocation/req-1/error", handler.CapturedRequest.RequestUri.ToString());
            Assert.Equal("application/json", handler.CapturedRequest.Headers.GetValues("Accept").Single());
            Assert.Equal("application/vnd.aws.lambda.error+json", handler.CapturedRequest.Content.Headers.GetValues("Content-Type").Single());
        }

        [Fact]
        public async Task StartStreamingResponseAsync_NullRequestId_ThrowsArgumentNullException()
        {
            var stream = new ResponseStream(Array.Empty<byte>());
            var client = CreateClientWithMockHandler(stream, out _);

            await Assert.ThrowsAsync<ArgumentNullException>(
                () => client.StartStreamingResponseAsync(null, null, stream, CancellationToken.None));
        }

        [Fact]
        public async Task StartStreamingResponseAsync_NullResponseStream_ThrowsArgumentNullException()
        {
            var stream = new ResponseStream(Array.Empty<byte>());
            var client = CreateClientWithMockHandler(stream, out _);

            await Assert.ThrowsAsync<ArgumentNullException>(
                () => client.StartStreamingResponseAsync("req-5", null, null, CancellationToken.None));
        }
    }
}
