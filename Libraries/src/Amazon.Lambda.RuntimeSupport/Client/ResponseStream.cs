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
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Amazon.Lambda.RuntimeSupport
{
    /// <summary>
    /// Internal implementation of IResponseStream.
    /// Buffers written data as chunks for HTTP chunked transfer encoding.
    /// </summary>
    internal class ResponseStream : IResponseStream
    {
        private readonly long _maxResponseSize;
        private readonly List<byte[]> _chunks;
        private long _bytesWritten;
        private bool _isCompleted;
        private bool _hasError;
        private Exception _reportedError;
        private readonly object _lock = new object();

        public long BytesWritten => _bytesWritten;
        public bool IsCompleted => _isCompleted;
        public bool HasError => _hasError;

        internal IReadOnlyList<byte[]> Chunks
        {
            get
            {
                lock (_lock)
                {
                    return _chunks.ToList();
                }
            }
        }

        internal Exception ReportedError => _reportedError;

        public ResponseStream(long maxResponseSize)
        {
            _maxResponseSize = maxResponseSize;
            _chunks = new List<byte[]>();
            _bytesWritten = 0;
            _isCompleted = false;
            _hasError = false;
        }

        public Task WriteAsync(byte[] buffer, CancellationToken cancellationToken = default)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            return WriteAsync(buffer, 0, buffer.Length, cancellationToken);
        }

        public Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken = default)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || offset > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0 || offset + count > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(count));

            lock (_lock)
            {
                ThrowIfCompletedOrError();

                if (_bytesWritten + count > _maxResponseSize)
                {
                    throw new InvalidOperationException(
                        $"Writing {count} bytes would exceed the maximum response size of {_maxResponseSize} bytes (20 MiB). " +
                        $"Current size: {_bytesWritten} bytes.");
                }

                var chunk = new byte[count];
                Array.Copy(buffer, offset, chunk, 0, count);
                _chunks.Add(chunk);
                _bytesWritten += count;
            }

            return Task.CompletedTask;
        }

        public Task WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                ThrowIfCompletedOrError();

                if (_bytesWritten + buffer.Length > _maxResponseSize)
                {
                    throw new InvalidOperationException(
                        $"Writing {buffer.Length} bytes would exceed the maximum response size of {_maxResponseSize} bytes (20 MiB). " +
                        $"Current size: {_bytesWritten} bytes.");
                }

                var chunk = buffer.ToArray();
                _chunks.Add(chunk);
                _bytesWritten += buffer.Length;
            }

            return Task.CompletedTask;
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

            return Task.CompletedTask;
        }

        internal void MarkCompleted()
        {
            lock (_lock)
            {
                _isCompleted = true;
            }
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
            // Nothing to dispose - all data is in managed memory
        }
    }
}
