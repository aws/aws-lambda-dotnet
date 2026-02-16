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
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Amazon.Lambda.RuntimeSupport
{
    /// <summary>
    /// HttpContent implementation for streaming responses with chunked transfer encoding.
    /// </summary>
    internal class StreamingHttpContent : HttpContent
    {
        private static readonly byte[] CrlfBytes = Encoding.ASCII.GetBytes("\r\n");
        private static readonly byte[] FinalChunkBytes = Encoding.ASCII.GetBytes("0\r\n");

        private readonly ResponseStream _responseStream;

        public StreamingHttpContent(ResponseStream responseStream)
        {
            _responseStream = responseStream ?? throw new ArgumentNullException(nameof(responseStream));
        }

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext context)
        {
            foreach (var chunk in _responseStream.Chunks)
            {
                await WriteChunkAsync(stream, chunk);
            }

            await WriteFinalChunkAsync(stream);

            if (_responseStream.HasError)
            {
                await WriteErrorTrailersAsync(stream, _responseStream.ReportedError);
            }

            await stream.FlushAsync();
        }

        protected override bool TryComputeLength(out long length)
        {
            length = -1;
            return false;
        }

        private async Task WriteChunkAsync(Stream stream, byte[] data)
        {
            var chunkSizeHex = data.Length.ToString("X");
            var chunkSizeBytes = Encoding.ASCII.GetBytes(chunkSizeHex);

            await stream.WriteAsync(chunkSizeBytes, 0, chunkSizeBytes.Length);
            await stream.WriteAsync(CrlfBytes, 0, CrlfBytes.Length);
            await stream.WriteAsync(data, 0, data.Length);
            await stream.WriteAsync(CrlfBytes, 0, CrlfBytes.Length);
        }

        private async Task WriteFinalChunkAsync(Stream stream)
        {
            await stream.WriteAsync(FinalChunkBytes, 0, FinalChunkBytes.Length);
        }

        private async Task WriteErrorTrailersAsync(Stream stream, Exception exception)
        {
            var exceptionInfo = ExceptionInfo.GetExceptionInfo(exception);

            var errorTypeHeader = $"{StreamingConstants.ErrorTypeTrailer}: {exceptionInfo.ErrorType}\r\n";
            var errorTypeBytes = Encoding.UTF8.GetBytes(errorTypeHeader);
            await stream.WriteAsync(errorTypeBytes, 0, errorTypeBytes.Length);

            var errorBodyJson = LambdaJsonExceptionWriter.WriteJson(exceptionInfo);
            var errorBodyHeader = $"{StreamingConstants.ErrorBodyTrailer}: {errorBodyJson}\r\n";
            var errorBodyBytes = Encoding.UTF8.GetBytes(errorBodyHeader);
            await stream.WriteAsync(errorBodyBytes, 0, errorBodyBytes.Length);

            await stream.WriteAsync(CrlfBytes, 0, CrlfBytes.Length);
        }
    }
}
