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
    /// Class that contains all the information necessary to handle an invocation of an AWS Lambda function.
    /// </summary>
    public class InvocationRequest : IDisposable
    {
        /// <summary>
        /// Input to the function invocation.
        /// </summary>
        public Stream InputStream { get; internal set; }

        /// <summary>
        /// Context for the invocation.
        /// </summary>
        public ILambdaContext LambdaContext { get; internal set; }

        internal InvocationRequest() { }

        public void Dispose()
        {
            InputStream?.Dispose();
        }
    }
}
