// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0
#if NET8_0_OR_GREATER

using System;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Lambda.Core.ResponseStreaming;
using Amazon.Lambda.RuntimeSupport.Client.ResponseStreaming;
#pragma warning disable CA2252
namespace Amazon.Lambda.RuntimeSupport
{
    /// <summary>
    /// This class is used to connect the <see cref="ResponseStream"/> created by <see cref="ResponseStreamFactory"/> to Amazon.Lambda.Core with it's public interfaces.
    /// The deployed Lambda function might be referencing an older version of Amazon.Lambda.Core that does not have the public interfaces for response streaming,
    /// so this class is used to avoid a direct dependency on Amazon.Lambda.Core in the rest of the response streaming implementation.
    /// <para>
    /// Any code referencing this class must wrap the code around a try/catch for <see cref="TypeLoadException"/> to allow for the case where the Lambda function
    /// is deployed with an older version of Amazon.Lambda.Core that does not have the response streaming interfaces.
    /// </para>
    /// </summary>
    internal class ResponseStreamLambdaCoreInitializerIsolated
    {
        /// <summary>
        /// Initalize Amazon.Lambda.Core with a factory method for creating <see cref="ILambdaResponseStream"/> that wraps the internal <see cref="ResponseStream"/> implementation.
        /// </summary>
        internal static void InitializeCore()
        {
#if !ANALYZER_UNIT_TESTS // This precompiler directive is used to avoid the unit tests from needing a dependency on Amazon.Lambda.Core.
            Func<byte[], ILambdaResponseStream> factory = (byte[] prelude) => new ImplLambdaResponseStream(ResponseStreamFactory.CreateStream(prelude));
            LambdaResponseStreamFactory.SetLambdaResponseStream(factory);
#endif
        }

        /// <summary>
        /// Implements the <see cref="ILambdaResponseStream"/> interface by wrapping a <see cref="ResponseStream"/>. This is used to connect the internal response streaming implementation to the public interfaces in Amazon.Lambda.Core.
        /// </summary>
        internal class ImplLambdaResponseStream : ILambdaResponseStream
        {
            private readonly ResponseStream _innerStream;

            internal ImplLambdaResponseStream(ResponseStream innerStream)
            {
                _innerStream = innerStream;
            }

            /// <inheritdoc/>
            public long BytesWritten => _innerStream.BytesWritten;

            /// <inheritdoc/>
            public bool HasError => _innerStream.HasError;

            /// <inheritdoc/>
            public void Dispose() => _innerStream.Dispose();

            /// <inheritdoc/>
            public Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken = default) => _innerStream.WriteAsync(buffer, offset, count);
        }
    }
}
#endif
