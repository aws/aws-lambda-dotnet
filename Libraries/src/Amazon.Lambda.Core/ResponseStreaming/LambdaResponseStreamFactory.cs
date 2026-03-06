// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0
#if NET8_0_OR_GREATER
using System;
using System.IO;
using System.Runtime.Versioning;

namespace Amazon.Lambda.Core.ResponseStreaming
{
    /// <summary>
    /// Factory to create Lambda response streams for writing streaming responses in AWS Lambda functions. The created streams are write-only and non-seekable.
    /// </summary>
    [RequiresPreviewFeatures(LambdaResponseStreamFactory.ParameterizedPreviewMessage)]
    public class LambdaResponseStreamFactory
    {
        internal const string ParameterizedPreviewMessage =
            "Response streaming is in preview till a new version of .NET Lambda runtime client that supports response streaming " +
            "has been deployed to the .NET Lambda managed runtime. Till deployment has been made the feature can be used by deploying as an " +
            "executable including the latest version of Amazon.Lambda.RuntimeSupport and setting the \"EnablePreviewFeatures\" in the Lambda " +
            "project file to \"true\"";

        private static Func<byte[], ILambdaResponseStream> _streamFactory;

        internal static void SetLambdaResponseStream(Func<byte[], ILambdaResponseStream> streamFactory)
        {
            _streamFactory = streamFactory ?? throw new ArgumentNullException(nameof(streamFactory));
        }

        /// <summary>
        /// Creates a <see cref="Stream"/> that can be used to write streaming responses back to callers of the Lambda function. Once
        /// a Lambda function creates a response stream all output must be returned by writing to the stream; the Lambda function's handler
        /// return value will be ignored. The stream is write-only and non-seekable.
        /// </summary>
        /// <returns></returns>
        public static Stream CreateStream()
        {
            var runtimeResponseStream = _streamFactory(Array.Empty<byte>());
            return new LambdaResponseStream(runtimeResponseStream);
        }

        /// <summary>
        /// Create a <see cref="Stream"/> for writing streaming responses, with an HTTP response prelude containing status code and headers. This should be used for
        /// Lambda functions using response streaming that are invoked via the Lambda Function URLs or API Gateway HTTP APIs, where the response format is expected to be an HTTP response.
        /// The prelude will be serialized and sent as the first chunk of the response stream, and should contain any necessary HTTP status code and headers.
        /// <para>
        /// Once a Lambda function creates a response stream all output must be returned by writing to the stream; the Lambda function's handler
        /// return value will be ignored. The stream is write-only and non-seekable.
        /// </para>
        /// </summary>
        /// <param name="prelude">The HTTP response prelude including status code and headers.</param>
        /// <returns></returns>
        public static Stream CreateHttpStream(HttpResponseStreamPrelude prelude)
        {
            var runtimeResponseStream = _streamFactory(prelude.ToByteArray());
            return new LambdaResponseStream(runtimeResponseStream);
        }
    }
}
#endif
