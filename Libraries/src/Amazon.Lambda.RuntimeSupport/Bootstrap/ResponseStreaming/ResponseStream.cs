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
using System.Buffers;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Lambda.RuntimeSupport.Helpers;

namespace Amazon.Lambda.RuntimeSupport.Client.ResponseStreaming
{
    /// <summary>
    /// Represents the writable stream used by Lambda handlers to write response data for streaming invocations.
    /// </summary>
    internal class ResponseStream
    {
        private long _bytesWritten;
        private bool _isCompleted;
        private bool _hasError;
        private Exception _reportedError;
        private readonly object _lock = new object();

        // The live HTTP output stream, set by RawStreamingHttpClient when sending the streaming response.
        private Stream _httpOutputStream;
        private bool _disposedValue;

        // The wait time is a sanity timeout to avoid waiting indefinitely if SetHttpOutputStreamAsync is not called or takes too long to call.
        // Reality is that SetHttpOutputStreamAsync should be called very quickly after CreateStream, so this timeout is generous to avoid false positives but still protects against hanging indefinitely.
        private readonly static TimeSpan _httpStreamWaitTimeout = TimeSpan.FromSeconds(30);

        private readonly SemaphoreSlim _httpStreamReady = new SemaphoreSlim(0, 1);
        private readonly SemaphoreSlim _completionSignal = new SemaphoreSlim(0, 1);

        private static readonly byte[] PreludeDelimiter = new byte[8];

        /// <summary>
        /// The number of bytes written to the Lambda response stream so far.
        /// </summary>
        public long BytesWritten => _bytesWritten;

        /// <summary>
        /// Gets a value indicating whether an error has occurred.
        /// </summary>
        public bool HasError => _hasError;

        private readonly byte[] _prelude;


        private readonly InternalLogger _logger;


        internal Exception ReportedError => _reportedError;

        internal ResponseStream(byte[] prelude)
        {
            _logger = InternalLogger.GetDefaultLogger();
            _prelude = prelude;
        }

        /// <summary>
        /// Called by RawStreamingHttpClient to provide the HTTP output stream (a ChunkedStreamWriter).
        /// </summary>
        internal async Task SetHttpOutputStreamAsync(Stream httpOutputStream, CancellationToken cancellationToken = default)
        {
            _httpOutputStream = httpOutputStream;

            // Write the prelude BEFORE releasing _httpStreamReady. This prevents a race
            // where a handler WriteAsync that is already waiting on the semaphore could
            // sneak in and write body data before the prelude, causing intermittent
            // "Failed to parse prelude JSON" errors from API Gateway.
            //
            // Note: we intentionally do NOT check ThrowIfCompletedOrError() here.
            // SetHttpOutputStreamAsync is infrastructure setup called by RawStreamingHttpClient,
            // not a handler write. For fast-completing responses (e.g. Results.Json),
            // LambdaBootstrap may call MarkCompleted() before the TCP connection is established
            // and this method is called. The prelude still needs to be written to the wire
            // so the response is properly framed.
            if (_prelude?.Length > 0)
            {
                _logger.LogDebug("Writing prelude to HTTP stream.");

                var combinedLength = _prelude.Length + PreludeDelimiter.Length;
                var combined = ArrayPool<byte>.Shared.Rent(combinedLength);
                try
                {
                    Buffer.BlockCopy(_prelude, 0, combined, 0, _prelude.Length);
                    Buffer.BlockCopy(PreludeDelimiter, 0, combined, _prelude.Length, PreludeDelimiter.Length);

                    await _httpOutputStream.WriteAsync(combined, 0, combinedLength, cancellationToken);
                    await _httpOutputStream.FlushAsync(cancellationToken);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(combined);
                }
            }

            _httpStreamReady.Release();
        }

        /// <summary>
        /// Called by RawStreamingHttpClient to wait until the handler
        /// finishes writing (MarkCompleted or ReportError).
        /// </summary>
        internal async Task WaitForCompletionAsync(CancellationToken cancellationToken = default)
        {
            await _completionSignal.WaitAsync(cancellationToken);
        }

        internal async Task WriteAsync(byte[] buffer, CancellationToken cancellationToken = default)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            await WriteAsync(buffer, 0, buffer.Length, cancellationToken);
        }

        /// <summary>
        /// Asynchronously writes a portion of a byte array to the response stream.
        /// </summary>
        /// <param name="buffer">The byte array containing data to write.</param>
        /// <param name="offset">The zero-based byte offset in buffer at which to begin copying bytes.</param>
        /// <param name="count">The number of bytes to write.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the stream is already completed or an error has been reported.</exception>
        public async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken = default)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || offset > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0 || offset + count > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(count));

            // Wait for the HTTP stream to be ready (first write only blocks)
            await _httpStreamReady.WaitAsync(_httpStreamWaitTimeout, cancellationToken);
            try
            {
                _logger.LogDebug("Writing chunk to HTTP response stream.");

                lock (_lock)
                {
                    // Only throw on error, not on completed. For buffered ASP.NET Core responses
                    // (e.g. Results.Json), the pipeline completes and LambdaBootstrap calls
                    // MarkCompleted() before the pre-start buffer has been flushed to the wire.
                    // The buffered data still needs to be written even after MarkCompleted.
                    if (_hasError)
                        throw new InvalidOperationException("Cannot write to a stream after an error has been reported.");
                    _bytesWritten += count;
                }

                await _httpOutputStream.WriteAsync(buffer, offset, count, cancellationToken);
                await _httpOutputStream.FlushAsync(cancellationToken);
            }
            finally
            {
                // Re-release so subsequent writes don't block
                _httpStreamReady.Release();
            }
        }

        /// <summary>
        /// Reports an error that occurred during streaming.
        /// This will send error information via HTTP trailing headers.
        /// </summary>
        /// <param name="exception">The exception to report.</param>
        /// <exception cref="InvalidOperationException">Thrown if the stream is already completed or an error has already been reported.</exception>
        internal void ReportError(Exception exception)
        {
            if (exception == null)
                throw new ArgumentNullException(nameof(exception));

            lock (_lock)
            {
                if (_isCompleted)
                    throw new InvalidOperationException("Cannot report an error after the stream has been completed.");
                if (_hasError)
                    throw new InvalidOperationException("An error has already been reported for this stream.");

                _hasError = true;
                _reportedError = exception;
                _isCompleted = true;
            }
            // Signal completion so RawStreamingHttpClient can write error trailers and finish
            _completionSignal.Release();
        }

        internal void MarkCompleted()
        {
            bool shouldReleaseLock = false;
            lock (_lock)
            {
                // Release lock if not already completed, otherwise do nothing (idempotent)
                if (!_isCompleted)
                {
                    shouldReleaseLock = true;
                }
                _isCompleted = true;
            }

            if (shouldReleaseLock)
            {
                // Signal completion so RawStreamingHttpClient can write the final chunk and finish
                _completionSignal.Release();
            }
        }

        private void ThrowIfCompletedOrError()
        {
            if (_isCompleted)
                throw new InvalidOperationException("Cannot write to a completed stream.");
            if (_hasError)
                throw new InvalidOperationException("Cannot write to a stream after an error has been reported.");
        }

        /// <summary>
        /// Disposes the stream. After calling Dispose, no further writes or error reports should be made.
        /// </summary>
        /// <param name="disposing"></param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    try { _httpStreamReady.Release(); } catch (SemaphoreFullException) { /* Ignore if already released */ }
                    _httpStreamReady.Dispose();

                    try { _completionSignal.Release(); } catch (SemaphoreFullException) { /* Ignore if already released */ }
                    _completionSignal.Dispose();
                }

                _disposedValue = true;
            }
        }

        /// <summary>
        /// Dispose of the stream. After calling Dispose, no further writes or error reports should be made.
        /// </summary>
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
