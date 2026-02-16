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
using Xunit;

namespace Amazon.Lambda.RuntimeSupport.UnitTests
{
    public class StreamingHttpContentTests
    {
        private const long MaxResponseSize = 20 * 1024 * 1024;

        private async Task<byte[]> SerializeContentAsync(StreamingHttpContent content)
        {
            using var ms = new MemoryStream();
            await content.CopyToAsync(ms);
            return ms.ToArray();
        }

        // --- Task 5.4: Chunked encoding format tests ---

        /// <summary>
        /// Property 9: Chunked Encoding Format - chunks are formatted as size(hex) + CRLF + data + CRLF.
        /// Validates: Requirements 4.3, 10.1, 10.2
        /// </summary>
        [Theory]
        [InlineData(1)]
        [InlineData(10)]
        [InlineData(255)]
        [InlineData(4096)]
        public async Task ChunkedEncoding_SingleChunk_CorrectFormat(int chunkSize)
        {
            var stream = new ResponseStream(MaxResponseSize);
            var data = new byte[chunkSize];
            for (int i = 0; i < data.Length; i++) data[i] = (byte)(i % 256);
            await stream.WriteAsync(data);

            var content = new StreamingHttpContent(stream);
            var output = await SerializeContentAsync(content);
            var outputStr = Encoding.ASCII.GetString(output);

            var expectedSizeHex = chunkSize.ToString("X");
            Assert.StartsWith(expectedSizeHex + "\r\n", outputStr);

            // Verify chunk data follows the size line
            var dataStart = expectedSizeHex.Length + 2; // size + CRLF
            for (int i = 0; i < chunkSize; i++)
            {
                Assert.Equal(data[i], output[dataStart + i]);
            }

            // Verify CRLF after data
            Assert.Equal((byte)'\r', output[dataStart + chunkSize]);
            Assert.Equal((byte)'\n', output[dataStart + chunkSize + 1]);
        }

        /// <summary>
        /// Property 9: Chunked Encoding Format - multiple chunks each formatted correctly.
        /// Validates: Requirements 4.3, 10.1
        /// </summary>
        [Fact]
        public async Task ChunkedEncoding_MultipleChunks_EachFormattedCorrectly()
        {
            var stream = new ResponseStream(MaxResponseSize);
            await stream.WriteAsync(new byte[] { 0xAA, 0xBB });
            await stream.WriteAsync(new byte[] { 0xCC });

            var content = new StreamingHttpContent(stream);
            var output = await SerializeContentAsync(content);
            var outputStr = Encoding.ASCII.GetString(output);

            // First chunk: "2\r\n" + 2 bytes + "\r\n"
            Assert.StartsWith("2\r\n", outputStr);
            Assert.Equal(0xAA, output[3]);
            Assert.Equal(0xBB, output[4]);
            Assert.Equal((byte)'\r', output[5]);
            Assert.Equal((byte)'\n', output[6]);

            // Second chunk: "1\r\n" + 1 byte + "\r\n"
            Assert.Equal((byte)'1', output[7]);
            Assert.Equal((byte)'\r', output[8]);
            Assert.Equal((byte)'\n', output[9]);
            Assert.Equal(0xCC, output[10]);
            Assert.Equal((byte)'\r', output[11]);
            Assert.Equal((byte)'\n', output[12]);
        }

        /// <summary>
        /// Property 20: Final Chunk Termination - final chunk "0\r\n" is written.
        /// Validates: Requirements 10.2, 10.5
        /// </summary>
        [Fact]
        public async Task FinalChunk_IsWritten()
        {
            var stream = new ResponseStream(MaxResponseSize);
            await stream.WriteAsync(new byte[] { 1 });

            var content = new StreamingHttpContent(stream);
            var output = await SerializeContentAsync(content);
            var outputStr = Encoding.ASCII.GetString(output);

            // Output should end with final chunk "0\r\n"
            Assert.EndsWith("0\r\n", outputStr);
        }

        [Fact]
        public async Task FinalChunk_EmptyStream_OnlyFinalChunk()
        {
            var stream = new ResponseStream(MaxResponseSize);

            var content = new StreamingHttpContent(stream);
            var output = await SerializeContentAsync(content);

            Assert.Equal(Encoding.ASCII.GetBytes("0\r\n"), output);
        }

        /// <summary>
        /// Property 22: CRLF Line Terminators - all line terminators are CRLF, not just LF.
        /// Validates: Requirements 10.5
        /// </summary>
        [Fact]
        public async Task CrlfTerminators_NoBareLineFeed()
        {
            var stream = new ResponseStream(MaxResponseSize);
            await stream.WriteAsync(new byte[] { 65, 66, 67 }); // "ABC"

            var content = new StreamingHttpContent(stream);
            var output = await SerializeContentAsync(content);

            // Check every \n is preceded by \r
            for (int i = 0; i < output.Length; i++)
            {
                if (output[i] == (byte)'\n')
                {
                    Assert.True(i > 0 && output[i - 1] == (byte)'\r',
                        $"Found bare LF at position {i} without preceding CR");
                }
            }
        }

        [Fact]
        public void TryComputeLength_ReturnsFalse()
        {
            var stream = new ResponseStream(MaxResponseSize);
            var content = new StreamingHttpContent(stream);

            var result = content.Headers.ContentLength;

            Assert.Null(result);
        }

        // --- Task 5.6: Error trailer tests ---

        /// <summary>
        /// Property 11: Midstream Error Type Trailer - error type trailer is included for various exception types.
        /// Validates: Requirements 5.1, 5.2
        /// </summary>
        [Theory]
        [InlineData(typeof(InvalidOperationException))]
        [InlineData(typeof(ArgumentException))]
        [InlineData(typeof(NullReferenceException))]
        public async Task ErrorTrailer_IncludesErrorType(Type exceptionType)
        {
            var stream = new ResponseStream(MaxResponseSize);
            await stream.WriteAsync(new byte[] { 1 });
            var exception = (Exception)Activator.CreateInstance(exceptionType, "test error");
            await stream.ReportErrorAsync(exception);

            var content = new StreamingHttpContent(stream);
            var output = await SerializeContentAsync(content);
            var outputStr = Encoding.UTF8.GetString(output);

            Assert.Contains($"Lambda-Runtime-Function-Error-Type: {exceptionType.Name}", outputStr);
        }

        /// <summary>
        /// Property 12: Midstream Error Body Trailer - error body trailer includes JSON exception details.
        /// Validates: Requirements 5.3
        /// </summary>
        [Fact]
        public async Task ErrorTrailer_IncludesJsonErrorBody()
        {
            var stream = new ResponseStream(MaxResponseSize);
            await stream.WriteAsync(new byte[] { 1 });
            await stream.ReportErrorAsync(new InvalidOperationException("something went wrong"));

            var content = new StreamingHttpContent(stream);
            var output = await SerializeContentAsync(content);
            var outputStr = Encoding.UTF8.GetString(output);

            Assert.Contains("Lambda-Runtime-Function-Error-Body:", outputStr);
            Assert.Contains("something went wrong", outputStr);
            Assert.Contains("InvalidOperationException", outputStr);
        }

        /// <summary>
        /// Property 21: Trailer Ordering - trailers appear after final chunk.
        /// Validates: Requirements 10.3
        /// </summary>
        [Fact]
        public async Task ErrorTrailers_AppearAfterFinalChunk()
        {
            var stream = new ResponseStream(MaxResponseSize);
            await stream.WriteAsync(new byte[] { 1 });
            await stream.ReportErrorAsync(new Exception("fail"));

            var content = new StreamingHttpContent(stream);
            var output = await SerializeContentAsync(content);
            var outputStr = Encoding.UTF8.GetString(output);

            var finalChunkIndex = outputStr.IndexOf("0\r\n");
            var errorTypeIndex = outputStr.IndexOf("Lambda-Runtime-Function-Error-Type:");
            var errorBodyIndex = outputStr.IndexOf("Lambda-Runtime-Function-Error-Body:");

            Assert.True(finalChunkIndex >= 0, "Final chunk not found");
            Assert.True(errorTypeIndex > finalChunkIndex, "Error type trailer should appear after final chunk");
            Assert.True(errorBodyIndex > finalChunkIndex, "Error body trailer should appear after final chunk");
        }

        [Fact]
        public async Task NoError_NoTrailersWritten()
        {
            var stream = new ResponseStream(MaxResponseSize);
            await stream.WriteAsync(new byte[] { 1 });

            var content = new StreamingHttpContent(stream);
            var output = await SerializeContentAsync(content);
            var outputStr = Encoding.UTF8.GetString(output);

            Assert.DoesNotContain("Lambda-Runtime-Function-Error-Type:", outputStr);
            Assert.DoesNotContain("Lambda-Runtime-Function-Error-Body:", outputStr);
        }

        [Fact]
        public async Task ErrorTrailers_EndWithCrlf()
        {
            var stream = new ResponseStream(MaxResponseSize);
            await stream.WriteAsync(new byte[] { 1 });
            await stream.ReportErrorAsync(new Exception("fail"));

            var content = new StreamingHttpContent(stream);
            var output = await SerializeContentAsync(content);
            var outputStr = Encoding.UTF8.GetString(output);

            // Should end with final CRLF after trailers
            Assert.EndsWith("\r\n", outputStr);
        }
    }
}
