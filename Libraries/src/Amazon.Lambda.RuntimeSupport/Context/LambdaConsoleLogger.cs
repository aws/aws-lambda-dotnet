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
using Amazon.Lambda.RuntimeSupport.Helpers;
using System;

namespace Amazon.Lambda.RuntimeSupport
{
    internal class LambdaConsoleLogger : ILambdaLogger
    {
        private IConsoleLoggerWriter _consoleLoggerRedirector;

        public LambdaConsoleLogger(IConsoleLoggerWriter consoleLoggerRedirector)
        {
            _consoleLoggerRedirector = consoleLoggerRedirector;
        }

        public void Log(string message)
        {
            Console.Write(message);
        }

        public void LogLine(string message)
        {
            _consoleLoggerRedirector.FormattedWriteLine(message);
        }

        public string CurrentAwsRequestId { get; set; }

#if NET6_0_OR_GREATER
        public void Log(string level, string message)
        {
            _consoleLoggerRedirector.FormattedWriteLine(level, message);
        }
#endif
    }
}
