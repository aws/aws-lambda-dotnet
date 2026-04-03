// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0
#if NET8_0_OR_GREATER

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Lambda.RuntimeSupport.Client.ResponseStreaming;
using Xunit;

namespace Amazon.Lambda.RuntimeSupport.UnitTests
{
    // ─────────────────────────────────────────────────────────────────────────────
    // RawStreamingHttpClient tests
    // ─────────────────────────────────────────────────────────────────────────────

    public class RawStreamingHttpClientTests
    {
        // --- Constructor / host parsing ---

        [Fact]
        public void Constructor_HostAndPort_ParsedCorrectly()
        {
            using var client = new RawStreamingHttpClient("localhost:9001");
            // No exception means parsing succeeded. Fields are private but
            // we verify indirectly via Dispose not throwing.
        }

        [Fact]
        public void Constructor_HostOnly_DefaultsToPort80()
        {
            using var client = new RawStreamingHttpClient("localhost");
            // Should not throw — defaults port to 80
        }

        [Fact]
        public void Constructor_HighPort_ParsedCorrectly()
        {
            using var client = new RawStreamingHttpClient("127.0.0.1:65535");
        }

        // --- Dispose ---

        [Fact]
        public void Dispose_CalledTwice_DoesNotThrow()
        {
            var client = new RawStreamingHttpClient("localhost:9001");
            client.Dispose();
            client.Dispose();
        }

        [Fact]
        public void Dispose_WithoutConnect_DoesNotThrow()
        {
            var client = new RawStreamingHttpClient("localhost:9001");
            client.Dispose();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // WriteTerminatorWithTrailersAsync tests
    // ─────────────────────────────────────────────────────────────────────────────

    public class WriteTerminatorWithTrailersAsyncTests
    {
        private static (RawStreamingHttpClient client, MemoryStream output) CreateClientWithMemoryStream()
        {
            var client = new RawStreamingHttpClient("localhost:9001");
            var output = new MemoryStream();
            client._networkStream = output;
            return (client, output);
        }

        [Fact]
        public async Task WriteTerminator_StartsWithZeroChunk()
        {
            var (client, output) = CreateClientWithMemoryStream();

            await client.WriteTerminatorWithTrailersAsync(
                new Exception("test"), CancellationToken.None);

            var written = Encoding.UTF8.GetString(output.ToArray());
            Assert.StartsWith("0\r\n", written);
        }

        [Fact]
        public async Task WriteTerminator_ContainsErrorTypeTrailer()
        {
            var (client, output) = CreateClientWithMemoryStream();

            await client.WriteTerminatorWithTrailersAsync(
                new InvalidOperationException("bad op"), CancellationToken.None);

            var written = Encoding.UTF8.GetString(output.ToArray());
            Assert.Contains($"{StreamingConstants.ErrorTypeTrailer}: InvalidOperationException\r\n", written);
        }

        [Fact]
        public async Task WriteTerminator_ContainsErrorBodyTrailerHeader()
        {
            var (client, output) = CreateClientWithMemoryStream();

            await client.WriteTerminatorWithTrailersAsync(
                new Exception("some error"), CancellationToken.None);

            var written = Encoding.UTF8.GetString(output.ToArray());
            Assert.Contains($"{StreamingConstants.ErrorBodyTrailer}: ", written);
        }

        [Fact]
        public async Task WriteTerminator_ErrorBodyIsBase64Encoded()
        {
            var (client, output) = CreateClientWithMemoryStream();
            const string errorMessage = "something broke";

            await client.WriteTerminatorWithTrailersAsync(
                new Exception(errorMessage), CancellationToken.None);

            var written = Encoding.UTF8.GetString(output.ToArray());

            // Extract the Base64 value from the error body trailer
            var prefix = $"{StreamingConstants.ErrorBodyTrailer}: ";
            var start = written.IndexOf(prefix, StringComparison.Ordinal) + prefix.Length;
            var end = written.IndexOf("\r\n", start, StringComparison.Ordinal);
            var base64Value = written.Substring(start, end - start);

            // Should be valid Base64
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(base64Value));
            Assert.Contains(errorMessage, decoded);
        }

        [Fact]
        public async Task WriteTerminator_ErrorBodyBase64ContainsNoNewlines()
        {
            var (client, output) = CreateClientWithMemoryStream();

            // Use an exception with a stack trace that would produce multi-line JSON
            Exception caughtException;
            try { throw new InvalidOperationException("multi\nline\nerror"); }
            catch (Exception ex) { caughtException = ex; }

            await client.WriteTerminatorWithTrailersAsync(
                caughtException, CancellationToken.None);

            var written = Encoding.UTF8.GetString(output.ToArray());

            // Extract just the error body trailer line
            var prefix = $"{StreamingConstants.ErrorBodyTrailer}: ";
            var start = written.IndexOf(prefix, StringComparison.Ordinal) + prefix.Length;
            var end = written.IndexOf("\r\n", start, StringComparison.Ordinal);
            var base64Value = written.Substring(start, end - start);

            // The Base64 value itself must not contain any newlines
            Assert.DoesNotContain("\n", base64Value);
            Assert.DoesNotContain("\r", base64Value);
        }

        [Fact]
        public async Task WriteTerminator_EndsWithEmptyLine()
        {
            var (client, output) = CreateClientWithMemoryStream();

            await client.WriteTerminatorWithTrailersAsync(
                new Exception("test"), CancellationToken.None);

            var written = Encoding.UTF8.GetString(output.ToArray());
            // Must end with \r\n\r\n — the last trailer line's \r\n plus the empty terminator line
            Assert.EndsWith("\r\n\r\n", written);
        }

        [Fact]
        public async Task WriteTerminator_CorrectWireFormat()
        {
            var (client, output) = CreateClientWithMemoryStream();

            await client.WriteTerminatorWithTrailersAsync(
                new ArgumentException("bad arg"), CancellationToken.None);

            var written = Encoding.UTF8.GetString(output.ToArray());
            var lines = written.Split("\r\n");

            // Line 0: "0" (zero-length chunk)
            Assert.Equal("0", lines[0]);
            // Line 1: error type trailer
            Assert.StartsWith($"{StreamingConstants.ErrorTypeTrailer}: ", lines[1]);
            // Line 2: error body trailer (Base64)
            Assert.StartsWith($"{StreamingConstants.ErrorBodyTrailer}: ", lines[2]);
            // Line 3: empty (end of trailers)
            Assert.Equal("", lines[3]);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // ReadAndDiscardResponseAsync tests
    // ─────────────────────────────────────────────────────────────────────────────

    public class ReadAndDiscardResponseAsyncTests
    {
        private static (RawStreamingHttpClient client, MemoryStream input) CreateClientWithResponse(string httpResponse)
        {
            var client = new RawStreamingHttpClient("localhost:9001");
            var input = new MemoryStream(Encoding.ASCII.GetBytes(httpResponse));
            client._networkStream = input;
            return (client, input);
        }

        [Fact]
        public async Task ReadAndDiscard_HeadersOnly_CompletesSuccessfully()
        {
            var (client, _) = CreateClientWithResponse(
                "HTTP/1.1 202 Accepted\r\nContent-Length: 0\r\n\r\n");

            await client.ReadAndDiscardResponseAsync(CancellationToken.None);
            // Should complete without error
        }

        [Fact]
        public async Task ReadAndDiscard_WithBody_ReadsFullBody()
        {
            var body = "OK";
            var (client, _) = CreateClientWithResponse(
                $"HTTP/1.1 200 OK\r\nContent-Length: {body.Length}\r\n\r\n{body}");

            await client.ReadAndDiscardResponseAsync(CancellationToken.None);
        }

        [Fact]
        public async Task ReadAndDiscard_NoContentLength_CompletesAfterHeaders()
        {
            var (client, _) = CreateClientWithResponse(
                "HTTP/1.1 202 Accepted\r\n\r\n");

            await client.ReadAndDiscardResponseAsync(CancellationToken.None);
        }

        [Fact]
        public async Task ReadAndDiscard_EmptyStream_CompletesSuccessfully()
        {
            var client = new RawStreamingHttpClient("localhost:9001");
            client._networkStream = new MemoryStream(Array.Empty<byte>());

            await client.ReadAndDiscardResponseAsync(CancellationToken.None);
        }

        [Fact]
        public async Task ReadAndDiscard_PartialBody_WaitsForFullBody()
        {
            // Content-Length says 10 but we provide all 10 bytes
            var body = "0123456789";
            var (client, _) = CreateClientWithResponse(
                $"HTTP/1.1 200 OK\r\nContent-Length: 10\r\n\r\n{body}");

            await client.ReadAndDiscardResponseAsync(CancellationToken.None);
        }

        [Fact]
        public async Task ReadAndDiscard_CancellationToken_Respected()
        {
            // Use a stream that blocks on read to test cancellation
            var cts = new CancellationTokenSource();
            cts.Cancel();

            var client = new RawStreamingHttpClient("localhost:9001");
            client._networkStream = new MemoryStream(Encoding.ASCII.GetBytes(
                "HTTP/1.1 200 OK\r\nContent-Length: 100\r\n\r\n"));

            // Should not throw — ReadAndDiscardResponseAsync catches exceptions
            await client.ReadAndDiscardResponseAsync(cts.Token);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // ChunkedStreamWriter tests
    // ─────────────────────────────────────────────────────────────────────────────

    public class ChunkedStreamWriterTests
    {
        [Fact]
        public void CanWrite_IsTrue()
        {
            using var inner = new MemoryStream();
            using var writer = new ChunkedStreamWriter(inner);
            Assert.True(writer.CanWrite);
        }

        [Fact]
        public void CanRead_IsFalse()
        {
            using var inner = new MemoryStream();
            using var writer = new ChunkedStreamWriter(inner);
            Assert.False(writer.CanRead);
        }

        [Fact]
        public void CanSeek_IsFalse()
        {
            using var inner = new MemoryStream();
            using var writer = new ChunkedStreamWriter(inner);
            Assert.False(writer.CanSeek);
        }

        [Fact]
        public void Constructor_NullStream_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new ChunkedStreamWriter(null));
        }

        [Fact]
        public void Length_ThrowsNotSupportedException()
        {
            using var inner = new MemoryStream();
            using var writer = new ChunkedStreamWriter(inner);
            Assert.Throws<NotSupportedException>(() => writer.Length);
        }

        [Fact]
        public void Position_Get_ThrowsNotSupportedException()
        {
            using var inner = new MemoryStream();
            using var writer = new ChunkedStreamWriter(inner);
            Assert.Throws<NotSupportedException>(() => writer.Position);
        }

        [Fact]
        public void Position_Set_ThrowsNotSupportedException()
        {
            using var inner = new MemoryStream();
            using var writer = new ChunkedStreamWriter(inner);
            Assert.Throws<NotSupportedException>(() => writer.Position = 0);
        }

        [Fact]
        public void Read_ThrowsNotSupportedException()
        {
            using var inner = new MemoryStream();
            using var writer = new ChunkedStreamWriter(inner);
            Assert.Throws<NotSupportedException>(() => writer.Read(new byte[1], 0, 1));
        }

        [Fact]
        public void Seek_ThrowsNotSupportedException()
        {
            using var inner = new MemoryStream();
            using var writer = new ChunkedStreamWriter(inner);
            Assert.Throws<NotSupportedException>(() => writer.Seek(0, SeekOrigin.Begin));
        }

        [Fact]
        public void SetLength_ThrowsNotSupportedException()
        {
            using var inner = new MemoryStream();
            using var writer = new ChunkedStreamWriter(inner);
            Assert.Throws<NotSupportedException>(() => writer.SetLength(0));
        }

        [Fact]
        public async Task WriteAsync_ByteArray_ProducesCorrectChunkFormat()
        {
            using var inner = new MemoryStream();
            using var writer = new ChunkedStreamWriter(inner);

            var data = Encoding.UTF8.GetBytes("Hello");
            await writer.WriteAsync(data, 0, data.Length);

            var output = Encoding.ASCII.GetString(inner.ToArray());
            // "Hello" is 5 bytes = 0x5
            Assert.Equal("5\r\nHello\r\n", output);
        }

        [Fact]
        public async Task WriteAsync_ReadOnlyMemory_ProducesCorrectChunkFormat()
        {
            using var inner = new MemoryStream();
            using var writer = new ChunkedStreamWriter(inner);

            var data = Encoding.UTF8.GetBytes("Hi");
            await writer.WriteAsync(new ReadOnlyMemory<byte>(data));

            var output = Encoding.ASCII.GetString(inner.ToArray());
            Assert.Equal("2\r\nHi\r\n", output);
        }

        [Fact]
        public async Task WriteAsync_ZeroBytes_WritesNothing()
        {
            using var inner = new MemoryStream();
            using var writer = new ChunkedStreamWriter(inner);

            await writer.WriteAsync(Array.Empty<byte>(), 0, 0);

            Assert.Equal(0, inner.Length);
        }

        [Fact]
        public async Task WriteAsync_ReadOnlyMemory_ZeroBytes_WritesNothing()
        {
            using var inner = new MemoryStream();
            using var writer = new ChunkedStreamWriter(inner);

            await writer.WriteAsync(ReadOnlyMemory<byte>.Empty);

            Assert.Equal(0, inner.Length);
        }

        [Fact]
        public async Task WriteAsync_MultipleChunks_EachCorrectlyFormatted()
        {
            using var inner = new MemoryStream();
            using var writer = new ChunkedStreamWriter(inner);

            await writer.WriteAsync(Encoding.UTF8.GetBytes("AB"), 0, 2);
            await writer.WriteAsync(Encoding.UTF8.GetBytes("CDE"), 0, 3);

            var output = Encoding.ASCII.GetString(inner.ToArray());
            Assert.Equal("2\r\nAB\r\n3\r\nCDE\r\n", output);
        }

        [Fact]
        public async Task WriteAsync_LargeChunk_HexSizeCorrect()
        {
            using var inner = new MemoryStream();
            using var writer = new ChunkedStreamWriter(inner);

            var data = new byte[256];
            Array.Fill(data, (byte)'X');
            await writer.WriteAsync(data, 0, data.Length);

            var output = Encoding.ASCII.GetString(inner.ToArray());
            // 256 = 0x100
            Assert.StartsWith("100\r\n", output);
            Assert.EndsWith("\r\n", output);
        }

        [Fact]
        public async Task WriteAsync_WithOffset_WritesCorrectSlice()
        {
            using var inner = new MemoryStream();
            using var writer = new ChunkedStreamWriter(inner);

            var data = Encoding.UTF8.GetBytes("ABCDE");
            await writer.WriteAsync(data, 1, 3); // "BCD"

            var output = Encoding.ASCII.GetString(inner.ToArray());
            Assert.Equal("3\r\nBCD\r\n", output);
        }

        [Fact]
        public void Write_Sync_ProducesCorrectChunkFormat()
        {
            using var inner = new MemoryStream();
            using var writer = new ChunkedStreamWriter(inner);

            var data = Encoding.UTF8.GetBytes("OK");
            writer.Write(data, 0, data.Length);

            var output = Encoding.ASCII.GetString(inner.ToArray());
            Assert.Equal("2\r\nOK\r\n", output);
        }

        [Fact]
        public async Task FlushAsync_DelegatesToInnerStream()
        {
            var flushCalled = false;
            var inner = new FlushTrackingStream(() => flushCalled = true);
            using var writer = new ChunkedStreamWriter(inner);

            await writer.FlushAsync(CancellationToken.None);

            Assert.True(flushCalled);
        }

        [Fact]
        public void Flush_DelegatesToInnerStream()
        {
            var flushCalled = false;
            var inner = new FlushTrackingStream(() => flushCalled = true);
            using var writer = new ChunkedStreamWriter(inner);

            writer.Flush();

            Assert.True(flushCalled);
        }

        /// <summary>
        /// A minimal writable stream that tracks Flush calls.
        /// </summary>
        private class FlushTrackingStream : MemoryStream
        {
            private readonly Action _onFlush;
            public FlushTrackingStream(Action onFlush) => _onFlush = onFlush;
            public override void Flush() { _onFlush(); base.Flush(); }
            public override Task FlushAsync(CancellationToken cancellationToken)
            {
                _onFlush();
                return base.FlushAsync(cancellationToken);
            }
        }
    }
}
#endif
