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
using System.Threading;
using System.Threading.Tasks;

namespace Amazon.Lambda.RuntimeSupport
{
    /// <summary>
    /// A write-only, non-seekable <see cref="Stream"/> subclass that streams response data
    /// to the Lambda Runtime API. Returned by <see cref="LambdaResponseStreamFactory.CreateStream"/>.
    /// Integrates with standard .NET stream consumers such as <see cref="System.IO.StreamWriter"/>.
    /// </summary>
    public partial class LambdaResponseStream : Stream, ILambdaResponseStream
    {
        // ── System.IO.Stream — capabilities ─────────────────────────────────

        /// <summary>Gets a value indicating whether the stream supports reading. Always <c>false</c>.</summary>
        public override bool CanRead => false;

        /// <summary>Gets a value indicating whether the stream supports seeking. Always <c>false</c>.</summary>
        public override bool CanSeek => false;

        /// <summary>Gets a value indicating whether the stream supports writing. Always <c>true</c>.</summary>
        public override bool CanWrite => true;

        // ── System.IO.Stream — Length / Position ────────────────────────────

        /// <summary>
        /// Gets the total number of bytes written to the stream so far.
        /// Equivalent to <see cref="BytesWritten"/>.
        /// </summary>
        public override long Length => BytesWritten;

        /// <summary>
        /// Getting or setting the position is not supported.
        /// </summary>
        /// <exception cref="NotSupportedException">Always thrown.</exception>
        public override long Position
        {
            get => throw new NotSupportedException("LambdaResponseStream does not support seeking.");
            set => throw new NotSupportedException("LambdaResponseStream does not support seeking.");
        }

        // ── System.IO.Stream — seek / read (not supported) ──────────────────

        /// <summary>Not supported.</summary>
        /// <exception cref="NotImplementedException">Always thrown.</exception>
        public override long Seek(long offset, SeekOrigin origin)
            => throw new NotImplementedException("LambdaResponseStream does not support seeking.");

        /// <summary>Not supported.</summary>
        /// <exception cref="NotImplementedException">Always thrown.</exception>
        public override int Read(byte[] buffer, int offset, int count)
            => throw new NotImplementedException("LambdaResponseStream does not support reading.");

        /// <summary>Not supported.</summary>
        /// <exception cref="NotImplementedException">Always thrown.</exception>
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => throw new NotImplementedException("LambdaResponseStream does not support reading.");

        // ── System.IO.Stream — write ─────────────────────────────────────────

        /// <summary>
        /// Writes a sequence of bytes to the stream. Delegates to the async path synchronously.
        /// Prefer <see cref="WriteAsync(byte[], int, int, CancellationToken)"/> to avoid blocking.
        /// </summary>
        public override void Write(byte[] buffer, int offset, int count)
            => WriteAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();

        // ── System.IO.Stream — flush / set length ────────────────────────────

        /// <summary>
        /// Flush is a no-op; data is sent to the Runtime API immediately on each write.
        /// </summary>
        public override void Flush() { }

        /// <summary>Not supported.</summary>
        /// <exception cref="NotSupportedException">Always thrown.</exception>
        public override void SetLength(long value)
            => throw new NotSupportedException("LambdaResponseStream does not support SetLength.");
    }
}
