// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0
#if NET8_0_OR_GREATER
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Amazon.Lambda.Core.ResponseStreaming
{
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
}
#endif
