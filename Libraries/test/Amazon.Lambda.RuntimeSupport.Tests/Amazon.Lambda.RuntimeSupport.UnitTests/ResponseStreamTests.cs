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
using Amazon.Lambda.RuntimeSupport.Client.ResponseStreaming;
using Xunit;

namespace Amazon.Lambda.RuntimeSupport.UnitTests
{
    public class ResponseStreamTests
    {
        /// <summary>
        /// Helper: creates a ResponseStream and wires up a MemoryStream as the HTTP output stream.
        /// Returns both so tests can inspect what was written.
        /// </summary>
        private static async Task<(ResponseStream stream, MemoryStream httpOutput)> CreateWiredStream()
        {
            var rs = new ResponseStream(Array.Empty<byte>());
            var output = new MemoryStream();
            await rs.SetHttpOutputStreamAsync(output);
            return (rs, output);
        }

        // ---- Basic state tests ----

        [Fact]
        public void Constructor_InitializesStateCorrectly()
        {
            var stream = new ResponseStream(Array.Empty<byte>());

            Assert.Equal(0, stream.BytesWritten);
            Assert.False(stream.HasError);
            Assert.Null(stream.ReportedError);
        }

        [Fact]
        public async Task WriteAsync_WithOffset_WritesCorrectSlice()
        {
            var (stream, httpOutput) = await CreateWiredStream();
            var data = new byte[] { 0, 1, 2, 3, 0 };

            await stream.WriteAsync(data, 1, 3);

            // Raw bytes {1,2,3} written directly — no chunked encoding
            var expected = new byte[] { 1, 2, 3 };
            Assert.Equal(expected, httpOutput.ToArray());
        }

        [Fact]
        public async Task WriteAsync_MultipleWrites_EachAppearsImmediately()
        {
            var (stream, httpOutput) = await CreateWiredStream();

            var data = new byte[] { 0xAA };
            await stream.WriteAsync(data, 0, data.Length);
            var afterFirst = httpOutput.ToArray().Length;
            Assert.True(afterFirst > 0, "First chunk should be on the HTTP stream immediately after WriteAsync returns");

            await stream.WriteAsync(new byte[] { 0xBB, 0xCC }, 0, 2);
            var afterSecond = httpOutput.ToArray().Length;
            Assert.True(afterSecond > afterFirst, "Second chunk should appear on the HTTP stream immediately");

            Assert.Equal(3, stream.BytesWritten);
        }

        [Fact]
        public async Task WriteAsync_BlocksUntilSetHttpOutputStream()
        {
            var rs = new ResponseStream(Array.Empty<byte>());
            var httpOutput = new MemoryStream();
            var writeStarted = new ManualResetEventSlim(false);
            var writeCompleted = new ManualResetEventSlim(false);

            // Start a write on a background thread — it should block
            var writeTask = Task.Run(async () =>
            {
                writeStarted.Set();
                await rs.WriteAsync(new byte[] { 1, 2, 3 }, 0, 3);
                writeCompleted.Set();
            });

            // Wait for the write to start, then verify it hasn't completed
            writeStarted.Wait(TimeSpan.FromSeconds(2));
            await Task.Delay(100); // give it a moment
            Assert.False(writeCompleted.IsSet, "WriteAsync should block until SetHttpOutputStream is called");

            // Now provide the HTTP stream — the write should complete
            await rs.SetHttpOutputStreamAsync(httpOutput);
            await writeTask;

            Assert.True(writeCompleted.IsSet);
            Assert.True(httpOutput.ToArray().Length > 0);
        }

        [Fact]
        public async Task MarkCompleted_ReleasesCompletionSignal()
        {
            var (stream, _) = await CreateWiredStream();

            var waitTask = stream.WaitForCompletionAsync();
            Assert.False(waitTask.IsCompleted, "WaitForCompletionAsync should block before MarkCompleted");

            stream.MarkCompleted();

            // Should complete within a reasonable time
            var completed = await Task.WhenAny(waitTask, Task.Delay(TimeSpan.FromSeconds(2)));
            Assert.Same(waitTask, completed);
        }

        [Fact]
        public async Task ReportErrorAsync_ReleasesCompletionSignal()
        {
            var (stream, _) = await CreateWiredStream();

            var waitTask = stream.WaitForCompletionAsync();
            Assert.False(waitTask.IsCompleted, "WaitForCompletionAsync should block before ReportErrorAsync");

            stream.ReportError(new Exception("test error"));

            var completed = await Task.WhenAny(waitTask, Task.Delay(TimeSpan.FromSeconds(2)));
            Assert.Same(waitTask, completed);
            Assert.True(stream.HasError);
        }

        [Fact]
        public async Task WriteAsync_AfterMarkCompleted_Throws()
        {
            var (stream, _) = await CreateWiredStream();
            await stream.WriteAsync(new byte[] { 1 }, 0, 1);
            stream.MarkCompleted();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => stream.WriteAsync(new byte[] { 2 }, 0, 1));
        }

        [Fact]
        public async Task WriteAsync_AfterReportError_Throws()
        {
            var (stream, _) = await CreateWiredStream();
            await stream.WriteAsync(new byte[] { 1 }, 0, 1);
            stream.ReportError(new Exception("test"));

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => stream.WriteAsync(new byte[] { 2 }, 0, 1));
        }

        [Fact]
        public async Task ReportErrorAsync_SetsErrorState()
        {
            var stream = new ResponseStream(Array.Empty<byte>());
            var exception = new InvalidOperationException("something broke");

            stream.ReportError(exception);

            Assert.True(stream.HasError);
            Assert.Same(exception, stream.ReportedError);
        }

        [Fact]
        public async Task ReportErrorAsync_AfterCompleted_Throws()
        {
            var stream = new ResponseStream(Array.Empty<byte>());
            stream.MarkCompleted();

            Assert.Throws<InvalidOperationException>(
                () => stream.ReportError(new Exception("test")));
        }

        [Fact]
        public async Task ReportErrorAsync_CalledTwice_Throws()
        {
            var stream = new ResponseStream(Array.Empty<byte>());
            stream.ReportError(new Exception("first"));

            Assert.Throws<InvalidOperationException>(
                () => stream.ReportError(new Exception("second")));
        }

        [Fact]
        public async Task WriteAsync_NullBuffer_ThrowsArgumentNull()
        {
            var (stream, _) = await CreateWiredStream();

            await Assert.ThrowsAsync<ArgumentNullException>(() => stream.WriteAsync((byte[])null, 0, 0));
        }

        [Fact]
        public async Task WriteAsync_NullBufferWithOffset_ThrowsArgumentNull()
        {
            var (stream, _) = await CreateWiredStream();

            await Assert.ThrowsAsync<ArgumentNullException>(() => stream.WriteAsync(null, 0, 0));
        }

        [Fact]
        public async Task ReportErrorAsync_NullException_ThrowsArgumentNull()
        {
            var stream = new ResponseStream(Array.Empty<byte>());

            Assert.Throws<ArgumentNullException>(() => stream.ReportError(null));
        }

        [Fact]
        public async Task Dispose_ReleasesCompletionSignalIfNotAlreadyReleased()
        {
            var stream = new ResponseStream(Array.Empty<byte>());

            var waitTask = stream.WaitForCompletionAsync();
            Assert.False(waitTask.IsCompleted);

            stream.Dispose();

            var completed = await Task.WhenAny(waitTask, Task.Delay(TimeSpan.FromSeconds(2)));
            Assert.Same(waitTask, completed);
        }

        [Fact]
        public async Task Dispose_CalledTwice_DoesNotThrow()
        {
            var stream = new ResponseStream(Array.Empty<byte>());
            stream.Dispose();
            // Second dispose should be a no-op
            stream.Dispose();
        }

        // ---- Prelude tests ----

        [Fact]
        public async Task SetHttpOutputStreamAsync_WithPrelude_WritesPreludeBeforeHandlerData()
        {
            var prelude = new byte[] { 0x01, 0x02, 0x03 };
            var rs = new ResponseStream(prelude);
            var output = new MemoryStream();

            await rs.SetHttpOutputStreamAsync(output);

            // Prelude bytes + 8-byte null delimiter should be written before any handler data
            var written = output.ToArray();
            Assert.True(written.Length >= prelude.Length + 8, "Prelude + delimiter should be written");
            Assert.Equal(prelude, written[..prelude.Length]);
            Assert.Equal(new byte[8], written[prelude.Length..(prelude.Length + 8)]);
        }

        [Fact]
        public async Task SetHttpOutputStreamAsync_WithEmptyPrelude_WritesNoPreludeBytes()
        {
            var rs = new ResponseStream(Array.Empty<byte>());
            var output = new MemoryStream();

            await rs.SetHttpOutputStreamAsync(output);

            // Empty prelude — nothing written yet (handler hasn't written anything)
            Assert.Empty(output.ToArray());
        }

        [Fact]
        public async Task SetHttpOutputStreamAsync_WithPrelude_HandlerDataAppendsAfterDelimiter()
        {
            var prelude = new byte[] { 0xAA, 0xBB };
            var rs = new ResponseStream(prelude);
            var output = new MemoryStream();

            await rs.SetHttpOutputStreamAsync(output);
            await rs.WriteAsync(new byte[] { 0xFF }, 0, 1);

            var written = output.ToArray();
            // Layout: [prelude][8 null bytes][handler data]
            int expectedMinLength = prelude.Length + 8 + 1;
            Assert.Equal(expectedMinLength, written.Length);
            Assert.Equal(new byte[] { 0xFF }, written[^1..]);
        }

        [Fact]
        public async Task SetHttpOutputStreamAsync_NullPrelude_WritesNoPreludeBytes()
        {
            var rs = new ResponseStream(null);
            var output = new MemoryStream();

            await rs.SetHttpOutputStreamAsync(output);

            Assert.Empty(output.ToArray());
        }

        // ---- MarkCompleted idempotency ----

        [Fact]
        public async Task MarkCompleted_CalledTwice_DoesNotThrowOrDoubleRelease()
        {
            var (stream, _) = await CreateWiredStream();

            stream.MarkCompleted();
            // Second call should be a no-op — semaphore should not be double-released
            stream.MarkCompleted();

            // WaitForCompletionAsync should complete exactly once without hanging
            var waitTask = stream.WaitForCompletionAsync();
            var completed = await Task.WhenAny(waitTask, Task.Delay(TimeSpan.FromSeconds(2)));
            Assert.Same(waitTask, completed);
        }

        [Fact]
        public async Task ReportError_ThenMarkCompleted_MarkCompletedIsNoOp()
        {
            var stream = new ResponseStream(Array.Empty<byte>());
            stream.ReportError(new Exception("error"));

            // MarkCompleted after ReportError should not throw and not double-release
            stream.MarkCompleted();

            // WaitForCompletionAsync should complete (released by ReportError)
            var waitTask = stream.WaitForCompletionAsync();
            var completed = await Task.WhenAny(waitTask, Task.Delay(TimeSpan.FromSeconds(2)));
            Assert.Same(waitTask, completed);
        }

        // ---- BytesWritten tracking ----

        [Fact]
        public async Task BytesWritten_TracksAcrossMultipleWrites()
        {
            var (stream, _) = await CreateWiredStream();

            await stream.WriteAsync(new byte[10], 0, 10);
            await stream.WriteAsync(new byte[5], 0, 5);

            Assert.Equal(15, stream.BytesWritten);
        }

        [Fact]
        public async Task BytesWritten_ReflectsOffsetAndCount()
        {
            var (stream, _) = await CreateWiredStream();

            await stream.WriteAsync(new byte[10], 2, 6); // only 6 bytes

            Assert.Equal(6, stream.BytesWritten);
        }
    }
}
