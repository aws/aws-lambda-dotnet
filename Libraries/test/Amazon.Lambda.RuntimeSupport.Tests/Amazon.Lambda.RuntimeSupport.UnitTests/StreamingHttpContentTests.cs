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
using System.Text;
using System.Threading.Tasks;
using Amazon.Lambda.RuntimeSupport.Client.ResponseStreaming;
using Xunit;

namespace Amazon.Lambda.RuntimeSupport.UnitTests
{
    public class StreamingHttpContentTests
    {
        private const long MaxResponseSize = 20 * 1024 * 1024;

        /// <summary>
        /// Helper: runs SerializeToStreamAsync concurrently with handler actions.
        /// The handlerAction receives the ResponseStream and should write data then signal completion.
        /// Returns the bytes written to the HTTP output stream.
        /// </summary>
        private async Task<byte[]> SerializeWithConcurrentHandler(
            ResponseStream responseStream,
            Func<ResponseStream, Task> handlerAction)
        {
            var content = new StreamingHttpContent(responseStream);
            var outputStream = new MemoryStream();

            // Start serialization on a background task (it will call SetHttpOutputStream and wait)
            var serializeTask = Task.Run(() => content.CopyToAsync(outputStream));

            // Give SerializeToStreamAsync a moment to start and call SetHttpOutputStream
            await Task.Delay(50);

            // Run the handler action (writes data, signals completion)
            await handlerAction(responseStream);

            // Wait for serialization to complete
            await serializeTask;

            return outputStream.ToArray();
        }

        // ---- SerializeToStreamAsync hands off HTTP stream ----

        /// <summary>
        /// Test that SerializeToStreamAsync calls SetHttpOutputStream on the ResponseStream,
        /// enabling writes to flow through.
        /// Validates: Requirements 4.3, 10.1
        /// </summary>
        [Fact]
        public async Task SerializeToStreamAsync_HandsOffHttpStream_WritesFlowThrough()
        {
            var rs = new ResponseStream(Array.Empty<byte>());

            var output = await SerializeWithConcurrentHandler(rs, async stream =>
            {
                await stream.WriteAsync(new byte[] { 0xAA, 0xBB });
                stream.MarkCompleted();
            });

            var outputStr = Encoding.ASCII.GetString(output);
            // Should contain the chunk data written by the handler
            Assert.Contains("2\r\n", outputStr);
            Assert.True(output.Length > 0);
        }

        /// <summary>
        /// Test that SerializeToStreamAsync blocks until MarkCompleted is called.
        /// Validates: Requirements 4.3
        /// </summary>
        [Fact]
        public async Task SerializeToStreamAsync_BlocksUntilMarkCompleted()
        {
            var rs = new ResponseStream(Array.Empty<byte>());
            var content = new StreamingHttpContent(rs);
            var outputStream = new MemoryStream();

            var serializeTask = Task.Run(() => content.CopyToAsync(outputStream));
            await Task.Delay(50);

            // Serialization should still be running (waiting for completion)
            Assert.False(serializeTask.IsCompleted, "SerializeToStreamAsync should block until completion is signaled");

            // Now signal completion
            rs.MarkCompleted();
            await serializeTask;

            Assert.True(serializeTask.IsCompleted);
        }

        /// <summary>
        /// Test that SerializeToStreamAsync blocks until ReportErrorAsync is called.
        /// Validates: Requirements 4.3, 5.1
        /// </summary>
        [Fact]
        public async Task SerializeToStreamAsync_BlocksUntilReportErrorAsync()
        {
            var rs = new ResponseStream(Array.Empty<byte>());
            var content = new StreamingHttpContent(rs);
            var outputStream = new MemoryStream();

            var serializeTask = Task.Run(() => content.CopyToAsync(outputStream));
            await Task.Delay(50);

            Assert.False(serializeTask.IsCompleted, "SerializeToStreamAsync should block until error is reported");

            rs.ReportError(new Exception("test error"));
            await serializeTask;

            Assert.True(serializeTask.IsCompleted);
        }

        // ---- Property 20: Final Chunk Termination ----

        /// <summary>
        /// Property 20: Final Chunk Termination — final chunk "0\r\n" is written after completion.
        /// Validates: Requirements 4.3, 10.2, 10.3
        /// </summary>
        [Fact]
        public async Task FinalChunk_WrittenAfterCompletion()
        {
            var rs = new ResponseStream(Array.Empty<byte>());

            var output = await SerializeWithConcurrentHandler(rs, async stream =>
            {
                await stream.WriteAsync(new byte[] { 1 });
                stream.MarkCompleted();
            });

            var outputStr = Encoding.ASCII.GetString(output);
            Assert.Contains("0\r\n", outputStr);

            // Final chunk should appear after the data chunk
            var dataChunkEnd = outputStr.IndexOf("1\r\n") + 3 + 1 + 2; // "1\r\n" + 1 byte data + "\r\n"
            var finalChunkIndex = outputStr.IndexOf("0\r\n", dataChunkEnd);
            Assert.True(finalChunkIndex >= 0, "Final chunk 0\\r\\n should appear after data chunks");
        }

        /// <summary>
        /// Property 20: Final Chunk Termination — empty stream still gets final chunk.
        /// Validates: Requirements 10.2
        /// </summary>
        [Fact]
        public async Task FinalChunk_EmptyStream_StillWritten()
        {
            var rs = new ResponseStream(Array.Empty<byte>());

            var output = await SerializeWithConcurrentHandler(rs, stream =>
            {
                stream.MarkCompleted();
                return Task.CompletedTask;
            });

            var outputStr = Encoding.ASCII.GetString(output);
            Assert.StartsWith("0\r\n", outputStr);
        }

        // ---- Property 21: Trailer Ordering ----

        /// <summary>
        /// Property 21: Trailer Ordering — trailers appear after final chunk.
        /// Validates: Requirements 10.3
        /// </summary>
        [Fact]
        public async Task ErrorTrailers_AppearAfterFinalChunk()
        {
            var rs = new ResponseStream(Array.Empty<byte>());

            var output = await SerializeWithConcurrentHandler(rs, async stream =>
            {
                await stream.WriteAsync(new byte[] { 1 });
                stream.ReportError(new Exception("fail"));
            });

            var outputStr = Encoding.UTF8.GetString(output);

            // Find the final chunk "0\r\n" that appears after data chunks
            var dataEnd = outputStr.IndexOf("1\r\n") + 3 + 1 + 2;
            var finalChunkIndex = outputStr.IndexOf("0\r\n", dataEnd);
            var errorTypeIndex = outputStr.IndexOf("Lambda-Runtime-Function-Error-Type:");
            var errorBodyIndex = outputStr.IndexOf("Lambda-Runtime-Function-Error-Body:");

            Assert.True(finalChunkIndex >= 0, "Final chunk not found");
            Assert.True(errorTypeIndex > finalChunkIndex, "Error type trailer should appear after final chunk");
            Assert.True(errorBodyIndex > finalChunkIndex, "Error body trailer should appear after final chunk");
        }

        // ---- Property 11: Midstream Error Type Trailer ----

        /// <summary>
        /// Property 11: Midstream Error Type Trailer — error type trailer is included for various exception types.
        /// Validates: Requirements 5.1, 5.2
        /// </summary>
        [Theory]
        [InlineData(typeof(InvalidOperationException))]
        [InlineData(typeof(ArgumentException))]
        [InlineData(typeof(NullReferenceException))]
        public async Task ErrorTrailer_IncludesErrorType(Type exceptionType)
        {
            var rs = new ResponseStream(Array.Empty<byte>());

            var output = await SerializeWithConcurrentHandler(rs, async stream =>
            {
                await stream.WriteAsync(new byte[] { 1 });
                var exception = (Exception)Activator.CreateInstance(exceptionType, "test error");
                stream.ReportError(exception);
            });

            var outputStr = Encoding.UTF8.GetString(output);
            Assert.Contains($"Lambda-Runtime-Function-Error-Type: {exceptionType.Name}", outputStr);
        }

        // ---- Property 12: Midstream Error Body Trailer ----

        /// <summary>
        /// Property 12: Midstream Error Body Trailer — error body trailer includes JSON exception details.
        /// Validates: Requirements 5.3
        /// </summary>
        [Fact]
        public async Task ErrorTrailer_IncludesJsonErrorBody()
        {
            var rs = new ResponseStream(Array.Empty<byte>());

            var output = await SerializeWithConcurrentHandler(rs, async stream =>
            {
                await stream.WriteAsync(new byte[] { 1 });
                stream.ReportError(new InvalidOperationException("something went wrong"));
            });

            var outputStr = Encoding.UTF8.GetString(output);
            Assert.Contains("Lambda-Runtime-Function-Error-Body:", outputStr);
            Assert.Contains("something went wrong", outputStr);
            Assert.Contains("InvalidOperationException", outputStr);
        }

        // ---- Final CRLF termination ----

        /// <summary>
        /// Test that the chunked message ends with CRLF after successful completion (no trailers).
        /// Validates: Requirements 10.2, 10.5
        /// </summary>
        [Fact]
        public async Task SuccessfulCompletion_EndsWithCrlf()
        {
            var rs = new ResponseStream(Array.Empty<byte>());

            var output = await SerializeWithConcurrentHandler(rs, async stream =>
            {
                await stream.WriteAsync(new byte[] { 1 });
                stream.MarkCompleted();
            });

            var outputStr = Encoding.ASCII.GetString(output);
            // Should end with "0\r\n" (final chunk) + "\r\n" (end of message)
            Assert.EndsWith("0\r\n\r\n", outputStr);
        }

        /// <summary>
        /// Test that the chunked message ends with CRLF after error trailers.
        /// Validates: Requirements 10.3, 10.5
        /// </summary>
        [Fact]
        public async Task ErrorCompletion_EndsWithCrlf()
        {
            var rs = new ResponseStream(Array.Empty<byte>());

            var output = await SerializeWithConcurrentHandler(rs, async stream =>
            {
                await stream.WriteAsync(new byte[] { 1 });
                stream.ReportError(new Exception("fail"));
            });

            var outputStr = Encoding.UTF8.GetString(output);
            Assert.EndsWith("\r\n", outputStr);
        }

        // ---- No error, no trailers ----

        [Fact]
        public async Task NoError_NoTrailersWritten()
        {
            var rs = new ResponseStream(Array.Empty<byte>());

            var output = await SerializeWithConcurrentHandler(rs, async stream =>
            {
                await stream.WriteAsync(new byte[] { 1 });
                stream.MarkCompleted();
            });

            var outputStr = Encoding.UTF8.GetString(output);
            Assert.DoesNotContain("Lambda-Runtime-Function-Error-Type:", outputStr);
            Assert.DoesNotContain("Lambda-Runtime-Function-Error-Body:", outputStr);
        }

        // ---- TryComputeLength ----

        [Fact]
        public void TryComputeLength_ReturnsFalse()
        {
            var stream = new ResponseStream(Array.Empty<byte>());
            var content = new StreamingHttpContent(stream);

            var result = content.Headers.ContentLength;
            Assert.Null(result);
        }

        // ---- CRLF correctness ----

        /// <summary>
        /// Property 22: CRLF Line Terminators — all line terminators are CRLF, not just LF.
        /// Validates: Requirements 10.5
        /// </summary>
        [Fact]
        public async Task CrlfTerminators_NoBareLineFeed()
        {
            var rs = new ResponseStream(Array.Empty<byte>());

            var output = await SerializeWithConcurrentHandler(rs, async stream =>
            {
                await stream.WriteAsync(new byte[] { 65, 66, 67 }); // "ABC"
                stream.MarkCompleted();
            });

            for (int i = 0; i < output.Length; i++)
            {
                if (output[i] == (byte)'\n')
                {
                    Assert.True(i > 0 && output[i - 1] == (byte)'\r',
                        $"Found bare LF at position {i} without preceding CR");
                }
            }
        }
    }
}
