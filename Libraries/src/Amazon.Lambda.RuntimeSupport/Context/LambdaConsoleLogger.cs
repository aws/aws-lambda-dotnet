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
using System;

namespace Amazon.Lambda.RuntimeSupport
{
    internal class LambdaConsoleLogger : ILambdaLogger
    {
        public void Log(string message)
        {
            Console.Write(message);
        }

        public void LogLine(string message)
        {
            Console.WriteLine(message);
        }

        public string CurrentAwsRequestId { get; set; }

#if NET6_0_OR_GREATER
        const string LOG_LEVEL_ENVIRONMENT_VARAIBLE = "AWS_LAMBDA_HANDLER_LOG_LEVEL";
        private LogLevel _minmumLogLevel = LogLevel.Information;

        internal LambdaConsoleLogger()
        {
            var envLogLevel = Environment.GetEnvironmentVariable(LOG_LEVEL_ENVIRONMENT_VARAIBLE); ;
            if(!string.IsNullOrEmpty(envLogLevel))
            {
                if(Enum.TryParse(typeof(LogLevel), envLogLevel, true, out var result))
                {
                    _minmumLogLevel = (LogLevel)result;
                }
            }
        }

        public void Log(LogLevel level, string message)
        {
            if (level < _minmumLogLevel)
                return;

            var line = $"{DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")}\t{CurrentAwsRequestId}\t{ConvertLogLevelToLabel(level)}\t{message}";
            Console.WriteLine(line);
        }

        /// <summary>
        /// Convert LogLevel enums to the the same string label that console provider for Microsoft.Extensions.Logging.ILogger uses.
        /// </summary>
        /// <param name="level"></param>
        /// <returns></returns>
        private string ConvertLogLevelToLabel(LogLevel level)
        {
            return level switch
            {
                LogLevel.Trace => "trce",
                LogLevel.Debug => "dbug",
                LogLevel.Information => "info",
                LogLevel.Warning => "warn",
                LogLevel.Error => "fail",
                LogLevel.Critical => "crit",
                _ => ""
            };
        }
#endif
    }
}
