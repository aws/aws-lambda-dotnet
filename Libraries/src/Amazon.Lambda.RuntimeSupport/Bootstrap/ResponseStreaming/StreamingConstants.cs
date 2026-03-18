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

namespace Amazon.Lambda.RuntimeSupport.Client.ResponseStreaming
{
    /// <summary>
    /// Constants used for Lambda response streaming.
    /// </summary>
    internal static class StreamingConstants
    {
        /// <summary>
        /// Header name for Lambda response mode.
        /// </summary>
        public const string ResponseModeHeader = "Lambda-Runtime-Function-Response-Mode";

        /// <summary>
        /// Value for streaming response mode.
        /// </summary>
        public const string StreamingResponseMode = "streaming";

        /// <summary>
        /// Trailer header name for error type.
        /// </summary>
        public const string ErrorTypeTrailer = "Lambda-Runtime-Function-Error-Type";

        /// <summary>
        /// Trailer header name for error body.
        /// </summary>
        public const string ErrorBodyTrailer = "Lambda-Runtime-Function-Error-Body";
    }
}
