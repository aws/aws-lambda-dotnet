﻿/*
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
using System.Runtime.Versioning;

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

        private const string ParameterizedPreviewMessage =
            "Parameterized logging is in preview till a new version of .NET Lambda runtime client that supports parameterized logging " +
            "has been deployed to the .NET Lambda managed runtime. Till deployment has been made the feature can be used by deploying as an " +
            "executable including the latest version of Amazon.Lambda.RuntimeSupport and setting the \"LangVersion\" in the Lambda " +
            "project file to \"preview\"";

        [RequiresPreviewFeatures(ParameterizedPreviewMessage)]
        public void Log(string level, string message, params object[] args)
        {
            _consoleLoggerRedirector.FormattedWriteLine(level, message, args);
        }

        [RequiresPreviewFeatures(ParameterizedPreviewMessage)]
        public void Log(string level, Exception exception, string message, params object[] args)
        {
            _consoleLoggerRedirector.FormattedWriteLine(level, exception, message, args);
        }
#endif
    }
}
