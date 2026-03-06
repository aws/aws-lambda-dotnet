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
using System.Threading;
using System.Threading.Tasks;
using Amazon.Lambda.RuntimeSupport.Helpers;

namespace Amazon.Lambda.RuntimeSupport.Client.ResponseStreaming
{
    /// <summary>
    /// HttpContent implementation for streaming responses with chunked transfer encoding.
    /// </summary>
    internal class StreamingHttpContent : HttpContent
    {
        private readonly ResponseStream _responseStream;
        private readonly CancellationToken _cancellationToken;

        public StreamingHttpContent(ResponseStream responseStream, CancellationToken cancellationToken = default)
        {
            _responseStream = responseStream ?? throw new ArgumentNullException(nameof(responseStream));
            _cancellationToken = cancellationToken;
        }

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext context)
        {
            // Hand the HTTP output stream to ResponseStream so WriteAsync calls
            // can write chunks directly to it.
            await _responseStream.SetHttpOutputStreamAsync(stream, _cancellationToken);

            InternalLogger.GetDefaultLogger().LogInformation("In SerializeToStreamAsync waiting for the underlying Lambda response stream in indicate it is complete.");
            // Wait for the handler to finish writing (MarkCompleted or ReportErrorAsync)
            await _responseStream.WaitForCompletionAsync(_cancellationToken);

            // Write error trailers if present
            if (_responseStream.HasError)
            {
                InternalLogger.GetDefaultLogger().LogError(_responseStream.ReportedError, "An error occurred during Lambda execution. Writing error trailers to response.");
                await WriteErrorTrailersAsync(stream, _responseStream.ReportedError);
            }
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
            await stream.WriteAsync(errorTypeBytes, 0, errorTypeBytes.Length, _cancellationToken);

            var errorBodyJson = LambdaJsonExceptionWriter.WriteJson(exceptionInfo);
            var errorBodyHeader = $"{StreamingConstants.ErrorBodyTrailer}: {errorBodyJson}\r\n";
            var errorBodyBytes = Encoding.UTF8.GetBytes(errorBodyHeader);
            await stream.WriteAsync(errorBodyBytes, 0, errorBodyBytes.Length, _cancellationToken);
        }
    }
}
