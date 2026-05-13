// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

namespace Amazon.Lambda.RuntimeSupport.Bootstrap
{
    /// <summary>
    /// Container for Lambda Bootstrap options.
    /// This is only for TESTING environments and should not be used in production.
    /// </summary>
    public class LambdaBootstrapOptions
    {
        /// <summary>
        /// The endpoint of the Lambda Runtime API.
        /// In PRODUCTION environments, this is retrieved from environment variables.
        /// In TESTING environments, this can be defined via command line arguments.
        /// This is only for TESTING environments and should not be used in production.
        /// </summary>
        public string RuntimeApiEndpoint { get; set; }
    }
}
