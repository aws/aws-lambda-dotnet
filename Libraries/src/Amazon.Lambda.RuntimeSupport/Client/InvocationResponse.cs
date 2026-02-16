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

namespace Amazon.Lambda.RuntimeSupport
{
    /// <summary>
    /// Class that contains the response for an invocation of an AWS Lambda function.
    /// </summary>
    public class InvocationResponse
    {
        /// <summary>
        /// Output from the function invocation.
        /// </summary>
        public Stream OutputStream { get; set; }

        /// <summary>
        /// True if the LambdaBootstrap should dispose the stream after it's read, false otherwise.
        /// Set this to false if you plan to reuse the same output stream for multiple invocations of the function.
        /// </summary>
        public bool DisposeOutputStream { get; private set; } = true;

        /// <summary>
        /// Indicates whether this response uses streaming mode.
        /// Set internally by the runtime when ResponseStreamFactory.CreateStream() is called.
        /// </summary>
        internal bool IsStreaming { get; set; }

        /// <summary>
        /// The ResponseStream instance if streaming mode is used.
        /// Set internally by the runtime.
        /// </summary>
        internal ResponseStream ResponseStream { get; set; }

        /// <summary>
        /// Construct a InvocationResponse with an output stream that will be disposed by the Lambda Runtime Client. 
        /// </summary>
        /// <param name="outputStream"></param>
        public InvocationResponse(Stream outputStream)
            : this(outputStream, true)
        { }

        /// <summary>
        /// Construct a InvocationResponse
        /// </summary>
        /// <param name="outputStream"></param>
        /// <param name="disposeOutputStream"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public InvocationResponse(Stream outputStream, bool disposeOutputStream)
        {
            OutputStream = outputStream ?? throw new ArgumentNullException(nameof(outputStream));
            DisposeOutputStream = disposeOutputStream;
            IsStreaming = false;
        }

        /// <summary>
        /// Creates an InvocationResponse for a streaming response.
        /// Used internally by the runtime.
        /// </summary>
        internal static InvocationResponse CreateStreamingResponse(ResponseStream responseStream)
        {
            return new InvocationResponse(Stream.Null, false)
            {
                IsStreaming = true,
                ResponseStream = responseStream
            };
        }
    }
}
