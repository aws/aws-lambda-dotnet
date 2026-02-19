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
            private readonly LambdaResponseStream _responseStream;

            public MockHttpMessageHandler(LambdaResponseStream responseStream)
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
            LambdaResponseStream stream, out MockHttpMessageHandler handler)
        {
            handler = new MockHttpMessageHandler(stream);
            var httpClient = new HttpClient(handler);
            var envVars = new TestEnvironmentVariables();
            envVars.SetEnvironmentVariable("AWS_LAMBDA_RUNTIME_API", "localhost:9001");
            return new RuntimeApiClient(envVars, httpClient);
        }

        // --- Property 7: Streaming Response Mode Header ---

        /// <summary>
        /// Property 7: Streaming Response Mode Header
        /// For any streaming response, the HTTP request should include
        /// "Lambda-Runtime-Function-Response-Mode: streaming".
        /// **Validates: Requirements 4.1**
        /// </summary>
        [Fact]
        public async Task StartStreamingResponseAsync_IncludesStreamingResponseModeHeader()
        {
            var stream = new LambdaResponseStream();
            var client = CreateClientWithMockHandler(stream, out var handler);

            await client.StartStreamingResponseAsync("req-1", stream, CancellationToken.None);

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
            var stream = new LambdaResponseStream();
            var client = CreateClientWithMockHandler(stream, out var handler);

            await client.StartStreamingResponseAsync("req-2", stream, CancellationToken.None);

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
            var stream = new LambdaResponseStream();
            var client = CreateClientWithMockHandler(stream, out var handler);

            await client.StartStreamingResponseAsync("req-3", stream, CancellationToken.None);

            Assert.NotNull(handler.CapturedRequest);
            Assert.True(handler.CapturedRequest.Headers.Contains("Trailer"));
            var trailerValue = string.Join(", ", handler.CapturedRequest.Headers.GetValues("Trailer"));
            Assert.Contains(StreamingConstants.ErrorTypeTrailer, trailerValue);
            Assert.Contains(StreamingConstants.ErrorBodyTrailer, trailerValue);
        }

        // --- Property 18: Stream Finalization ---

        /// <summary>
        /// Property 18: Stream Finalization
        /// For any streaming response that completes successfully, the ResponseStream
        /// should be marked as completed (IsCompleted = true) after the HTTP response succeeds.
        /// **Validates: Requirements 8.3**
        /// </summary>
        [Fact]
        public async Task StartStreamingResponseAsync_MarksStreamCompletedAfterSuccess()
        {
            var stream = new LambdaResponseStream();
            var client = CreateClientWithMockHandler(stream, out _);

            await client.StartStreamingResponseAsync("req-4", stream, CancellationToken.None);

            Assert.True(stream.IsCompleted);
        }

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

        [Fact]
        public async Task StartStreamingResponseAsync_NullRequestId_ThrowsArgumentNullException()
        {
            var stream = new LambdaResponseStream();
            var client = CreateClientWithMockHandler(stream, out _);

            await Assert.ThrowsAsync<ArgumentNullException>(
                () => client.StartStreamingResponseAsync(null, stream, CancellationToken.None));
        }

        [Fact]
        public async Task StartStreamingResponseAsync_NullResponseStream_ThrowsArgumentNullException()
        {
            var stream = new LambdaResponseStream();
            var client = CreateClientWithMockHandler(stream, out _);

            await Assert.ThrowsAsync<ArgumentNullException>(
                () => client.StartStreamingResponseAsync("req-5", null, CancellationToken.None));
        }
    }
}
