/*
 * Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Lambda.RuntimeSupport.UnitTests.TestHelpers;
using Xunit;

namespace Amazon.Lambda.RuntimeSupport.UnitTests
{
    [CollectionDefinition("ResponseStreamFactory")]
    public class ResponseStreamFactoryCollection { }

    /// <summary>
    /// End-to-end integration tests for the true-streaming architecture.
    /// These tests exercise the full pipeline: LambdaBootstrap → ResponseStreamFactory →
    /// ResponseStream → StreamingHttpContent → captured HTTP output stream.
    /// </summary>
    [Collection("ResponseStreamFactory")]
    public class StreamingE2EWithMoq : IDisposable
    {
        public void Dispose()
        {
            LambdaResponseStreamFactory.CleanupInvocation(isMultiConcurrency: false);
            LambdaResponseStreamFactory.CleanupInvocation(isMultiConcurrency: true);
        }

        // ─── Helpers ────────────────────────────────────────────────────────────────

        private static Dictionary<string, IEnumerable<string>> MakeHeaders(string requestId = "test-request-id")
            => new Dictionary<string, IEnumerable<string>>
            {
                { RuntimeApiHeaders.HeaderAwsRequestId,       new List<string> { requestId } },
                { RuntimeApiHeaders.HeaderInvokedFunctionArn, new List<string> { "arn:aws:lambda:us-east-1:123456789012:function:test" } },
                { RuntimeApiHeaders.HeaderAwsTenantId,        new List<string> { "tenant-id" } },
                { RuntimeApiHeaders.HeaderTraceId,            new List<string> { "trace-id" } },
                { RuntimeApiHeaders.HeaderDeadlineMs,         new List<string> { "9999999999999" } },
            };

        /// <summary>
        /// A capturing RuntimeApiClient that records the raw bytes written to the HTTP output stream
        /// by SerializeToStreamAsync, enabling assertions on chunked-encoding format.
        /// </summary>
        private class CapturingStreamingRuntimeApiClient : RuntimeApiClient, IRuntimeApiClient
        {
            private readonly IEnvironmentVariables _envVars;
            private readonly Dictionary<string, IEnumerable<string>> _headers;

            public bool StartStreamingCalled { get; private set; }
            public bool SendResponseCalled { get; private set; }
            public bool ReportInvocationErrorCalled { get; private set; }
            public byte[] CapturedHttpBytes { get; private set; }
            public LambdaResponseStream LastResponseStream { get; private set; }
            public Stream LastBufferedOutputStream { get; private set; }

            public new Amazon.Lambda.RuntimeSupport.Helpers.IConsoleLoggerWriter ConsoleLogger { get; } = new Helpers.LogLevelLoggerWriter(new SystemEnvironmentVariables());

            public CapturingStreamingRuntimeApiClient(
                IEnvironmentVariables envVars,
                Dictionary<string, IEnumerable<string>> headers)
                : base(envVars, new NoOpInternalRuntimeApiClient())
            {
                _envVars = envVars;
                _headers = headers;
            }

            public new async Task<InvocationRequest> GetNextInvocationAsync(CancellationToken cancellationToken = default)
            {
                _headers[RuntimeApiHeaders.HeaderTraceId] = new List<string> { Guid.NewGuid().ToString() };
                var inputStream = new MemoryStream(new byte[0]);
                return new InvocationRequest
                {
                    InputStream = inputStream,
                    LambdaContext = new LambdaContext(
                        new RuntimeApiHeaders(_headers),
                        new LambdaEnvironment(_envVars),
                        new TestDateTimeHelper(),
                        new Helpers.SimpleLoggerWriter(_envVars))
                };
            }

            internal override async Task StartStreamingResponseAsync(
                string awsRequestId, LambdaResponseStream responseStream, CancellationToken cancellationToken = default)
            {
                StartStreamingCalled = true;
                LastResponseStream = responseStream;

                // Use a real MemoryStream as the HTTP output stream so we capture actual bytes
                var captureStream = new MemoryStream();
                var content = new StreamingHttpContent(responseStream);

                // SerializeToStreamAsync hands the stream to ResponseStream and waits for completion
                await content.CopyToAsync(captureStream);
                CapturedHttpBytes = captureStream.ToArray();
            }

            public new async Task SendResponseAsync(string awsRequestId, Stream outputStream, CancellationToken cancellationToken = default)
            {
                SendResponseCalled = true;
                if (outputStream != null)
                {
                    var ms = new MemoryStream();
                    await outputStream.CopyToAsync(ms);
                    ms.Position = 0;
                    LastBufferedOutputStream = ms;
                }
            }

            public new Task ReportInvocationErrorAsync(string awsRequestId, Exception exception, CancellationToken cancellationToken = default)
            {
                ReportInvocationErrorCalled = true;
                return Task.CompletedTask;
            }

            public new Task ReportInitializationErrorAsync(Exception exception, string errorType = null, CancellationToken cancellationToken = default)
                => Task.CompletedTask;

            public new Task ReportInitializationErrorAsync(string errorType, CancellationToken cancellationToken = default)
                => Task.CompletedTask;

#if NET8_0_OR_GREATER
            public new Task RestoreNextInvocationAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
            public new Task ReportRestoreErrorAsync(Exception exception, string errorType = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
#endif
        }

        private static CapturingStreamingRuntimeApiClient CreateClient(string requestId = "test-request-id")
            => new CapturingStreamingRuntimeApiClient(new TestEnvironmentVariables(), MakeHeaders(requestId));

        // ─── 10.1 End-to-end streaming response ─────────────────────────────────────

        /// <summary>
        /// End-to-end: handler calls CreateStream, writes multiple chunks.
        /// Verifies data flows through with correct chunked encoding and stream is finalized.
        /// Requirements: 3.2, 4.3, 10.1
        /// </summary>
        [Fact]
        public async Task Streaming_MultipleChunks_FlowThroughWithChunkedEncoding()
        {
            var client = CreateClient();
            var chunks = new[] { "Hello", ", ", "World" };

            LambdaBootstrapHandler handler = async (invocation) =>
            {
                var stream = LambdaResponseStreamFactory.CreateStream();
                foreach (var chunk in chunks)
                    await stream.WriteAsync(Encoding.UTF8.GetBytes(chunk));
                return new InvocationResponse(Stream.Null, false);
            };

            using var bootstrap = new LambdaBootstrap(handler, null);
            bootstrap.Client = client;
            await bootstrap.InvokeOnceAsync();

            Assert.True(client.StartStreamingCalled);
            Assert.NotNull(client.CapturedHttpBytes);

            var output = Encoding.UTF8.GetString(client.CapturedHttpBytes);

            // Each chunk should appear as: hex-size\r\ndata\r\n
            Assert.Contains("5\r\nHello\r\n", output);
            Assert.Contains("2\r\n, \r\n", output);
            Assert.Contains("5\r\nWorld\r\n", output);

            // Final chunk terminates the stream
            Assert.Contains("0\r\n", output);
            Assert.EndsWith("0\r\n\r\n", output);
        }

        /// <summary>
        /// End-to-end: all data is transmitted correctly (content round-trip).
        /// Requirements: 3.2, 4.3, 10.1
        /// </summary>
        [Fact]
        public async Task Streaming_AllDataTransmitted_ContentRoundTrip()
        {
            var client = CreateClient();
            var payload = Encoding.UTF8.GetBytes("integration test payload");

            LambdaBootstrapHandler handler = async (invocation) =>
            {
                var stream = LambdaResponseStreamFactory.CreateStream();
                await stream.WriteAsync(payload);
                return new InvocationResponse(Stream.Null, false);
            };

            using var bootstrap = new LambdaBootstrap(handler, null);
            bootstrap.Client = client;
            await bootstrap.InvokeOnceAsync();

            var output = client.CapturedHttpBytes;
            Assert.NotNull(output);

            // Decode the single chunk: hex-size\r\ndata\r\n
            var outputStr = Encoding.UTF8.GetString(output);
            var hexSize = payload.Length.ToString("X");
            Assert.Contains(hexSize + "\r\n", outputStr);
            Assert.Contains("integration test payload", outputStr);
        }

        /// <summary>
        /// End-to-end: stream is finalized (final chunk written, BytesWritten matches).
        /// Requirements: 3.2, 4.3, 10.1
        /// </summary>
        [Fact]
        public async Task Streaming_StreamFinalized_BytesWrittenMatchesPayload()
        {
            var client = CreateClient();
            var data = Encoding.UTF8.GetBytes("finalization check");

            LambdaBootstrapHandler handler = async (invocation) =>
            {
                var stream = LambdaResponseStreamFactory.CreateStream();
                await stream.WriteAsync(data);
                return new InvocationResponse(Stream.Null, false);
            };

            using var bootstrap = new LambdaBootstrap(handler, null);
            bootstrap.Client = client;
            await bootstrap.InvokeOnceAsync();

            Assert.NotNull(client.LastResponseStream);
            Assert.Equal(data.Length, client.LastResponseStream.BytesWritten);
        }

        // ─── 10.2 End-to-end buffered response ──────────────────────────────────────

        /// <summary>
        /// End-to-end: handler does NOT call CreateStream — response goes via buffered path.
        /// Verifies SendResponseAsync is called and streaming headers are absent.
        /// Requirements: 1.5, 4.6, 9.4
        /// </summary>
        [Fact]
        public async Task Buffered_HandlerDoesNotCallCreateStream_UsesSendResponsePath()
        {
            var client = CreateClient();
            var responseBody = Encoding.UTF8.GetBytes("buffered response body");

            LambdaBootstrapHandler handler = async (invocation) =>
            {
                await Task.Yield();
                return new InvocationResponse(new MemoryStream(responseBody));
            };

            using var bootstrap = new LambdaBootstrap(handler, null);
            bootstrap.Client = client;
            await bootstrap.InvokeOnceAsync();

            Assert.False(client.StartStreamingCalled, "StartStreamingResponseAsync should NOT be called for buffered mode");
            Assert.True(client.SendResponseCalled, "SendResponseAsync should be called for buffered mode");
            Assert.Null(client.CapturedHttpBytes);
        }

        /// <summary>
        /// End-to-end: buffered response body is transmitted correctly.
        /// Requirements: 1.5, 4.6, 9.4
        /// </summary>
        [Fact]
        public async Task Buffered_ResponseBodyTransmittedCorrectly()
        {
            var client = CreateClient();
            var responseBody = Encoding.UTF8.GetBytes("hello buffered world");

            LambdaBootstrapHandler handler = async (invocation) =>
            {
                await Task.Yield();
                return new InvocationResponse(new MemoryStream(responseBody));
            };

            using var bootstrap = new LambdaBootstrap(handler, null);
            bootstrap.Client = client;
            await bootstrap.InvokeOnceAsync();

            Assert.True(client.SendResponseCalled);
            Assert.NotNull(client.LastBufferedOutputStream);
            var received = new MemoryStream();
            await client.LastBufferedOutputStream.CopyToAsync(received);
            Assert.Equal(responseBody, received.ToArray());
        }

        // ─── 10.3 Midstream error ────────────────────────────────────────────────────

        /// <summary>
        /// End-to-end: handler writes data then throws — error trailers appear after final chunk.
        /// Requirements: 5.1, 5.2, 5.3, 5.6
        /// </summary>
        [Fact]
        public async Task MidstreamError_ErrorTrailersIncludedAfterFinalChunk()
        {
            var client = CreateClient();

            LambdaBootstrapHandler handler = async (invocation) =>
            {
                var stream = LambdaResponseStreamFactory.CreateStream();
                await stream.WriteAsync(Encoding.UTF8.GetBytes("partial data"));
                throw new InvalidOperationException("midstream failure");
            };

            using var bootstrap = new LambdaBootstrap(handler, null);
            bootstrap.Client = client;
            await bootstrap.InvokeOnceAsync();

            Assert.True(client.StartStreamingCalled);
            Assert.NotNull(client.CapturedHttpBytes);

            var output = Encoding.UTF8.GetString(client.CapturedHttpBytes);

            // Data chunk should be present
            Assert.Contains("partial data", output);

            // Final chunk must appear
            Assert.Contains("0\r\n", output);

            // Error trailers must appear after the final chunk
            var finalChunkIdx = output.LastIndexOf("0\r\n");
            var errorTypeIdx = output.IndexOf(StreamingConstants.ErrorTypeTrailer + ":");
            var errorBodyIdx = output.IndexOf(StreamingConstants.ErrorBodyTrailer + ":");

            Assert.True(errorTypeIdx > finalChunkIdx, "Error-Type trailer should appear after final chunk");
            Assert.True(errorBodyIdx > finalChunkIdx, "Error-Body trailer should appear after final chunk");

            // Error type should reference the exception type
            Assert.Contains("InvalidOperationException", output);

            // Standard error reporting should NOT be used (error went via trailers)
            Assert.False(client.ReportInvocationErrorCalled);
        }

        /// <summary>
        /// End-to-end: error body trailer contains JSON with exception details.
        /// Requirements: 5.2, 5.3
        /// </summary>
        [Fact]
        public async Task MidstreamError_ErrorBodyTrailerContainsJsonDetails()
        {
            var client = CreateClient();
            const string errorMessage = "something went wrong mid-stream";

            LambdaBootstrapHandler handler = async (invocation) =>
            {
                var stream = LambdaResponseStreamFactory.CreateStream();
                await stream.WriteAsync(Encoding.UTF8.GetBytes("some data"));
                throw new InvalidOperationException(errorMessage);
            };

            using var bootstrap = new LambdaBootstrap(handler, null);
            bootstrap.Client = client;
            await bootstrap.InvokeOnceAsync();

            var output = Encoding.UTF8.GetString(client.CapturedHttpBytes);
            Assert.Contains(StreamingConstants.ErrorBodyTrailer + ":", output);
            Assert.Contains(errorMessage, output);
        }

        // ─── 10.4 Multi-concurrency ──────────────────────────────────────────────────

        /// <summary>
        /// Multi-concurrency: concurrent invocations use AsyncLocal for state isolation.
        /// Each invocation independently uses streaming or buffered mode without interference.
        /// Requirements: 2.9, 6.5, 8.9
        /// </summary>
        [Fact]
        public async Task MultiConcurrency_ConcurrentInvocations_StateIsolated()
        {
            const int concurrency = 3;
            var results = new ConcurrentDictionary<string, string>();
            var barrier = new SemaphoreSlim(0, concurrency);
            var allStarted = new SemaphoreSlim(0, concurrency);

            // Simulate concurrent invocations using AsyncLocal directly
            var tasks = new List<Task>();
            for (int i = 0; i < concurrency; i++)
            {
                var requestId = $"req-{i}";
                var payload = $"payload-{i}";
                tasks.Add(Task.Run(async () =>
                {
                    var mockClient = new MockMultiConcurrencyStreamingClient();
                    LambdaResponseStreamFactory.InitializeInvocation(
                        requestId,
                        isMultiConcurrency: true,
                        mockClient,
                        CancellationToken.None);

                    var stream = LambdaResponseStreamFactory.CreateStream();
                    allStarted.Release();

                    // Wait until all tasks have started (to ensure true concurrency)
                    await barrier.WaitAsync();

                    await stream.WriteAsync(Encoding.UTF8.GetBytes(payload));
                    stream.MarkCompleted();

                    // Verify this invocation's stream is still accessible
                    var retrieved = LambdaResponseStreamFactory.GetStreamIfCreated(isMultiConcurrency: true);
                    results[requestId] = retrieved != null ? payload : "MISSING";

                    LambdaResponseStreamFactory.CleanupInvocation(isMultiConcurrency: true);
                }));
            }

            // Wait for all tasks to start, then release the barrier
            for (int i = 0; i < concurrency; i++)
                await allStarted.WaitAsync();
            barrier.Release(concurrency);

            await Task.WhenAll(tasks);

            // Each invocation should have seen its own stream
            Assert.Equal(concurrency, results.Count);
            for (int i = 0; i < concurrency; i++)
                Assert.Equal($"payload-{i}", results[$"req-{i}"]);
        }

        /// <summary>
        /// Multi-concurrency: streaming and buffered invocations can run concurrently without interference.
        /// Requirements: 2.9, 6.5, 8.9
        /// </summary>
        [Fact]
        public async Task MultiConcurrency_StreamingAndBufferedMixedConcurrently_NoInterference()
        {
            var streamingResults = new ConcurrentBag<bool>();
            var bufferedResults = new ConcurrentBag<bool>();
            var barrier = new SemaphoreSlim(0, 4);
            var allStarted = new SemaphoreSlim(0, 4);

            var tasks = new List<Task>();

            // 2 streaming invocations
            for (int i = 0; i < 2; i++)
            {
                var requestId = $"stream-{i}";
                tasks.Add(Task.Run(async () =>
                {
                    var mockClient = new MockMultiConcurrencyStreamingClient();
                    LambdaResponseStreamFactory.InitializeInvocation(
                        requestId, 
                        isMultiConcurrency: true, mockClient, CancellationToken.None);

                    var stream = LambdaResponseStreamFactory.CreateStream();
                    allStarted.Release();
                    await barrier.WaitAsync();

                    await stream.WriteAsync(Encoding.UTF8.GetBytes("streaming data"));
                    stream.MarkCompleted();

                    var retrieved = LambdaResponseStreamFactory.GetStreamIfCreated(isMultiConcurrency: true);
                    streamingResults.Add(retrieved != null);
                    LambdaResponseStreamFactory.CleanupInvocation(isMultiConcurrency: true);
                }));
            }

            // 2 buffered invocations (no CreateStream)
            for (int i = 0; i < 2; i++)
            {
                var requestId = $"buffered-{i}";
                tasks.Add(Task.Run(async () =>
                {
                    var mockClient = new MockMultiConcurrencyStreamingClient();
                    LambdaResponseStreamFactory.InitializeInvocation(
                        requestId, 
                        isMultiConcurrency: true, mockClient, CancellationToken.None);

                    allStarted.Release();
                    await barrier.WaitAsync();

                    // No CreateStream — buffered mode
                    var retrieved = LambdaResponseStreamFactory.GetStreamIfCreated(isMultiConcurrency: true);
                    bufferedResults.Add(retrieved == null); // should be null (no stream created)
                    LambdaResponseStreamFactory.CleanupInvocation(isMultiConcurrency: true);
                }));
            }

            for (int i = 0; i < 4; i++)
                await allStarted.WaitAsync();
            barrier.Release(4);

            await Task.WhenAll(tasks);

            Assert.Equal(2, streamingResults.Count);
            Assert.All(streamingResults, r => Assert.True(r, "Streaming invocation should have a stream"));

            Assert.Equal(2, bufferedResults.Count);
            Assert.All(bufferedResults, r => Assert.True(r, "Buffered invocation should have no stream"));
        }

        /// <summary>
        /// Minimal mock RuntimeApiClient for multi-concurrency tests.
        /// Accepts StartStreamingResponseAsync calls without real HTTP.
        /// </summary>
        private class MockMultiConcurrencyStreamingClient : RuntimeApiClient
        {
            public MockMultiConcurrencyStreamingClient()
                : base(new TestEnvironmentVariables(), new NoOpInternalRuntimeApiClient()) { }

            internal override async Task StartStreamingResponseAsync(
                string awsRequestId, LambdaResponseStream responseStream, CancellationToken cancellationToken = default)
            {
                // Provide the HTTP output stream so writes don't block
                responseStream.SetHttpOutputStream(new MemoryStream());
                await responseStream.WaitForCompletionAsync();
            }
        }

        // ─── 10.5 Backward compatibility ────────────────────────────────────────────

        /// <summary>
        /// Backward compatibility: existing handler signatures (event + ILambdaContext) work without modification.
        /// Requirements: 9.1, 9.2, 9.3
        /// </summary>
        [Fact]
        public async Task BackwardCompat_ExistingHandlerSignature_WorksUnchanged()
        {
            var client = CreateClient();
            bool handlerCalled = false;

            // Simulate a classic handler that returns a buffered response
            LambdaBootstrapHandler handler = async (invocation) =>
            {
                handlerCalled = true;
                await Task.Yield();
                return new InvocationResponse(new MemoryStream(Encoding.UTF8.GetBytes("classic response")));
            };

            using var bootstrap = new LambdaBootstrap(handler, null);
            bootstrap.Client = client;
            await bootstrap.InvokeOnceAsync();

            Assert.True(handlerCalled);
            Assert.True(client.SendResponseCalled);
            Assert.False(client.StartStreamingCalled);
        }

        /// <summary>
        /// Backward compatibility: no regression in buffered response behavior — response body is correct.
        /// Requirements: 9.4, 9.5
        /// </summary>
        [Fact]
        public async Task BackwardCompat_BufferedResponse_NoRegression()
        {
            var client = CreateClient();
            var expected = Encoding.UTF8.GetBytes("no regression here");

            LambdaBootstrapHandler handler = async (invocation) =>
            {
                await Task.Yield();
                return new InvocationResponse(new MemoryStream(expected));
            };

            using var bootstrap = new LambdaBootstrap(handler, null);
            bootstrap.Client = client;
            await bootstrap.InvokeOnceAsync();

            Assert.True(client.SendResponseCalled);
            Assert.NotNull(client.LastBufferedOutputStream);
            var received = new MemoryStream();
            await client.LastBufferedOutputStream.CopyToAsync(received);
            Assert.Equal(expected, received.ToArray());
        }

        /// <summary>
        /// Backward compatibility: handler that returns null OutputStream still works.
        /// Requirements: 9.4
        /// </summary>
        [Fact]
        public async Task BackwardCompat_NullOutputStream_HandledGracefully()
        {
            var client = CreateClient();

            LambdaBootstrapHandler handler = async (invocation) =>
            {
                await Task.Yield();
                return new InvocationResponse(Stream.Null, false);
            };

            using var bootstrap = new LambdaBootstrap(handler, null);
            bootstrap.Client = client;

            // Should not throw
            await bootstrap.InvokeOnceAsync();

            Assert.True(client.SendResponseCalled);
        }

        /// <summary>
        /// Backward compatibility: handler that throws before CreateStream uses standard error path.
        /// Requirements: 9.5
        /// </summary>
        [Fact]
        public async Task BackwardCompat_HandlerThrows_StandardErrorReportingUsed()
        {
            var client = CreateClient();

            LambdaBootstrapHandler handler = async (invocation) =>
            {
                await Task.Yield();
                throw new Exception("classic handler error");
            };

            using var bootstrap = new LambdaBootstrap(handler, null);
            bootstrap.Client = client;
            await bootstrap.InvokeOnceAsync();

            Assert.True(client.ReportInvocationErrorCalled);
            Assert.False(client.StartStreamingCalled);
        }
    }
}
