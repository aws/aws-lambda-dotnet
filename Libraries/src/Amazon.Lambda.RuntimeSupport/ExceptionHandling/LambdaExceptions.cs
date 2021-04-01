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
using System.Globalization;

namespace Amazon.Lambda.RuntimeSupport.ExceptionHandling
{
    /// <summary>
    /// Static methods for formatting and creating Lambda exceptions.
    /// </summary>
    internal static class LambdaExceptions
    {
        /// <summary>
        /// Creates LambdaValidationException with specified messageFormat,
        /// and arguments.
        /// If an exception is encountered when formatting the string, messageFormat
        /// is used as the message.
        /// </summary>
        /// <param name="messageFormat"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public static LambdaValidationException ValidationException(string messageFormat, params object[] args)
        {
            return new LambdaValidationException(FormatMessage(messageFormat, args));
        }

        /// <summary>
        /// Creates LambdaValidationException with specified inner exception,
        /// messageFormat, and arguments.
        /// If an exception is encountered when formatting the string, messageFormat
        /// is used as the message.
        /// </summary>
        /// <param name="innerException"></param>
        /// <param name="messageFormat"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public static LambdaValidationException ValidationException(Exception innerException, string messageFormat, params object[] args)
        {
            return new LambdaValidationException(FormatMessage(messageFormat, args), innerException);
        }

        /// <summary>
        /// Attempts to create a string from the specified format and arguments.
        /// If string.Format fails, messageFormat is returned as the message.
        /// </summary>
        /// <param name="messageFormat"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public static string FormatMessage(string messageFormat, params object[] args)
        {
            string message;
            try
            {
                message = string.Format(CultureInfo.InvariantCulture, messageFormat, args);
            }
            catch
            {
                // if Format fails, go with the unformatted message, so customer at least
                // has some sort of clue of what went wrong
                message = messageFormat;
            }

            return message;
        }
    }
}