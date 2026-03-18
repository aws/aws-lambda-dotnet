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

        [Fact]
        public async Task SerializeToStreamAsync_HandsOffHttpStream_WritesFlowThrough()
        {
            var rs = new ResponseStream(Array.Empty<byte>());

            var output = await SerializeWithConcurrentHandler(rs, async stream =>
            {
                await stream.WriteAsync(new byte[] { 0xAA, 0xBB });
                stream.MarkCompleted();
            });

            Assert.Equal(2, output.Length);
        }

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

        [Fact]
        public void TryComputeLength_ReturnsFalse()
        {
            var stream = new ResponseStream(Array.Empty<byte>());
            var content = new StreamingHttpContent(stream);

            var result = content.Headers.ContentLength;
            Assert.Null(result);
        }
    }
}
