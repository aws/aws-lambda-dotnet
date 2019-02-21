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
using Amazon.Lambda.Core;
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

        public InvocationResponse(Stream outputStream)
            : this(outputStream, true)
        { }

        public InvocationResponse(Stream outputStream, bool disposeOutputStream)
        {
            OutputStream = outputStream ?? throw new ArgumentNullException(nameof(outputStream));
            DisposeOutputStream = disposeOutputStream;
        }
    }
}
