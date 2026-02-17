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

namespace Amazon.Lambda.RuntimeSupport
{
    /// <summary>
    /// Internal implementation of IResponseStream with true streaming.
    /// Writes data directly to the HTTP output stream as chunked transfer encoding.
    /// </summary>
    internal class ResponseStream : IResponseStream
    {
        private static readonly byte[] CrlfBytes = Encoding.ASCII.GetBytes("\r\n");

        private readonly long _maxResponseSize;
        private long _bytesWritten;
        private bool _isCompleted;
        private bool _hasError;
        private Exception _reportedError;
        private readonly object _lock = new object();

        // The live HTTP output stream, set by StreamingHttpContent when SerializeToStreamAsync is called.
        private Stream _httpOutputStream;
        private readonly SemaphoreSlim _httpStreamReady = new SemaphoreSlim(0, 1);
        private readonly SemaphoreSlim _completionSignal = new SemaphoreSlim(0, 1);

        public long BytesWritten => _bytesWritten;
        public bool IsCompleted => _isCompleted;
        public bool HasError => _hasError;
        internal Exception ReportedError => _reportedError;

        public ResponseStream(long maxResponseSize)
        {
            _maxResponseSize = maxResponseSize;
        }

        /// <summary>
        /// Called by StreamingHttpContent.SerializeToStreamAsync to provide the HTTP output stream.
        /// </summary>
        internal void SetHttpOutputStream(Stream httpOutputStream)
        {
            _httpOutputStream = httpOutputStream;
            _httpStreamReady.Release();
        }

        /// <summary>
        /// Called by StreamingHttpContent.SerializeToStreamAsync to wait until the handler
        /// finishes writing (MarkCompleted or ReportErrorAsync).
        /// </summary>
        internal async Task WaitForCompletionAsync()
        {
            await _completionSignal.WaitAsync();
        }

        public async Task WriteAsync(byte[] buffer, CancellationToken cancellationToken = default)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            await WriteAsync(buffer, 0, buffer.Length, cancellationToken);
        }

        public async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken = default)
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

                    if (_bytesWritten + count > _maxResponseSize)
                    {
                        throw new InvalidOperationException(
                            $"Writing {count} bytes would exceed the maximum response size of {_maxResponseSize} bytes (20 MiB). " +
                            $"Current size: {_bytesWritten} bytes.");
                    }

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

        public async Task WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            // Convert to array and delegate — small overhead but keeps the API simple
            var array = buffer.ToArray();
            await WriteAsync(array, 0, array.Length, cancellationToken);
        }

        public Task ReportErrorAsync(Exception exception, CancellationToken cancellationToken = default)
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
            }

            // Signal completion so StreamingHttpContent can write error trailers and finish
            _completionSignal.Release();
            return Task.CompletedTask;
        }

        internal void MarkCompleted()
        {
            lock (_lock)
            {
                _isCompleted = true;
            }
            // Signal completion so StreamingHttpContent can write the final chunk and finish
            _completionSignal.Release();
        }

        private void ThrowIfCompletedOrError()
        {
            if (_isCompleted)
                throw new InvalidOperationException("Cannot write to a completed stream.");
            if (_hasError)
                throw new InvalidOperationException("Cannot write to a stream after an error has been reported.");
        }

        public void Dispose()
        {
            // Ensure completion is signaled if not already
            try { _completionSignal.Release(); } catch (SemaphoreFullException) { }
        }
    }
}
