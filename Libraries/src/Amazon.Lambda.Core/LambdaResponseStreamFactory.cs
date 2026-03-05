// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0
#if NET8_0_OR_GREATER
using System;
using System.IO;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;

namespace Amazon.Lambda.Core
{
    /// <summary>
    /// Factory to create Lambda response streams for writing streaming responses in AWS Lambda functions. The created streams are write-only and non-seekable.
    /// </summary>
    [RequiresPreviewFeatures(LambdaResponseStreamFactory.ParameterizedPreviewMessage)]
    public class LambdaResponseStreamFactory
    {
        internal const string ParameterizedPreviewMessage =
            "Response streaming is in preview till a new version of .NET Lambda runtime client that supports response streaming " +
            "has been deployed to the .NET Lambda managed runtime. Till deployment has been made the feature can be used by deploying as an " +
            "executable including the latest version of Amazon.Lambda.RuntimeSupport and setting the \"EnablePreviewFeatures\" in the Lambda " +
            "project file to \"true\"";

        private static Func<byte[], ILambdaResponseStream> _streamFactory;

        internal static void SetLambdaResponseStream(Func<byte[], ILambdaResponseStream> streamFactory)
        {
            _streamFactory = streamFactory ?? throw new ArgumentNullException(nameof(streamFactory));
        }

        /// <summary>
        /// Creates a <see cref="Stream"/> that can be used to write streaming responses back to callers of the Lambda function. Once
        /// A Lambda function creates a response stream all output must be returned by writing to the stream; the Lambda function's handler
        /// return value will be ignored. The stream is write-only and non-seekable.
        /// </summary>
        /// <returns></returns>
        public static Stream CreateStream()
        {
            var runtimeResponseStream = _streamFactory(Array.Empty<byte>());
            return new LambdaResponseStream(runtimeResponseStream);
        }
    }

    /// <summary>
    /// Interface for writing streaming responses in AWS Lambda functions.
    /// Obtained by calling <see cref="LambdaResponseStreamFactory.CreateStream"/> within a handler.
    /// </summary>
    internal interface ILambdaResponseStream : IDisposable
    {
        /// <summary>
        /// Asynchronously writes a portion of a byte array to the response stream.
        /// </summary>
        /// <param name="buffer">The byte array containing data to write.</param>
        /// <param name="offset">The zero-based byte offset in buffer at which to begin copying bytes.</param>
        /// <param name="count">The number of bytes to write.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the stream is already completed or an error has been reported.</exception>
        Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken = default);


        /// <summary>
        /// Gets the total number of bytes written to the stream so far.
        /// </summary>
        long BytesWritten { get; }


        /// <summary>
        /// Gets whether an error has been reported.
        /// </summary>
        bool HasError { get; }
    }

    /// <summary>
    /// A write-only, non-seekable <see cref="Stream"/> subclass that streams response data
    /// to the Lambda Runtime API. Returned by <see cref="LambdaResponseStreamFactory.CreateStream"/>.
    /// Integrates with standard .NET stream consumers such as <see cref="System.IO.StreamWriter"/>.
    /// </summary>
    [RequiresPreviewFeatures(LambdaResponseStreamFactory.ParameterizedPreviewMessage)]
    public class LambdaResponseStream : Stream
    {
        private readonly ILambdaResponseStream _responseStream;

        internal LambdaResponseStream(ILambdaResponseStream responseStream)
        {
            _responseStream = responseStream;
        }

        /// <summary>
        /// The number of bytes written to the Lambda response stream so far.
        /// </summary>
        public long BytesWritten => _responseStream.BytesWritten;

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
            await _responseStream.WriteAsync(buffer, offset, count, cancellationToken);
        }

        #region Noop Overrides

        /// <summary>Gets a value indicating whether the stream supports reading. Always <c>false</c>.</summary>
        public override bool CanRead => false;

        /// <summary>Gets a value indicating whether the stream supports seeking. Always <c>false</c>.</summary>
        public override bool CanSeek => false;

        /// <summary>Gets a value indicating whether the stream supports writing. Always <c>true</c>.</summary>
        public override bool CanWrite => true;

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

        /// <summary>
        /// Writes a sequence of bytes to the stream. Delegates to the async path synchronously.
        /// Prefer <see cref="WriteAsync(byte[], int, int, CancellationToken)"/> to avoid blocking.
        /// </summary>
        public override void Write(byte[] buffer, int offset, int count)
            => WriteAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();

        /// <summary>
        /// Flush is a no-op; data is sent to the Runtime API immediately on each write.
        /// </summary>
        public override void Flush() { }

        /// <summary>Not supported.</summary>
        /// <exception cref="NotSupportedException">Always thrown.</exception>
        public override void SetLength(long value)
            => throw new NotSupportedException("LambdaResponseStream does not support SetLength.");
        #endregion
    }
}
#endif
