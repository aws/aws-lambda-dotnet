// Test-only implementation of ILambdaResponseStream for unit tests.
// In production, DispatchProxy is used instead to avoid the type reference in assembly metadata.
using System;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Lambda.Core.ResponseStreaming;
using Amazon.Lambda.RuntimeSupport.Client.ResponseStreaming;

namespace Amazon.Lambda.RuntimeSupport.UnitTests
{
    internal class TestImplLambdaResponseStream : ILambdaResponseStream
    {
        private readonly ResponseStream _innerStream;

        internal TestImplLambdaResponseStream(ResponseStream innerStream)
        {
            _innerStream = innerStream;
        }

        public long BytesWritten => _innerStream.BytesWritten;
        public bool HasError => _innerStream.HasError;
        public void Dispose() => _innerStream.Dispose();
        public Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken = default)
            => _innerStream.WriteAsync(buffer, offset, count, cancellationToken);
    }
}
