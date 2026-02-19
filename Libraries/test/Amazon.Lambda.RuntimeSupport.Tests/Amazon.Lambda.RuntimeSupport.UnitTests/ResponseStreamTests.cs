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
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Amazon.Lambda.RuntimeSupport.UnitTests
{
    public class ResponseStreamTests
    {
        /// <summary>
        /// Helper: creates a ResponseStream and wires up a MemoryStream as the HTTP output stream.
        /// Returns both so tests can inspect what was written.
        /// </summary>
        private static (LambdaResponseStream stream, MemoryStream httpOutput) CreateWiredStream()
        {
            var rs = new LambdaResponseStream();
            var output = new MemoryStream();
            rs.SetHttpOutputStream(output);
            return (rs, output);
        }

        // ---- Basic state tests ----

        [Fact]
        public void Constructor_InitializesStateCorrectly()
        {
            var stream = new LambdaResponseStream();

            Assert.Equal(0, stream.BytesWritten);
            Assert.False(stream.IsCompleted);
            Assert.False(stream.HasError);
            Assert.Null(stream.ReportedError);
        }

        // ---- Chunked encoding format (Property 9, Property 22) ----

        /// <summary>
        /// Property 9: Chunked Encoding Format — each chunk is hex-size + CRLF + data + CRLF.
        /// Property 22: CRLF Line Terminators — all line terminators are \r\n.
        /// Validates: Requirements 3.2, 10.1, 10.5
        /// </summary>
        [Theory]
        [InlineData(new byte[] { 1, 2, 3 }, "3")]           // 3 bytes → "3"
        [InlineData(new byte[] { 0xFF }, "1")]               // 1 byte → "1"
        [InlineData(new byte[0], "0")]                       // 0 bytes → "0"
        public async Task WriteAsync_WritesChunkedEncodingFormat(byte[] data, string expectedHexSize)
        {
            var (stream, httpOutput) = CreateWiredStream();

            await stream.WriteAsync(data);

            var written = httpOutput.ToArray();
            var expected = Encoding.ASCII.GetBytes(expectedHexSize + "\r\n")
                .Concat(data)
                .Concat(Encoding.ASCII.GetBytes("\r\n"))
                .ToArray();

            Assert.Equal(expected, written);
        }

        /// <summary>
        /// Property 9: Chunked Encoding Format — verify with offset/count overload.
        /// Validates: Requirements 3.2, 10.1
        /// </summary>
        [Fact]
        public async Task WriteAsync_WithOffset_WritesCorrectSliceAsChunk()
        {
            var (stream, httpOutput) = CreateWiredStream();
            var data = new byte[] { 0, 1, 2, 3, 0 };

            await stream.WriteAsync(data, 1, 3);

            var written = httpOutput.ToArray();
            // 3 bytes → hex "3", data is {1,2,3}
            var expected = Encoding.ASCII.GetBytes("3\r\n")
                .Concat(new byte[] { 1, 2, 3 })
                .Concat(Encoding.ASCII.GetBytes("\r\n"))
                .ToArray();

            Assert.Equal(expected, written);
        }

        // ---- Property 5: Written Data Appears in HTTP Response Immediately ----

        /// <summary>
        /// Property 5: Written Data Appears in HTTP Response Immediately —
        /// each WriteAsync call writes to the HTTP stream before returning.
        /// Validates: Requirements 3.2
        /// </summary>
        [Fact]
        public async Task WriteAsync_MultipleWrites_EachAppearsImmediately()
        {
            var (stream, httpOutput) = CreateWiredStream();

            await stream.WriteAsync(new byte[] { 0xAA });
            var afterFirst = httpOutput.ToArray().Length;
            Assert.True(afterFirst > 0, "First chunk should be on the HTTP stream immediately after WriteAsync returns");

            await stream.WriteAsync(new byte[] { 0xBB, 0xCC });
            var afterSecond = httpOutput.ToArray().Length;
            Assert.True(afterSecond > afterFirst, "Second chunk should appear on the HTTP stream immediately");

            Assert.Equal(3, stream.BytesWritten);
        }

        /// <summary>
        /// Property 5: Written Data Appears in HTTP Response Immediately —
        /// verify with a larger payload that hex size is multi-character.
        /// Validates: Requirements 3.2
        /// </summary>
        [Fact]
        public async Task WriteAsync_LargerPayload_HexSizeIsCorrect()
        {
            var (stream, httpOutput) = CreateWiredStream();
            var data = new byte[256]; // 0x100

            await stream.WriteAsync(data);

            var written = Encoding.ASCII.GetString(httpOutput.ToArray());
            Assert.StartsWith("100\r\n", written);
        }

        // ---- Semaphore coordination: _httpStreamReady blocks until SetHttpOutputStream ----

        /// <summary>
        /// Test that WriteAsync blocks until SetHttpOutputStream is called.
        /// Validates: Requirements 3.2, 10.1
        /// </summary>
        [Fact]
        public async Task WriteAsync_BlocksUntilSetHttpOutputStream()
        {
            var rs = new LambdaResponseStream();
            var httpOutput = new MemoryStream();
            var writeStarted = new ManualResetEventSlim(false);
            var writeCompleted = new ManualResetEventSlim(false);

            // Start a write on a background thread — it should block
            var writeTask = Task.Run(async () =>
            {
                writeStarted.Set();
                await rs.WriteAsync(new byte[] { 1, 2, 3 });
                writeCompleted.Set();
            });

            // Wait for the write to start, then verify it hasn't completed
            writeStarted.Wait(TimeSpan.FromSeconds(2));
            await Task.Delay(100); // give it a moment
            Assert.False(writeCompleted.IsSet, "WriteAsync should block until SetHttpOutputStream is called");

            // Now provide the HTTP stream — the write should complete
            rs.SetHttpOutputStream(httpOutput);
            await writeTask;

            Assert.True(writeCompleted.IsSet);
            Assert.True(httpOutput.ToArray().Length > 0);
        }

        // ---- Completion signaling: MarkCompleted releases _completionSignal ----

        /// <summary>
        /// Test that MarkCompleted releases the completion signal (WaitForCompletionAsync unblocks).
        /// Validates: Requirements 5.5, 8.3
        /// </summary>
        [Fact]
        public async Task MarkCompleted_ReleasesCompletionSignal()
        {
            var (stream, _) = CreateWiredStream();

            var waitTask = stream.WaitForCompletionAsync();
            Assert.False(waitTask.IsCompleted, "WaitForCompletionAsync should block before MarkCompleted");

            stream.MarkCompleted();

            // Should complete within a reasonable time
            var completed = await Task.WhenAny(waitTask, Task.Delay(TimeSpan.FromSeconds(2)));
            Assert.Same(waitTask, completed);
            Assert.True(stream.IsCompleted);
        }

        // ---- Completion signaling: ReportErrorAsync releases _completionSignal ----

        /// <summary>
        /// Test that ReportErrorAsync releases the completion signal.
        /// Validates: Requirements 5.5
        /// </summary>
        [Fact]
        public async Task ReportErrorAsync_ReleasesCompletionSignal()
        {
            var (stream, _) = CreateWiredStream();

            var waitTask = stream.WaitForCompletionAsync();
            Assert.False(waitTask.IsCompleted, "WaitForCompletionAsync should block before ReportErrorAsync");

            await stream.ReportErrorAsync(new Exception("test error"));

            var completed = await Task.WhenAny(waitTask, Task.Delay(TimeSpan.FromSeconds(2)));
            Assert.Same(waitTask, completed);
            Assert.True(stream.HasError);
        }

        // ---- Property 19: Writes After Completion Rejected ----

        /// <summary>
        /// Property 19: Writes After Completion Rejected — writes after MarkCompleted throw.
        /// Validates: Requirements 8.8
        /// </summary>
        [Fact]
        public async Task WriteAsync_AfterMarkCompleted_Throws()
        {
            var (stream, _) = CreateWiredStream();
            await stream.WriteAsync(new byte[] { 1 });
            stream.MarkCompleted();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => stream.WriteAsync(new byte[] { 2 }));
        }

        /// <summary>
        /// Property 19: Writes After Completion Rejected — writes after ReportErrorAsync throw.
        /// Validates: Requirements 8.8
        /// </summary>
        [Fact]
        public async Task WriteAsync_AfterReportError_Throws()
        {
            var (stream, _) = CreateWiredStream();
            await stream.WriteAsync(new byte[] { 1 });
            await stream.ReportErrorAsync(new Exception("test"));

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => stream.WriteAsync(new byte[] { 2 }));
        }

        // ---- Error handling tests ----

        [Fact]
        public async Task ReportErrorAsync_SetsErrorState()
        {
            var stream = new LambdaResponseStream();
            var exception = new InvalidOperationException("something broke");

            await stream.ReportErrorAsync(exception);

            Assert.True(stream.HasError);
            Assert.Same(exception, stream.ReportedError);
        }

        [Fact]
        public async Task ReportErrorAsync_AfterCompleted_Throws()
        {
            var stream = new LambdaResponseStream();
            stream.MarkCompleted();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => stream.ReportErrorAsync(new Exception("test")));
        }

        [Fact]
        public async Task ReportErrorAsync_CalledTwice_Throws()
        {
            var stream = new LambdaResponseStream();
            await stream.ReportErrorAsync(new Exception("first"));

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => stream.ReportErrorAsync(new Exception("second")));
        }

        [Fact]
        public void MarkCompleted_SetsCompletionState()
        {
            var stream = new LambdaResponseStream();

            stream.MarkCompleted();

            Assert.True(stream.IsCompleted);
        }

        // ---- Argument validation ----

        [Fact]
        public async Task WriteAsync_NullBuffer_ThrowsArgumentNull()
        {
            var (stream, _) = CreateWiredStream();

            await Assert.ThrowsAsync<ArgumentNullException>(() => stream.WriteAsync((byte[])null));
        }

        [Fact]
        public async Task WriteAsync_NullBufferWithOffset_ThrowsArgumentNull()
        {
            var (stream, _) = CreateWiredStream();

            await Assert.ThrowsAsync<ArgumentNullException>(() => stream.WriteAsync(null, 0, 0));
        }

        [Fact]
        public async Task ReportErrorAsync_NullException_ThrowsArgumentNull()
        {
            var stream = new LambdaResponseStream();

            await Assert.ThrowsAsync<ArgumentNullException>(() => stream.ReportErrorAsync(null));
        }

        // ---- Dispose signals completion ----

        [Fact]
        public async Task Dispose_ReleasesCompletionSignalIfNotAlreadyReleased()
        {
            var stream = new LambdaResponseStream();

            var waitTask = stream.WaitForCompletionAsync();
            Assert.False(waitTask.IsCompleted);

            stream.Dispose();

            var completed = await Task.WhenAny(waitTask, Task.Delay(TimeSpan.FromSeconds(2)));
            Assert.Same(waitTask, completed);
        }
    }
}
