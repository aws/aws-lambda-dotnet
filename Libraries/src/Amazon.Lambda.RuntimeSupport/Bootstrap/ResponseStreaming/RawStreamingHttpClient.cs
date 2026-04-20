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

#if NET8_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Lambda.RuntimeSupport.Helpers;

namespace Amazon.Lambda.RuntimeSupport.Client.ResponseStreaming
{
    /// <summary>
    /// A raw HTTP/1.1 client for sending streaming responses to the Lambda Runtime API
    /// with support for HTTP trailing headers (used for error reporting).
    ///
    /// .NET's HttpClient/SocketsHttpHandler does not support sending HTTP/1.1 trailing headers.
    /// The Lambda Runtime API requires error information to be sent as trailing headers
    /// (Lambda-Runtime-Function-Error-Type and Lambda-Runtime-Function-Error-Body) after
    /// the chunked transfer encoding body. This class gives us full control over the
    /// HTTP wire format to properly send those trailers.
    /// </summary>
    internal class RawStreamingHttpClient : IDisposable
    {
        private readonly string _host;
        private readonly int _port;
        private TcpClient _tcpClient;
        internal Stream _networkStream;
        private bool _disposed;

        private readonly InternalLogger _logger = InternalLogger.GetDefaultLogger();

        public RawStreamingHttpClient(string hostAndPort)
        {
            var parts = hostAndPort.Split(':');
            if (parts.Length != 2)
                throw new ArgumentException($"Invalid host and port format: {hostAndPort}. Expected format is 'host:port'"); 

            _host = parts[0];
            _port = int.Parse(parts[1], CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Sends a streaming response to the Lambda Runtime API.
        /// Connects via TCP, sends HTTP headers, then streams the response body
        /// using chunked transfer encoding. When the response stream completes,
        /// writes the chunked encoding terminator with optional trailing headers
        /// for error reporting.
        /// </summary>
        /// <param name="awsRequestId">The Lambda request ID.</param>
        /// <param name="responseStream">The response stream that provides data and error state.</param>
        /// <param name="userAgent">The User-Agent header value.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task SendStreamingResponseAsync(
            string awsRequestId,
            ResponseStream responseStream,
            string userAgent,
            CancellationToken cancellationToken = default)
        {
            _tcpClient = new TcpClient();
            _tcpClient.NoDelay = true;
            await _tcpClient.ConnectAsync(_host, _port, cancellationToken);
            _networkStream = _tcpClient.GetStream();

            // Send HTTP request line and headers
            var path = $"/2018-06-01/runtime/invocation/{awsRequestId}/response";
            var headers = new StringBuilder();
            headers.Append($"POST {path} HTTP/1.1\r\n");
            headers.Append($"Host: {_host}:{_port}\r\n");
            headers.Append($"User-Agent: {userAgent}\r\n");
            headers.Append($"Content-Type: application/vnd.awslambda.http-integration-response\r\n");
            headers.Append($"{StreamingConstants.ResponseModeHeader}: {StreamingConstants.StreamingResponseMode}\r\n");
            headers.Append("Transfer-Encoding: chunked\r\n");
            headers.Append($"Trailer: {StreamingConstants.ErrorTypeTrailer}, {StreamingConstants.ErrorBodyTrailer}\r\n");
            headers.Append("\r\n");

            var headerBytes = Encoding.ASCII.GetBytes(headers.ToString());
            await _networkStream.WriteAsync(headerBytes, cancellationToken);
            await _networkStream.FlushAsync(cancellationToken);

            // Hand the network stream (wrapped in a chunked writer) to the ResponseStream
            var chunkedWriter = new ChunkedStreamWriter(_networkStream);
            await responseStream.SetHttpOutputStreamAsync(chunkedWriter, cancellationToken);

            _logger.LogInformation("In SendStreamingResponseAsync waiting for the underlying Lambda response stream to indicate it is complete.");

            // Wait for the handler to finish writing
            await responseStream.WaitForCompletionAsync(cancellationToken);

            // Write the chunked encoding terminator with optional trailers
            if (responseStream.HasError)
            {
                _logger.LogInformation("Adding response stream trailing error headers");
                await WriteTerminatorWithTrailersAsync(responseStream.ReportedError, cancellationToken);
            }
            else
            {
                // No error — write simple terminator: 0\r\n\r\n
                var terminator = Encoding.ASCII.GetBytes("0\r\n\r\n");
                await _networkStream.WriteAsync(terminator, cancellationToken);
            }

            await _networkStream.FlushAsync(cancellationToken);

            // Read and discard the HTTP response (we don't need it, but must consume it)
            await ReadAndDiscardResponseAsync(cancellationToken);
        }

        /// <summary>
        /// Writes the chunked encoding terminator with HTTP trailing headers for error reporting.
        /// Format:
        ///   0\r\n
        ///   Lambda-Runtime-Function-Error-Type: errorType\r\n
        ///   Lambda-Runtime-Function-Error-Body: base64EncodedErrorBodyJson\r\n
        ///   \r\n
        ///
        /// The error body JSON is Base64-encoded because LambdaJsonExceptionWriter produces
        /// pretty-printed multi-line JSON. HTTP trailer values cannot contain raw CR/LF characters
        /// as they would break the HTTP framing — the Runtime API would see the first newline
        /// inside the JSON as the end of the trailer and treat the rest as malformed data,
        /// resulting in Runtime.TruncatedResponse instead of the actual error.
        /// </summary>
        internal async Task WriteTerminatorWithTrailersAsync(Exception exception, CancellationToken cancellationToken)
        {
            var exceptionInfo = ExceptionInfo.GetExceptionInfo(exception);
            var errorBodyJson = LambdaJsonExceptionWriter.WriteJson(exceptionInfo);
            var errorBodyBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(errorBodyJson));

            InternalLogger.GetDefaultLogger().LogInformation($"Writing trailing header {StreamingConstants.ErrorTypeTrailer} with error type {exceptionInfo.ErrorType}.");
            var trailers = new StringBuilder();
            trailers.Append("0\r\n"); // zero-length chunk (end of body)
            trailers.Append($"{StreamingConstants.ErrorTypeTrailer}: {exceptionInfo.ErrorType}\r\n");
            trailers.Append($"{StreamingConstants.ErrorBodyTrailer}: {errorBodyBase64}\r\n");
            trailers.Append("\r\n"); // end of trailers

            var trailerBytes = Encoding.UTF8.GetBytes(trailers.ToString());
            await _networkStream.WriteAsync(trailerBytes, cancellationToken);
        }

        /// <summary>
        /// Reads and discards the HTTP response from the Runtime API.
        /// We need to consume the response to properly close the connection,
        /// but we don't need to process it.
        /// </summary>
        internal async Task ReadAndDiscardResponseAsync(CancellationToken cancellationToken)
        {
            const string headerDelimiter = "\r\n\r\n";
            var buffer = new byte[4096];
            try
            {
                // Read until we get the full response. The Runtime API sends a short response.
                var totalRead = 0;
                var responseText = new StringBuilder();
                while (true)
                {
                    var bytesRead = await _networkStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                    if (bytesRead == 0)
                        break;

                    totalRead += bytesRead;
                    responseText.Append(Encoding.ASCII.GetString(buffer, 0, bytesRead));

                    // Check if we've received the complete response (ends with \r\n\r\n for headers,
                    // or we've read the content-length worth of body)
                    var text = responseText.ToString();
                    if (text.Contains(headerDelimiter))
                    {
                        // Find Content-Length to know if there's a body to read
                        var headerEnd = text.IndexOf(headerDelimiter, StringComparison.Ordinal);
                        var headers = text.Substring(0, headerEnd);

                        var contentLengthMatch = System.Text.RegularExpressions.Regex.Match(
                            headers, @"Content-Length:\s*(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                        if (contentLengthMatch.Success)
                        {
                            var contentLength = int.Parse(contentLengthMatch.Groups[1].Value, CultureInfo.InvariantCulture);
                            var bodyStart = headerEnd + 4; // skip \r\n\r\n
                            var bodyRead = text.Length - bodyStart;
                            if (bodyRead >= contentLength)
                                break;
                        }
                        else
                        {
                            // No Content-Length, assume response is complete after headers
                            break;
                        }
                    }

                    // 16KB is more than enough for the Runtime API response, so we can break here to avoid an infinite loop in case of malformed response
                    if (totalRead > 16384)
                        break; // Safety limit
                }
            }
            catch (Exception ex)
            {
                // Log but don't throw — the streaming response was already sent
                _logger.LogDebug($"Error reading Runtime API response: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _networkStream?.Dispose();
                _tcpClient?.Dispose();
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// A write-only Stream wrapper that writes data in HTTP/1.1 chunked transfer encoding format.
    /// Each write produces a chunk: {size in hex}\r\n{data}\r\n
    /// FlushAsync flushes the underlying network stream to ensure data is sent immediately.
    /// The chunked encoding terminator (0\r\n...\r\n) is NOT written by this class —
    /// it is handled by RawStreamingHttpClient to support trailing headers.
    /// </summary>
    internal class ChunkedStreamWriter : Stream
    {
        private readonly Stream _innerStream;

        public ChunkedStreamWriter(Stream innerStream)
        {
            _innerStream = innerStream ?? throw new ArgumentNullException(nameof(innerStream));
        }

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            WriteAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (count == 0) return;

            // Write chunk header: size in hex + \r\n
            var chunkHeader = Encoding.ASCII.GetBytes($"{count:X}\r\n");
            await _innerStream.WriteAsync(chunkHeader, 0, chunkHeader.Length, cancellationToken);

            // Write chunk data
            await _innerStream.WriteAsync(buffer, offset, count, cancellationToken);

            // Write chunk trailer: \r\n
            var crlf = Encoding.ASCII.GetBytes("\r\n");
            await _innerStream.WriteAsync(crlf, 0, crlf.Length, cancellationToken);
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (buffer.Length == 0) return;

            var chunkHeader = Encoding.ASCII.GetBytes($"{buffer.Length:X}\r\n");
            await _innerStream.WriteAsync(chunkHeader, cancellationToken);
            await _innerStream.WriteAsync(buffer, cancellationToken);
            await _innerStream.WriteAsync(Encoding.ASCII.GetBytes("\r\n"), cancellationToken);
        }

        public override void Flush() => _innerStream.Flush();

        public override Task FlushAsync(CancellationToken cancellationToken) =>
            _innerStream.FlushAsync(cancellationToken);

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
#endif
