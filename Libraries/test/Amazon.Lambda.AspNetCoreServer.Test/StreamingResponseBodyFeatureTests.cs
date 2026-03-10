// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Runtime.Versioning;
using System.Threading.Tasks;

using Amazon.Lambda.AspNetCoreServer.Internal;
using Microsoft.AspNetCore.Http.Features;
using Xunit;

namespace Amazon.Lambda.AspNetCoreServer.Test
{
    [RequiresPreviewFeatures]
    public class StreamingResponseBodyFeatureTests
    {
        // Helper: creates a StreamingResponseBodyFeature backed by a MemoryStream stand-in.
        // Returns the feature and the MemoryStream that acts as the LambdaResponseStream.
        private static (StreamingResponseBodyFeature feature, MemoryStream lambdaStream, InvokeFeatures invokeFeatures)
            CreateFeature()
        {
            var lambdaStream = new MemoryStream();
            var invokeFeatures = new InvokeFeatures();
            var feature = new StreamingResponseBodyFeature(
                (IHttpResponseFeature)invokeFeatures,
                () => Task.FromResult<Stream>(lambdaStream));
            return (feature, lambdaStream, invokeFeatures);
        }

        [Fact]
        public async Task PreStartBytes_AreBuffered_ThenFlushedToLambdaStream_OnStartAsync()
        {
            var (feature, lambdaStream, _) = CreateFeature();

            // Write before StartAsync — should go to the pre-start buffer, NOT to lambdaStream yet.
            var preBytes = new byte[] { 1, 2, 3 };
            await feature.Stream.WriteAsync(preBytes, 0, preBytes.Length);

            Assert.Equal(0, lambdaStream.Length); // nothing in lambda stream yet

            // Now call StartAsync — buffered bytes should be flushed.
            await feature.StartAsync();

            lambdaStream.Position = 0;
            var result = lambdaStream.ToArray();
            Assert.Equal(preBytes, result);
        }

        [Fact]
        public async Task PostStartBytes_GoDirectlyToLambdaStream()
        {
            var (feature, lambdaStream, _) = CreateFeature();

            await feature.StartAsync();

            var postBytes = new byte[] { 10, 20, 30, 40 };
            await feature.Stream.WriteAsync(postBytes, 0, postBytes.Length);

            lambdaStream.Position = 0;
            var result = lambdaStream.ToArray();
            Assert.Equal(postBytes, result);
        }

        [Fact]
        public async Task OnStartingCallbacks_FireBeforeFirstByteReachesLambdaStream()
        {
            var lambdaStream = new SequenceTrackingStream();
            var invokeFeatures = new InvokeFeatures();
            var responseFeature = (IHttpResponseFeature)invokeFeatures;

            int callbackSequence = -1;
            int writeSequence = -1;
            int sequenceCounter = 0;

            // Register an OnStarting callback that records its sequence number.
            responseFeature.OnStarting(_ =>
            {
                callbackSequence = sequenceCounter++;
                return Task.CompletedTask;
            }, null);

            // The stream opener records the sequence when the stream is first written to.
            var feature = new StreamingResponseBodyFeature(
                responseFeature,
                () =>
                {
                    lambdaStream.OnFirstWrite = () => writeSequence = sequenceCounter++;
                    return Task.FromResult<Stream>(lambdaStream);
                });

            // Write a byte — this should trigger StartAsync internally (via Stream property
            // returning the pre-start buffer), but we explicitly call StartAsync here.
            await feature.StartAsync();

            // Write after start to trigger the first actual write to lambdaStream.
            var bytes = new byte[] { 0xFF };
            await feature.Stream.WriteAsync(bytes, 0, bytes.Length);

            Assert.True(callbackSequence >= 0, "OnStarting callback was never called");
            Assert.True(writeSequence >= 0, "No write reached the lambda stream");
            Assert.True(callbackSequence < writeSequence,
                $"OnStarting callback (seq={callbackSequence}) should fire before first write (seq={writeSequence})");
        }

        [Fact]
        public async Task DisableBuffering_IsNoOp_DoesNotThrow_DoesNotChangeBehavior()
        {
            var (feature, lambdaStream, _) = CreateFeature();

            // Should not throw.
            feature.DisableBuffering();

            // Behavior should be unchanged: bytes still flow through normally.
            await feature.StartAsync();
            var bytes = new byte[] { 7, 8, 9 };
            await feature.Stream.WriteAsync(bytes, 0, bytes.Length);

            lambdaStream.Position = 0;
            Assert.Equal(bytes, lambdaStream.ToArray());
        }

        [Fact]
        public void DisableBuffering_BeforeStart_DoesNotThrow()
        {
            var (feature, _, _) = CreateFeature();
            var ex = Record.Exception(() => feature.DisableBuffering());
            Assert.Null(ex);
        }

        [Fact]
        public async Task SendFileAsync_WritesFullFile_WhenNoOffsetOrCount()
        {
            var (feature, lambdaStream, _) = CreateFeature();

            var fileBytes = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
            var tempFile = Path.GetTempFileName();
            try
            {
                await File.WriteAllBytesAsync(tempFile, fileBytes);

                await feature.SendFileAsync(tempFile, 0, null);

                lambdaStream.Position = 0;
                Assert.Equal(fileBytes, lambdaStream.ToArray());
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Fact]
        public async Task SendFileAsync_WritesCorrectByteRange_WithOffsetAndCount()
        {
            var (feature, lambdaStream, _) = CreateFeature();

            var fileBytes = new byte[] { 10, 20, 30, 40, 50, 60, 70, 80 };
            var tempFile = Path.GetTempFileName();
            try
            {
                await File.WriteAllBytesAsync(tempFile, fileBytes);

                // Read bytes at offset=2, count=4 → should get [30, 40, 50, 60]
                await feature.SendFileAsync(tempFile, offset: 2, count: 4);

                lambdaStream.Position = 0;
                Assert.Equal(new byte[] { 30, 40, 50, 60 }, lambdaStream.ToArray());
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Fact]
        public async Task SendFileAsync_WithOffset_SkipsLeadingBytes()
        {
            var (feature, lambdaStream, _) = CreateFeature();

            var fileBytes = new byte[] { 1, 2, 3, 4, 5 };
            var tempFile = Path.GetTempFileName();
            try
            {
                await File.WriteAllBytesAsync(tempFile, fileBytes);

                // offset=3, count=null → should get [4, 5]
                await feature.SendFileAsync(tempFile, offset: 3, count: null);

                lambdaStream.Position = 0;
                Assert.Equal(new byte[] { 4, 5 }, lambdaStream.ToArray());
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Fact]
        public async Task CompleteAsync_CallsStartAsync_WhenNotYetStarted()
        {
            bool streamOpenerCalled = false;
            var lambdaStream = new MemoryStream();
            var invokeFeatures = new InvokeFeatures();

            var feature = new StreamingResponseBodyFeature(
                (IHttpResponseFeature)invokeFeatures,
                () =>
                {
                    streamOpenerCalled = true;
                    return Task.FromResult<Stream>(lambdaStream);
                });

            Assert.False(streamOpenerCalled);

            await feature.CompleteAsync();

            Assert.True(streamOpenerCalled, "CompleteAsync should have triggered StartAsync which calls the stream opener");
        }

        [Fact]
        public async Task CompleteAsync_WhenAlreadyStarted_DoesNotCallStreamOpenerAgain()
        {
            int streamOpenerCallCount = 0;
            var lambdaStream = new MemoryStream();
            var invokeFeatures = new InvokeFeatures();

            var feature = new StreamingResponseBodyFeature(
                (IHttpResponseFeature)invokeFeatures,
                () =>
                {
                    streamOpenerCallCount++;
                    return Task.FromResult<Stream>(lambdaStream);
                });

            await feature.StartAsync();
            await feature.CompleteAsync();

            Assert.Equal(1, streamOpenerCallCount);
        }

        [Fact]
        public async Task PreAndPostStartBytes_AreForwardedInOrder()
        {
            var (feature, lambdaStream, _) = CreateFeature();

            var preBytes = new byte[] { 1, 2, 3 };
            var postBytes = new byte[] { 4, 5, 6 };

            await feature.Stream.WriteAsync(preBytes, 0, preBytes.Length);
            await feature.StartAsync();
            await feature.Stream.WriteAsync(postBytes, 0, postBytes.Length);

            lambdaStream.Position = 0;
            var result = lambdaStream.ToArray();
            Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6 }, result);
        }

        private class SequenceTrackingStream : MemoryStream
        {
            public Action OnFirstWrite { get; set; }
            private bool _firstWriteDone;

            public override void Write(byte[] buffer, int offset, int count)
            {
                FireFirstWrite();
                base.Write(buffer, offset, count);
            }

            public override Task WriteAsync(byte[] buffer, int offset, int count,
                System.Threading.CancellationToken cancellationToken)
            {
                FireFirstWrite();
                return base.WriteAsync(buffer, offset, count, cancellationToken);
            }

            private void FireFirstWrite()
            {
                if (!_firstWriteDone)
                {
                    _firstWriteDone = true;
                    OnFirstWrite?.Invoke();
                }
            }
        }
    }
}
