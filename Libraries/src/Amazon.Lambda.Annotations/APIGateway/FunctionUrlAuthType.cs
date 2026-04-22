// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

namespace Amazon.Lambda.Annotations.APIGateway
{
    /// <summary>
    /// The type of authentication for a Lambda Function URL.
    /// </summary>
    public enum FunctionUrlAuthType
    {
        /// <summary>
        /// No authentication. Anyone with the Function URL can invoke the function.
        /// </summary>
        NONE,

        /// <summary>
        /// IAM authentication. Only authenticated IAM users and roles can invoke the function.
        /// </summary>
        AWS_IAM
    }
}
