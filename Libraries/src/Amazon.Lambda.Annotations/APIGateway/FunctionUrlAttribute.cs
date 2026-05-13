// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace Amazon.Lambda.Annotations.APIGateway
{
    /// <summary>
    /// Configures the Lambda function to be invoked via a Lambda Function URL.
    /// </summary>
    /// <remarks>
    /// Function URLs use the same payload format as HTTP API v2 (APIGatewayHttpApiV2ProxyRequest/Response).
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method)]
    public class FunctionUrlAttribute : Attribute
    {
        /// <inheritdoc cref="FunctionUrlAuthType"/>
        public FunctionUrlAuthType AuthType { get; set; } = FunctionUrlAuthType.NONE;

        /// <summary>
        /// The allowed origins for CORS requests. Example: new[] { "https://example.com" }
        /// </summary>
        public string[] AllowOrigins { get; set; }

        /// <summary>
        /// The allowed HTTP methods for CORS requests. Example: new[] { LambdaHttpMethod.Get, LambdaHttpMethod.Post }
        /// </summary>
        public LambdaHttpMethod[] AllowMethods { get; set; }

        /// <summary>
        /// The allowed headers for CORS requests.
        /// </summary>
        public string[] AllowHeaders { get; set; }

        /// <summary>
        /// Whether credentials are included in the CORS request.
        /// </summary>
        public bool AllowCredentials { get; set; }

        /// <summary>
        /// The expose headers for CORS responses.
        /// </summary>
        public string[] ExposeHeaders { get; set; }

        /// <summary>
        /// The maximum time in seconds that a browser can cache the CORS preflight response.
        /// A value of 0 means the property is not set.
        /// </summary>
        public int MaxAge { get; set; }
    }
}
