// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0
#if NET8_0_OR_GREATER

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Amazon.Lambda.Core.ResponseStreaming
{
    internal class ImplLambdaResponseStream : ILambdaResponseStream
    {
        private readonly Delegates _innerDelegates;

        internal ImplLambdaResponseStream(Delegates innerDelegates)
        {
            _innerDelegates = innerDelegates;
        }

        /// <inheritdoc/>
        public long BytesWritten => _innerDelegates.BytesWritten();

        /// <inheritdoc/>
        public bool HasError => _innerDelegates.HasError();

        /// <inheritdoc/>
        public void Dispose() => _innerDelegates.Dispose();

        /// <inheritdoc/>
        public Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken = default) => _innerDelegates.WriteAsync(buffer, offset, count, cancellationToken);

        internal class Delegates
        {
            internal Func<byte[], int, int, CancellationToken, Task> WriteAsync { get; set; }
            internal Func<long> BytesWritten { get; set; }
            internal Func<bool> HasError { get; set; }
            internal Action Dispose { get; set; }
        }
    }
}
#endif
