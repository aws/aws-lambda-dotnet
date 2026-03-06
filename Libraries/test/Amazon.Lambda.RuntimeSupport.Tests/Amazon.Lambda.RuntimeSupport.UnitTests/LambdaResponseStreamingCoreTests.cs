// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0
#if NET8_0_OR_GREATER
#pragma warning disable CA2252

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Lambda.Core.ResponseStreaming;
using Amazon.Lambda.RuntimeSupport.Client.ResponseStreaming;
using Xunit;

namespace Amazon.Lambda.RuntimeSupport.UnitTests
{
    // ─────────────────────────────────────────────────────────────────────────────
    // HttpResponseStreamPrelude.ToByteArray() tests
    // ─────────────────────────────────────────────────────────────────────────────

    public class HttpResponseStreamPreludeTests
    {
        private static JsonDocument ParsePrelude(HttpResponseStreamPrelude prelude)
            => JsonDocument.Parse(prelude.ToByteArray());

        [Fact]
        public void ToByteArray_EmptyPrelude_ProducesEmptyJsonObject()
        {
            var prelude = new HttpResponseStreamPrelude();
            var doc = ParsePrelude(prelude);

            Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
            // No properties should be present
            Assert.False(doc.RootElement.TryGetProperty("statusCode", out _));
            Assert.False(doc.RootElement.TryGetProperty("headers", out _));
            Assert.False(doc.RootElement.TryGetProperty("multiValueHeaders", out _));
            Assert.False(doc.RootElement.TryGetProperty("cookies", out _));
        }

        [Fact]
        public void ToByteArray_WithStatusCode_IncludesStatusCode()
        {
            var prelude = new HttpResponseStreamPrelude { StatusCode = HttpStatusCode.OK };
            var doc = ParsePrelude(prelude);

            Assert.True(doc.RootElement.TryGetProperty("statusCode", out var sc));
            Assert.Equal(200, sc.GetInt32());
        }

        [Fact]
        public void ToByteArray_WithHeaders_IncludesHeaders()
        {
            var prelude = new HttpResponseStreamPrelude
            {
                Headers = new Dictionary<string, string>
                {
                    ["Content-Type"] = "application/json",
                    ["X-Custom"] = "value"
                }
            };
            var doc = ParsePrelude(prelude);

            Assert.True(doc.RootElement.TryGetProperty("headers", out var headers));
            Assert.Equal("application/json", headers.GetProperty("Content-Type").GetString());
            Assert.Equal("value", headers.GetProperty("X-Custom").GetString());
        }

        [Fact]
        public void ToByteArray_WithMultiValueHeaders_IncludesMultiValueHeaders()
        {
            var prelude = new HttpResponseStreamPrelude
            {
                MultiValueHeaders = new Dictionary<string, IList<string>>
                {
                    ["Set-Cookie"] = new List<string> { "a=1", "b=2" }
                }
            };
            var doc = ParsePrelude(prelude);

            Assert.True(doc.RootElement.TryGetProperty("multiValueHeaders", out var mvh));
            var cookies = mvh.GetProperty("Set-Cookie");
            Assert.Equal(JsonValueKind.Array, cookies.ValueKind);
            Assert.Equal(2, cookies.GetArrayLength());
        }

        [Fact]
        public void ToByteArray_WithCookies_IncludesCookies()
        {
            var prelude = new HttpResponseStreamPrelude
            {
                Cookies = new List<string> { "session=abc", "pref=dark" }
            };
            var doc = ParsePrelude(prelude);

            Assert.True(doc.RootElement.TryGetProperty("cookies", out var cookies));
            Assert.Equal(JsonValueKind.Array, cookies.ValueKind);
            Assert.Equal(2, cookies.GetArrayLength());
            Assert.Equal("session=abc", cookies[0].GetString());
        }

        [Fact]
        public void ToByteArray_AllFieldsPopulated_ProducesCorrectJson()
        {
            var prelude = new HttpResponseStreamPrelude
            {
                StatusCode = HttpStatusCode.Created,
                Headers = new Dictionary<string, string> { ["X-Req"] = "1" },
                MultiValueHeaders = new Dictionary<string, IList<string>> { ["X-Multi"] = new List<string> { "a", "b" } },
                Cookies = new List<string> { "c=1" }
            };
            var doc = ParsePrelude(prelude);

            Assert.Equal(201, doc.RootElement.GetProperty("statusCode").GetInt32());
            Assert.Equal("1", doc.RootElement.GetProperty("headers").GetProperty("X-Req").GetString());
            Assert.Equal(2, doc.RootElement.GetProperty("multiValueHeaders").GetProperty("X-Multi").GetArrayLength());
            Assert.Equal("c=1", doc.RootElement.GetProperty("cookies")[0].GetString());
        }

        [Fact]
        public void ToByteArray_EmptyCollections_OmitsThoseFields()
        {
            var prelude = new HttpResponseStreamPrelude
            {
                StatusCode = HttpStatusCode.OK,
                Headers = new Dictionary<string, string>(),       // empty — should be omitted
                MultiValueHeaders = new Dictionary<string, IList<string>>(), // empty
                Cookies = new List<string>()                       // empty
            };
            var doc = ParsePrelude(prelude);

            Assert.True(doc.RootElement.TryGetProperty("statusCode", out _));
            Assert.False(doc.RootElement.TryGetProperty("headers", out _));
            Assert.False(doc.RootElement.TryGetProperty("multiValueHeaders", out _));
            Assert.False(doc.RootElement.TryGetProperty("cookies", out _));
        }

        [Fact]
        public void ToByteArray_ProducesValidUtf8()
        {
            var prelude = new HttpResponseStreamPrelude
            {
                StatusCode = HttpStatusCode.OK,
                Headers = new Dictionary<string, string> { ["Content-Type"] = "text/plain; charset=utf-8" }
            };
            var bytes = prelude.ToByteArray();

            // Should not throw
            var text = Encoding.UTF8.GetString(bytes);
            Assert.NotEmpty(text);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // LambdaResponseStream (Stream subclass) tests
    // ─────────────────────────────────────────────────────────────────────────────

    public class LambdaResponseStreamTests
    {
        /// <summary>
        /// Creates a LambdaResponseStream backed by a real ResponseStream wired to a MemoryStream.
        /// </summary>
        private static async Task<(LambdaResponseStream lambdaStream, MemoryStream httpOutput)> CreateWiredLambdaStream()
        {
            var inner = new ResponseStream(Array.Empty<byte>());
            var output = new MemoryStream();
            await inner.SetHttpOutputStreamAsync(output);

            var implStream = new ResponseStreamLambdaCoreInitializerIsolated.ImplLambdaResponseStream(inner);
            var lambdaStream = new LambdaResponseStream(implStream);
            return (lambdaStream, output);
        }

        [Fact]
        public void LambdaResponseStream_IsStreamSubclass()
        {
            var inner = new ResponseStream(Array.Empty<byte>());
            var impl = new ResponseStreamLambdaCoreInitializerIsolated.ImplLambdaResponseStream(inner);
            var stream = new LambdaResponseStream(impl);

            Assert.IsAssignableFrom<Stream>(stream);
        }

        [Fact]
        public void CanWrite_IsTrue()
        {
            var inner = new ResponseStream(Array.Empty<byte>());
            var impl = new ResponseStreamLambdaCoreInitializerIsolated.ImplLambdaResponseStream(inner);
            var stream = new LambdaResponseStream(impl);

            Assert.True(stream.CanWrite);
        }

        [Fact]
        public void CanRead_IsFalse()
        {
            var inner = new ResponseStream(Array.Empty<byte>());
            var impl = new ResponseStreamLambdaCoreInitializerIsolated.ImplLambdaResponseStream(inner);
            var stream = new LambdaResponseStream(impl);

            Assert.False(stream.CanRead);
        }

        [Fact]
        public void CanSeek_IsFalse()
        {
            var inner = new ResponseStream(Array.Empty<byte>());
            var impl = new ResponseStreamLambdaCoreInitializerIsolated.ImplLambdaResponseStream(inner);
            var stream = new LambdaResponseStream(impl);

            Assert.False(stream.CanSeek);
        }

        [Fact]
        public void Read_ThrowsNotImplementedException()
        {
            var inner = new ResponseStream(Array.Empty<byte>());
            var impl = new ResponseStreamLambdaCoreInitializerIsolated.ImplLambdaResponseStream(inner);
            var stream = new LambdaResponseStream(impl);

            Assert.Throws<NotImplementedException>(() => stream.Read(new byte[1], 0, 1));
        }

        [Fact]
        public void ReadAsync_ThrowsNotImplementedException()
        {
            var inner = new ResponseStream(Array.Empty<byte>());
            var impl = new ResponseStreamLambdaCoreInitializerIsolated.ImplLambdaResponseStream(inner);
            var stream = new LambdaResponseStream(impl);

            // ReadAsync throws synchronously (not async) — capture the thrown task
            var ex = Assert.Throws<NotImplementedException>(
                () => { var _ = stream.ReadAsync(new byte[1], 0, 1, CancellationToken.None); });
            Assert.NotNull(ex);
        }

        [Fact]
        public void Seek_ThrowsNotImplementedException()
        {
            var inner = new ResponseStream(Array.Empty<byte>());
            var impl = new ResponseStreamLambdaCoreInitializerIsolated.ImplLambdaResponseStream(inner);
            var stream = new LambdaResponseStream(impl);

            Assert.Throws<NotImplementedException>(() => stream.Seek(0, SeekOrigin.Begin));
        }

        [Fact]
        public void Position_Get_ThrowsNotSupportedException()
        {
            var inner = new ResponseStream(Array.Empty<byte>());
            var impl = new ResponseStreamLambdaCoreInitializerIsolated.ImplLambdaResponseStream(inner);
            var stream = new LambdaResponseStream(impl);

            Assert.Throws<NotSupportedException>(() => _ = stream.Position);
        }

        [Fact]
        public void Position_Set_ThrowsNotSupportedException()
        {
            var inner = new ResponseStream(Array.Empty<byte>());
            var impl = new ResponseStreamLambdaCoreInitializerIsolated.ImplLambdaResponseStream(inner);
            var stream = new LambdaResponseStream(impl);

            Assert.Throws<NotSupportedException>(() => stream.Position = 0);
        }

        [Fact]
        public void SetLength_ThrowsNotSupportedException()
        {
            var inner = new ResponseStream(Array.Empty<byte>());
            var impl = new ResponseStreamLambdaCoreInitializerIsolated.ImplLambdaResponseStream(inner);
            var stream = new LambdaResponseStream(impl);

            Assert.Throws<NotSupportedException>(() => stream.SetLength(100));
        }

        [Fact]
        public async Task WriteAsync_WritesRawBytesToHttpStream()
        {
            var (stream, output) = await CreateWiredLambdaStream();
            var data = Encoding.UTF8.GetBytes("hello streaming");

            await stream.WriteAsync(data, 0, data.Length);

            Assert.Equal(data, output.ToArray());
        }

        [Fact]
        public async Task Write_SyncOverload_WritesRawBytes()
        {
            var (stream, output) = await CreateWiredLambdaStream();
            var data = new byte[] { 1, 2, 3 };

            stream.Write(data, 0, data.Length);

            Assert.Equal(data, output.ToArray());
        }

        [Fact]
        public async Task Length_ReflectsBytesWritten()
        {
            var (stream, _) = await CreateWiredLambdaStream();
            var data = new byte[42];

            await stream.WriteAsync(data, 0, data.Length);

            Assert.Equal(42, stream.Length);
            Assert.Equal(42, stream.BytesWritten);
        }

        [Fact]
        public async Task Flush_IsNoOp()
        {
            var (stream, _) = await CreateWiredLambdaStream();
            // Should not throw
            stream.Flush();
        }

        [Fact]
        public async Task WriteAsync_ByteArrayOverload_WritesFullArray()
        {
            var (stream, output) = await CreateWiredLambdaStream();
            var data = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };

            await stream.WriteAsync(data);

            Assert.Equal(data, output.ToArray());
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // ImplLambdaResponseStream (bridge class) tests
    // ─────────────────────────────────────────────────────────────────────────────

    public class ImplLambdaResponseStreamTests
    {
        [Fact]
        public async Task WriteAsync_DelegatesToInnerResponseStream()
        {
            var inner = new ResponseStream(Array.Empty<byte>());
            var output = new MemoryStream();
            await inner.SetHttpOutputStreamAsync(output);

            var impl = new ResponseStreamLambdaCoreInitializerIsolated.ImplLambdaResponseStream(inner);
            var data = new byte[] { 1, 2, 3 };

            await impl.WriteAsync(data, 0, data.Length);

            Assert.Equal(data, output.ToArray());
        }

        [Fact]
        public async Task BytesWritten_ReflectsInnerStreamBytesWritten()
        {
            var inner = new ResponseStream(Array.Empty<byte>());
            var output = new MemoryStream();
            await inner.SetHttpOutputStreamAsync(output);

            var impl = new ResponseStreamLambdaCoreInitializerIsolated.ImplLambdaResponseStream(inner);
            await impl.WriteAsync(new byte[7], 0, 7);

            Assert.Equal(7, impl.BytesWritten);
        }

        [Fact]
        public void HasError_InitiallyFalse()
        {
            var inner = new ResponseStream(Array.Empty<byte>());
            var impl = new ResponseStreamLambdaCoreInitializerIsolated.ImplLambdaResponseStream(inner);

            Assert.False(impl.HasError);
        }

        [Fact]
        public void HasError_TrueAfterReportError()
        {
            var inner = new ResponseStream(Array.Empty<byte>());
            inner.ReportError(new Exception("test"));

            var impl = new ResponseStreamLambdaCoreInitializerIsolated.ImplLambdaResponseStream(inner);

            Assert.True(impl.HasError);
        }

        [Fact]
        public void Dispose_DisposesInnerStream()
        {
            var inner = new ResponseStream(Array.Empty<byte>());
            var impl = new ResponseStreamLambdaCoreInitializerIsolated.ImplLambdaResponseStream(inner);

            // Should not throw
            impl.Dispose();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // LambdaResponseStreamFactory tests
    // ─────────────────────────────────────────────────────────────────────────────

    [Collection("ResponseStreamFactory")]
    public class LambdaResponseStreamFactoryTests : IDisposable
    {

        public LambdaResponseStreamFactoryTests()
        {
            // Wire up the factory via the initializer (same as production bootstrap does)
            ResponseStreamLambdaCoreInitializerIsolated.InitializeCore();
        }

        public void Dispose()
        {
            ResponseStreamFactory.CleanupInvocation(isMultiConcurrency: false);
        }

        private void InitializeInvocation(string requestId = "test-req")
        {
            var envVars = new TestEnvironmentVariables();
            var client = new NoOpStreamingRuntimeApiClient(envVars);
            ResponseStreamFactory.InitializeInvocation(requestId, false, client, CancellationToken.None);
        }

        /// <summary>
        /// Minimal RuntimeApiClient that accepts StartStreamingResponseAsync without real HTTP.
        /// </summary>
        private class NoOpStreamingRuntimeApiClient : RuntimeApiClient
        {
            public NoOpStreamingRuntimeApiClient(IEnvironmentVariables envVars)
                : base(envVars, new TestHelpers.NoOpInternalRuntimeApiClient()) { }

            internal override async Task StartStreamingResponseAsync(
                string awsRequestId, ResponseStream responseStream, CancellationToken cancellationToken = default)
            {
                // Provide the HTTP output stream so writes don't block
                await responseStream.SetHttpOutputStreamAsync(new MemoryStream(), cancellationToken);
                await responseStream.WaitForCompletionAsync(cancellationToken);
            }
        }

        [Fact]
        public void CreateStream_ReturnsLambdaResponseStream()
        {
            InitializeInvocation();

            var stream = LambdaResponseStreamFactory.CreateStream();

            Assert.NotNull(stream);
            Assert.IsType<LambdaResponseStream>(stream);
        }

        [Fact]
        public void CreateStream_ReturnsStreamSubclass()
        {
            InitializeInvocation();

            var stream = LambdaResponseStreamFactory.CreateStream();

            Assert.IsAssignableFrom<Stream>(stream);
        }

        [Fact]
        public void CreateStream_ReturnedStream_IsWritable()
        {
            InitializeInvocation();

            var stream = LambdaResponseStreamFactory.CreateStream();

            Assert.True(stream.CanWrite);
        }

        [Fact]
        public void CreateStream_ReturnedStream_IsNotSeekable()
        {
            InitializeInvocation();

            var stream = LambdaResponseStreamFactory.CreateStream();

            Assert.False(stream.CanSeek);
        }

        [Fact]
        public void CreateStream_ReturnedStream_IsNotReadable()
        {
            InitializeInvocation();

            var stream = LambdaResponseStreamFactory.CreateStream();

            Assert.False(stream.CanRead);
        }

        [Fact]
        public void CreateHttpStream_WithPrelude_ReturnsLambdaResponseStream()
        {
            InitializeInvocation();

            var prelude = new HttpResponseStreamPrelude { StatusCode = HttpStatusCode.OK };
            var stream = LambdaResponseStreamFactory.CreateHttpStream(prelude);

            Assert.NotNull(stream);
            Assert.IsType<LambdaResponseStream>(stream);
        }

        [Fact]
        public void CreateHttpStream_PassesSerializedPreludeToFactory()
        {
            // Capture the prelude bytes passed to the inner factory
            byte[] capturedPrelude = null;
            LambdaResponseStreamFactory.SetLambdaResponseStream(prelude =>
            {
                capturedPrelude = prelude;
                // Return a minimal stub that satisfies the interface
                return new StubLambdaResponseStream();
            });

            var httpPrelude = new HttpResponseStreamPrelude
            {
                StatusCode = HttpStatusCode.Created,
                Headers = new Dictionary<string, string> { ["X-Test"] = "1" }
            };
            LambdaResponseStreamFactory.CreateHttpStream(httpPrelude);

            Assert.NotNull(capturedPrelude);
            Assert.True(capturedPrelude.Length > 0);

            // Verify the bytes are valid JSON containing the status code
            var doc = JsonDocument.Parse(capturedPrelude);
            Assert.Equal(201, doc.RootElement.GetProperty("statusCode").GetInt32());
        }

        [Fact]
        public void CreateStream_PassesEmptyPreludeToFactory()
        {
            byte[] capturedPrelude = null;
            LambdaResponseStreamFactory.SetLambdaResponseStream(prelude =>
            {
                capturedPrelude = prelude;
                return new StubLambdaResponseStream();
            });

            LambdaResponseStreamFactory.CreateStream();

            Assert.NotNull(capturedPrelude);
            Assert.Empty(capturedPrelude);
        }

        private class StubLambdaResponseStream : ILambdaResponseStream
        {
            public long BytesWritten => 0;
            public bool HasError => false;
            public void Dispose() { }
            public Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken = default)
                => Task.CompletedTask;
        }
    }
}
#endif
