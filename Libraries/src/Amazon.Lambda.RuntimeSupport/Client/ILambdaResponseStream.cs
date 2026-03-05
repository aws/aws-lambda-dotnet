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
using System.Threading;
using System.Threading.Tasks;

namespace Amazon.Lambda.RuntimeSupport
{
    /// <summary>
    /// Interface for writing streaming responses in AWS Lambda functions.
    /// Obtained by calling <see cref="LambdaResponseStreamFactory.CreateStream"/> within a handler.
    /// </summary>
    public interface ILambdaResponseStream : IDisposable
    {
        /// <summary>
        /// Asynchronously writes a byte array to the response stream.
        /// </summary>
        /// <param name="buffer">The byte array to write.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the stream is already completed or an error has been reported.</exception>
        Task WriteAsync(byte[] buffer, CancellationToken cancellationToken = default);

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
