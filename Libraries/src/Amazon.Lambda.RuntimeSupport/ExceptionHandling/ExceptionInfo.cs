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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Amazon.Lambda.RuntimeSupport
{
    /// <summary>
    /// Class to hold basic raw information extracted from Exceptions.
    /// The raw information will be formatted as JSON to be reported to the Lambda Runtime API.
    /// </summary>
    internal class ExceptionInfo
    {
        public string ErrorMessage { get; set; }
        public string ErrorType { get; set; }
        public StackFrameInfo[] StackFrames { get; set; }
        public string StackTrace { get; set; }

        public ExceptionInfo InnerException { get; set; }
        public List<ExceptionInfo> InnerExceptions { get; internal set; } = new List<ExceptionInfo>();

        public Exception OriginalException { get; set; }

        public ExceptionInfo() { }

        public ExceptionInfo(Exception exception, bool isNestedException = false)
        {
            if (exception == null)
                throw new ArgumentNullException(nameof(exception));

            ErrorType = exception.GetType().Name;
            ErrorMessage = exception.Message;

            if (!string.IsNullOrEmpty(exception.StackTrace))
            {
                StackTrace stackTrace = new StackTrace(exception, true);
                StackTrace = stackTrace.ToString();

                // Only extract the stack frames like this for the top-level exception
                // This is used for Xray Exception serialization
                if (isNestedException || stackTrace?.GetFrames() == null)
                {
                    StackFrames = new StackFrameInfo[0];
                }
                else
                {
                    StackFrames = (
                        from sf in stackTrace.GetFrames()
                        where sf != null
                        select new StackFrameInfo(sf)
                    ).ToArray();
                }
            }

            if (exception.InnerException != null)
            {
                InnerException = new ExceptionInfo(exception.InnerException, true);
            }

            AggregateException aggregateException = exception as AggregateException;

            if (aggregateException != null && aggregateException.InnerExceptions != null)
            {
                foreach (var innerEx in aggregateException.InnerExceptions)
                {
                    InnerExceptions.Add(new ExceptionInfo(innerEx, true));
                }
            }
        }

        public static ExceptionInfo GetExceptionInfo(Exception exception)
        {
            return new ExceptionInfo(exception);
        }
    }
}
