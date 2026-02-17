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
            // Hand the HTTP output stream to ResponseStream so WriteAsync calls
            // can write chunks directly to it.
            _responseStream.SetHttpOutputStream(stream);

            // Wait for the handler to finish writing (MarkCompleted or ReportErrorAsync)
            await _responseStream.WaitForCompletionAsync();

            // Write final chunk
            await stream.WriteAsync(FinalChunkBytes, 0, FinalChunkBytes.Length);

            // Write error trailers if present
            if (_responseStream.HasError)
            {
                await WriteErrorTrailersAsync(stream, _responseStream.ReportedError);
            }

            // Write final CRLF to end the chunked message
            await stream.WriteAsync(CrlfBytes, 0, CrlfBytes.Length);
            await stream.FlushAsync();
        }

        protected override bool TryComputeLength(out long length)
        {
            length = -1;
            return false;
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
        }
    }
}
