/*
 * Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
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

namespace Amazon.Lambda.RuntimeSupport.ExceptionHandling
{
    ///<summary>
    /// An exception indicating that one of the inputs provided by the user
    /// is not valid. This can indicate an error in the handler string (invalid format
    /// or non-existent assembly/type/method), error in the type (abstract / generic),
    /// error in the method (invalid signature, generic, overloads, or params/varargs),
    /// or an error in the Serializer (lacking attribute, invalid type in attribute).
    /// The message in this exception is retained when returning the exception to the user.
    /// This exception should not have any inner exceptions.
    /// Ref: https://w.amazon.com/bin/view/AWS/DeveloperResources/AWSSDKsAndTools/NetSDK/NetLambda/Design/ExceptionHandling/
    /// </summary>
    internal sealed class LambdaValidationException : Exception
    {
        /// <summary>
        /// Construct instance of LambdaValidationException
        /// </summary>
        /// <param name="message">The message to display to the user.</param>
        public LambdaValidationException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Construct instance of LambdaValidationException
        /// </summary>
        /// <param name="message">The message to display to the user.</param>
        /// <param name="innerException">The cause of this exception.</param>
        public LambdaValidationException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}