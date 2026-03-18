// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.IO.Pipelines;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http.Features;

using Amazon.Lambda.Core.ResponseStreaming;
using Microsoft.Extensions.Logging;

namespace Amazon.Lambda.AspNetCoreServer.Internal
{
    /// <summary>
    /// An <see cref="IHttpResponseBodyFeature"/> implementation that supports Lambda response streaming.
    /// Uses a two-phase approach: bytes written before <see cref="StartAsync"/> are buffered in a
    /// <see cref="MemoryStream"/>; after <see cref="StartAsync"/> all writes go directly to the
    /// <see cref="LambdaResponseStream"/> obtained from the stream opener delegate.
    /// </summary>
    [RequiresPreviewFeatures(AbstractAspNetCoreFunction.ParameterizedPreviewMessage)]
    internal class StreamingResponseBodyFeature : IHttpResponseBodyFeature
    {
        private readonly ILogger _logger;
        private readonly IHttpResponseFeature _responseFeature;
        private readonly Func<Task<Stream>> _streamOpener;

        private Stream _lambdaStream;         // null until StartAsync completes
        private MemoryStream _preStartBuffer; // accumulates bytes written before StartAsync
        private bool _started;
        private PipeWriter _pipeWriter;       // lazily created; always wraps the live Stream

        /// <summary>
        /// Initializes a new instance of <see cref="StreamingResponseBodyFeature"/>.
        /// </summary>
        /// <param name="responseFeature">
        /// The <see cref="IHttpResponseFeature"/> for the current invocation. Used to fire
        /// <c>OnStarting</c> callbacks when <see cref="StartAsync"/> is called.
        /// </param>
        /// <param name="streamOpener">
        /// A delegate that, when invoked, builds the <see cref="HttpResponseStreamPrelude"/> from
        /// the response headers and calls <see cref="LambdaResponseStreamFactory.CreateHttpStream"/>
        /// to obtain the <see cref="LambdaResponseStream"/>.
        /// </param>
        public StreamingResponseBodyFeature(
            ILogger logger,
            IHttpResponseFeature responseFeature,
            Func<Task<Stream>> streamOpener)
        {
            _logger = logger;
            _responseFeature = responseFeature ?? throw new ArgumentNullException(nameof(responseFeature));
            _streamOpener = streamOpener ?? throw new ArgumentNullException(nameof(streamOpener));
        }

        /// <summary>
        /// Initializes a new instance without a logger (for use in tests).
        /// </summary>
        internal StreamingResponseBodyFeature(
            IHttpResponseFeature responseFeature,
            Func<Task<Stream>> streamOpener)
            : this(null, responseFeature, streamOpener) { }

        /// <inheritdoc />
        /// <remarks>
        /// Returns the <see cref="LambdaResponseStream"/> once <see cref="StartAsync"/> has been
        /// called; otherwise returns a lazy-initialized <see cref="MemoryStream"/> that buffers
        /// bytes until the stream is opened.
        /// </remarks>
        public Stream Stream => _lambdaStream ?? (_preStartBuffer ??= new MemoryStream());

        /// <inheritdoc />
        /// <remarks>
        /// Returns a <see cref="PipeWriter"/> that calls <see cref="StartAsync"/> on first
        /// flush/write so that the Lambda stream is opened (and the HTTP prelude is sent)
        /// as soon as the application first flushes, rather than waiting until the end.
        /// </remarks>
        public PipeWriter Writer => _pipeWriter ??= new StartOnFlushPipeWriter(this);

        /// <inheritdoc />
        /// <remarks>
        /// Fires all registered <c>OnStarting</c> callbacks, then calls the stream opener delegate
        /// to obtain the <see cref="LambdaResponseStream"/>, and finally flushes any bytes that
        /// were buffered before this method was called.
        /// </remarks>
        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            _logger?.LogInformation("Starting response streaming");

            if (_started) return;
            _started = true;

            // Fire OnStarting callbacks registered on the response feature.
            // InvokeFeatures (which implements IHttpResponseFeature) stores these in
            // ResponseStartingEvents, which is internal to this assembly.
            if (_responseFeature is InvokeFeatures invokeFeatures &&
                invokeFeatures.ResponseStartingEvents != null)
            {
                await invokeFeatures.ResponseStartingEvents.ExecuteAsync();
            }

            // Open the Lambda response stream (this writes the HTTP prelude).
            _lambdaStream = await _streamOpener();

            // Flush any bytes that were written before StartAsync was called.
            if (_preStartBuffer != null && _preStartBuffer.Length > 0)
            {
                _preStartBuffer.Position = 0;
                await _preStartBuffer.CopyToAsync(_lambdaStream, cancellationToken);
            }
        }

        /// <inheritdoc />
        public async Task CompleteAsync()
        {
            await StartAsync();

            if (_pipeWriter != null)
            {
                await _pipeWriter.FlushAsync();
            }
        }

        /// <inheritdoc />
        /// <remarks>No-op: the stream is already unbuffered once opened.</remarks>
        public void DisableBuffering()
        {
            // Intentional no-op per design: the Lambda response stream is already unbuffered.
        }

        /// <inheritdoc />
        /// <remarks>
        /// Calls <see cref="StartAsync"/> to ensure the stream is open, then reads the specified
        /// byte range from the file and writes it to the <see cref="LambdaResponseStream"/>.
        /// </remarks>
        public async Task SendFileAsync(
            string path,
            long offset,
            long? count,
            CancellationToken cancellationToken = default)
        {
            await StartAsync(cancellationToken);

            var fileInfo = new FileInfo(path);
            if (offset < 0 || offset > fileInfo.Length)
                throw new ArgumentOutOfRangeException(nameof(offset), offset, string.Empty);
            if (count.HasValue && (count.Value < 0 || count.Value > fileInfo.Length - offset))
                throw new ArgumentOutOfRangeException(nameof(count), count, string.Empty);

            cancellationToken.ThrowIfCancellationRequested();

            const int bufferSize = 1024 * 16;
            var fileStream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite,
                bufferSize: bufferSize,
                options: FileOptions.Asynchronous | FileOptions.SequentialScan);

            using (fileStream)
            {
                fileStream.Seek(offset, SeekOrigin.Begin);
                await Utilities.CopyToAsync(fileStream, _lambdaStream, count, bufferSize, cancellationToken);
            }
        }

        // -----------------------------------------------------------------------
        // StartOnFlushPipeWriter
        //
        // A PipeWriter wrapper that ensures StartAsync is called (opening the Lambda
        // stream and sending the HTTP prelude) the first time the application flushes
        // or completes the writer — not just at the very end of the request.
        //
        // The inner PipeWriter is created lazily against the *live* Stream property
        // so it always targets the correct underlying stream (Lambda stream after
        // StartAsync, pre-start buffer before).
        // -----------------------------------------------------------------------
        private sealed class StartOnFlushPipeWriter : PipeWriter
        {
            private readonly StreamingResponseBodyFeature _feature;
            private PipeWriter _inner;

            // The inner writer must be recreated after StartAsync because Stream
            // switches from _preStartBuffer to _lambdaStream at that point.
            private PipeWriter Inner => _inner ??= PipeWriter.Create(_feature.Stream);

            public StartOnFlushPipeWriter(StreamingResponseBodyFeature feature)
            {
                _feature = feature;
            }

            public override void Advance(int bytes) => Inner.Advance(bytes);

            public override Memory<byte> GetMemory(int sizeHint = 0) => Inner.GetMemory(sizeHint);

            public override Span<byte> GetSpan(int sizeHint = 0) => Inner.GetSpan(sizeHint);

            public override async ValueTask<FlushResult> FlushAsync(CancellationToken cancellationToken = default)
            {
                if (!_feature._started)
                {
                    // Flush buffered bytes into the pre-start buffer first, then open the stream.
                    await Inner.FlushAsync(cancellationToken);
                    // Recreate inner writer against the Lambda stream after StartAsync.
                    _inner = null;
                    await _feature.StartAsync(cancellationToken);
                    // Inner now wraps _lambdaStream; nothing extra to flush (StartAsync already
                    // copied the pre-start buffer across).
                    return new FlushResult(isCanceled: false, isCompleted: false);
                }

                return await Inner.FlushAsync(cancellationToken);
            }

            public override async ValueTask CompleteAsync(Exception exception = null)
            {
                if (!_feature._started)
                {
                    await Inner.FlushAsync();
                    _inner = null;
                    await _feature.StartAsync();
                }
                await Inner.CompleteAsync(exception);
            }

            // Complete (sync) — delegate
            public override void Complete(Exception exception = null) => Inner.Complete(exception);

            public override void CancelPendingFlush() => Inner.CancelPendingFlush();
        }
    }
}
