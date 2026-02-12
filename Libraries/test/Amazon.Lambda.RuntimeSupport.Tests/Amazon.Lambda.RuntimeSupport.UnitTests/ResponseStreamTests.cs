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
using System.Threading.Tasks;
using Xunit;

namespace Amazon.Lambda.RuntimeSupport.UnitTests
{
    public class ResponseStreamTests
    {
        private const long MaxResponseSize = 20 * 1024 * 1024; // 20 MiB

        [Fact]
        public void Constructor_InitializesStateCorrectly()
        {
            var stream = new ResponseStream(MaxResponseSize);

            Assert.Equal(0, stream.BytesWritten);
            Assert.False(stream.IsCompleted);
            Assert.False(stream.HasError);
            Assert.Empty(stream.Chunks);
            Assert.Null(stream.ReportedError);
        }

        [Fact]
        public async Task WriteAsync_ByteArray_BuffersDataCorrectly()
        {
            var stream = new ResponseStream(MaxResponseSize);
            var data = new byte[] { 1, 2, 3, 4, 5 };

            await stream.WriteAsync(data);

            Assert.Equal(5, stream.BytesWritten);
            Assert.Single(stream.Chunks);
            Assert.Equal(data, stream.Chunks[0]);
        }

        [Fact]
        public async Task WriteAsync_WithOffset_BuffersCorrectSlice()
        {
            var stream = new ResponseStream(MaxResponseSize);
            var data = new byte[] { 0, 1, 2, 3, 0 };

            await stream.WriteAsync(data, 1, 3);

            Assert.Equal(3, stream.BytesWritten);
            Assert.Equal(new byte[] { 1, 2, 3 }, stream.Chunks[0]);
        }

        [Fact]
        public async Task WriteAsync_ReadOnlyMemory_BuffersDataCorrectly()
        {
            var stream = new ResponseStream(MaxResponseSize);
            var data = new ReadOnlyMemory<byte>(new byte[] { 10, 20, 30 });

            await stream.WriteAsync(data);

            Assert.Equal(3, stream.BytesWritten);
            Assert.Equal(new byte[] { 10, 20, 30 }, stream.Chunks[0]);
        }

        [Fact]
        public async Task WriteAsync_MultipleWrites_AccumulatesBytesWritten()
        {
            var stream = new ResponseStream(MaxResponseSize);

            await stream.WriteAsync(new byte[100]);
            await stream.WriteAsync(new byte[200]);
            await stream.WriteAsync(new byte[300]);

            Assert.Equal(600, stream.BytesWritten);
            Assert.Equal(3, stream.Chunks.Count);
        }

        [Fact]
        public async Task WriteAsync_CopiesData_AvoidingBufferReuseIssues()
        {
            var stream = new ResponseStream(MaxResponseSize);
            var buffer = new byte[] { 1, 2, 3 };

            await stream.WriteAsync(buffer);
            buffer[0] = 99; // mutate original

            Assert.Equal(1, stream.Chunks[0][0]); // chunk should be unaffected
        }

        /// <summary>
        /// Property 6: Size Limit Enforcement - Writing beyond 20 MiB throws InvalidOperationException.
        /// Validates: Requirements 3.6, 3.7
        /// </summary>
        [Theory]
        [InlineData(21 * 1024 * 1024)] // Single write exceeding limit
        public async Task SizeLimit_SingleWriteExceedingLimit_Throws(int writeSize)
        {
            var stream = new ResponseStream(MaxResponseSize);
            var data = new byte[writeSize];

            await Assert.ThrowsAsync<InvalidOperationException>(() => stream.WriteAsync(data));
        }

        /// <summary>
        /// Property 6: Size Limit Enforcement - Multiple writes exceeding 20 MiB throws.
        /// Validates: Requirements 3.6, 3.7
        /// </summary>
        [Fact]
        public async Task SizeLimit_MultipleWritesExceedingLimit_Throws()
        {
            var stream = new ResponseStream(MaxResponseSize);

            await stream.WriteAsync(new byte[10 * 1024 * 1024]);
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => stream.WriteAsync(new byte[11 * 1024 * 1024]));
        }

        [Fact]
        public async Task SizeLimit_ExactlyAtLimit_Succeeds()
        {
            var stream = new ResponseStream(MaxResponseSize);
            var data = new byte[20 * 1024 * 1024];

            await stream.WriteAsync(data);

            Assert.Equal(MaxResponseSize, stream.BytesWritten);
        }

        /// <summary>
        /// Property 19: Writes After Completion Rejected - Writes after completion throw InvalidOperationException.
        /// Validates: Requirements 8.8
        /// </summary>
        [Fact]
        public async Task WriteAsync_AfterMarkCompleted_Throws()
        {
            var stream = new ResponseStream(MaxResponseSize);
            await stream.WriteAsync(new byte[] { 1 });
            stream.MarkCompleted();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => stream.WriteAsync(new byte[] { 2 }));
        }

        [Fact]
        public async Task WriteAsync_AfterReportError_Throws()
        {
            var stream = new ResponseStream(MaxResponseSize);
            await stream.WriteAsync(new byte[] { 1 });
            await stream.ReportErrorAsync(new Exception("test"));

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => stream.WriteAsync(new byte[] { 2 }));
        }

        // --- Error handling tests (2.6) ---

        [Fact]
        public async Task ReportErrorAsync_SetsErrorState()
        {
            var stream = new ResponseStream(MaxResponseSize);
            var exception = new InvalidOperationException("something broke");

            await stream.ReportErrorAsync(exception);

            Assert.True(stream.HasError);
            Assert.Same(exception, stream.ReportedError);
        }

        [Fact]
        public async Task ReportErrorAsync_AfterCompleted_Throws()
        {
            var stream = new ResponseStream(MaxResponseSize);
            stream.MarkCompleted();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => stream.ReportErrorAsync(new Exception("test")));
        }

        [Fact]
        public async Task ReportErrorAsync_CalledTwice_Throws()
        {
            var stream = new ResponseStream(MaxResponseSize);
            await stream.ReportErrorAsync(new Exception("first"));

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => stream.ReportErrorAsync(new Exception("second")));
        }

        [Fact]
        public void MarkCompleted_SetsCompletionState()
        {
            var stream = new ResponseStream(MaxResponseSize);

            stream.MarkCompleted();

            Assert.True(stream.IsCompleted);
        }
    }
}
