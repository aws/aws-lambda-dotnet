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
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Lambda.RuntimeSupport.Helpers;

namespace Amazon.Lambda.RuntimeSupport
{
    /// <summary>
    /// A write-only, non-seekable <see cref="Stream"/> subclass that streams response data
    /// to the Lambda Runtime API. Returned by <see cref="LambdaResponseStreamFactory.CreateStream"/>.
    /// </summary>
    public partial class LambdaResponseStream : Stream, ILambdaResponseStream
    {
        private static readonly byte[] CrlfBytes = Encoding.ASCII.GetBytes("\r\n");

        private long _bytesWritten;
        private bool _isCompleted;
        private bool _hasError;
        private Exception _reportedError;
        private readonly object _lock = new object();

        // The live HTTP output stream, set by StreamingHttpContent when SerializeToStreamAsync is called.
        private Stream _httpOutputStream;
        private readonly SemaphoreSlim _httpStreamReady = new SemaphoreSlim(0, 1);
        private readonly SemaphoreSlim _completionSignal = new SemaphoreSlim(0, 1);

        /// <summary>
        /// The number of bytes written to the Lambda response stream so far.
        /// </summary>
        public long BytesWritten => _bytesWritten;

        /// <summary>
        /// Gets a value indicating whether an error has occurred.
        /// </summary>
        public bool HasError => _hasError;

        private readonly byte[] _prelude;


        internal Exception ReportedError => _reportedError;

        internal LambdaResponseStream()
            : this(Array.Empty<byte>())
        {
        }

        internal LambdaResponseStream(byte[] prelude)
        {
            _prelude = prelude;
        }

        /// <summary>
        /// Called by StreamingHttpContent.SerializeToStreamAsync to provide the HTTP output stream.
        /// </summary>
        internal async Task SetHttpOutputStreamAsync(Stream httpOutputStream, CancellationToken cancellationToken = default)
        {
            _httpOutputStream = httpOutputStream;
            _httpStreamReady.Release();

            InternalLogger.GetDefaultLogger().LogDebug($"Writing prelude of {_prelude.Length} bytes to HTTP stream.");
            await WritePreludeAsync(cancellationToken);
        }

        private async Task WritePreludeAsync(CancellationToken cancellationToken = default)
        {
            if (_prelude?.Length > 0)
            {
                await _httpStreamReady.WaitAsync(cancellationToken);
                try
                {
                    lock (_lock)
                    {
                        ThrowIfCompletedOrError();
                    }

                    // Write prelude JSON chunk
                    var chunkSizeHex = _prelude.Length.ToString("X");
                    var chunkSizeBytes = Encoding.ASCII.GetBytes(chunkSizeHex);
                    await _httpOutputStream.WriteAsync(chunkSizeBytes, 0, chunkSizeBytes.Length, cancellationToken);
                    await _httpOutputStream.WriteAsync(CrlfBytes, 0, CrlfBytes.Length, cancellationToken);
                    await _httpOutputStream.WriteAsync(_prelude, 0, _prelude.Length, cancellationToken);
                    await _httpOutputStream.WriteAsync(CrlfBytes, 0, CrlfBytes.Length, cancellationToken);

                    // Write 8 null bytes delimiter chunk
                    var delimiterBytes = new byte[8];
                    chunkSizeHex = "8";
                    chunkSizeBytes = Encoding.ASCII.GetBytes(chunkSizeHex);
                    await _httpOutputStream.WriteAsync(chunkSizeBytes, 0, chunkSizeBytes.Length, cancellationToken);
                    await _httpOutputStream.WriteAsync(CrlfBytes, 0, CrlfBytes.Length, cancellationToken);
                    await _httpOutputStream.WriteAsync(delimiterBytes, 0, delimiterBytes.Length, cancellationToken);
                    await _httpOutputStream.WriteAsync(CrlfBytes, 0, CrlfBytes.Length, cancellationToken);

                    await _httpOutputStream.FlushAsync(cancellationToken);
                }
                finally
                {
                    _httpStreamReady.Release();
                }
            }
        }

        /// <summary>
        /// Called by StreamingHttpContent.SerializeToStreamAsync to wait until the handler
        /// finishes writing (MarkCompleted or ReportErrorAsync).
        /// </summary>
        internal async Task WaitForCompletionAsync(CancellationToken cancellationToken = default)
        {
            await _completionSignal.WaitAsync(cancellationToken);
        }

        /// <summary>
        /// Asynchronously writes a byte array to the response stream.
        /// </summary>
        /// <param name="buffer">The byte array to write.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the stream is already completed or an error has been reported.</exception>
        public async Task WriteAsync(byte[] buffer, CancellationToken cancellationToken = default)
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
        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken = default)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || offset > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0 || offset + count > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(count));

            // Wait for the HTTP stream to be ready (first write only blocks)
            await _httpStreamReady.WaitAsync(cancellationToken);
            try
            {
                lock (_lock)
                {
                    ThrowIfCompletedOrError();
                    _bytesWritten += count;
                }

                // Write chunk directly to the HTTP stream: size(hex) + CRLF + data + CRLF
                var chunkSizeHex = count.ToString("X");
                var chunkSizeBytes = Encoding.ASCII.GetBytes(chunkSizeHex);
                await _httpOutputStream.WriteAsync(chunkSizeBytes, 0, chunkSizeBytes.Length, cancellationToken);
                await _httpOutputStream.WriteAsync(CrlfBytes, 0, CrlfBytes.Length, cancellationToken);
                await _httpOutputStream.WriteAsync(buffer, offset, count, cancellationToken);
                await _httpOutputStream.WriteAsync(CrlfBytes, 0, CrlfBytes.Length, cancellationToken);
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
            // Signal completion so StreamingHttpContent can write error trailers and finish
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
                // Signal completion so StreamingHttpContent can write the final chunk and finish
                _completionSignal.Release();
            }
        }

        /// <summary>
        /// The resouces like the SemaphoreSlims are manually disposed by LambdaBootstrap after each invocation instead of relying on the
        /// Dipose pattern because we don't want the user's Lambda function to trigger Releasing and disposing the semaphores when
        /// invocation of the user's code ends.
        /// </summary>
        internal void ManualDispose()
        {
            try { _httpStreamReady.Release(); } catch (SemaphoreFullException) { /* Ignore if already released */ }
            _httpStreamReady.Dispose();
            try { _completionSignal.Release(); } catch (SemaphoreFullException) { /* Ignore if already released */ }
            _completionSignal.Dispose();
        }

        private void ThrowIfCompletedOrError()
        {
            if (_isCompleted)
                throw new InvalidOperationException("Cannot write to a completed stream.");
            if (_hasError)
                throw new InvalidOperationException("Cannot write to a stream after an error has been reported.");
        }
    }
}
