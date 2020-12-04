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

using Amazon.Lambda.Core;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace HandlerTest
{
    public static class Common
    {
        private static readonly string AppSettingsPath = Path.Combine(GetCurrentLocation(), "appsettings.json");
        private static string GetCurrentLocation()
        {
            var assemblyLocation = typeof(Common).GetTypeInfo().Assembly.Location;
            var currentDir = Path.GetDirectoryName(assemblyLocation);
            return currentDir;
        }

        public static readonly ILoggerFactory LoggerFactory = CreateLoggerFactory();
        private static ILoggerFactory CreateLoggerFactory()
        {
            var configuration = new ConfigurationBuilder()
                .AddJsonFile(AppSettingsPath)
                .Build();

            var loggingSection = configuration.GetSection("Lambda.Logging");
            if (loggingSection == null)
                throw new InvalidOperationException($"Cannot find Lambda.Logging section.");
            var options = new LambdaLoggerOptions(configuration);
            if (options.IncludeCategory != false)
                throw new InvalidOperationException($"IncludeCategory should be false.");
            if (options.IncludeLogLevel != true)
                throw new InvalidOperationException($"IncludeLogLevel should be true.");

            var loggerfactory = new TestLoggerFactory()
                .AddLambdaLogger(options);
            return loggerfactory;
        }

        public static string GetString(Stream stream)
        {
            if (stream == null)
                return null;

            using (var reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }
        public static MemoryStream GetStream(string data)
        {
            if (data == null)
                return null;

            var bytes = Encoding.UTF8.GetBytes(data);
            var ms = new MemoryStream(bytes);
            return ms;
        }

        public static void LogCommonData(string methodName)
        {
            LogCommonData(methodName, null, null);
        }
        public static void LogCommonData(string methodName, string data)
        {
            LogCommonData(methodName, data, null);
        }
        public static void LogCommonData(string methodName, ILambdaContext context)
        {
            LogCommonData(methodName, null, context);
        }
        public static void LogCommonData(string methodName, string data, ILambdaContext context)
        {
            // log method
            LambdaLogger.Log($">>[{methodName}]>>");

            // log data
            if (data != null)
            {
                LambdaLogger.Log($"<<[{data}]<<");
            }

            // log context data
            if (context != null)
            {
                var contextData = new List<string>();
                contextData.Add(context.GetType().FullName);
                contextData.Add(context.AwsRequestId);
                contextData.Add(context.ClientContext.GetType().FullName);
                contextData.Add(context.ClientContext.Client.AppPackageName);
                contextData.Add(context.ClientContext.Client.AppTitle);
                contextData.Add(context.ClientContext.Client.AppVersionCode);
                contextData.Add(context.ClientContext.Client.AppVersionName);
                contextData.Add(context.ClientContext.Client.InstallationId);
                contextData.Add(string.Join(", ", context.ClientContext.Custom.Keys));
                contextData.Add(string.Join(", ", context.ClientContext.Custom.Values));
                contextData.Add(string.Join(", ", context.ClientContext.Environment.Keys));
                contextData.Add(string.Join(", ", context.ClientContext.Environment.Values));
                contextData.Add(context.FunctionName);
                contextData.Add(context.FunctionVersion);
                contextData.Add(context.Identity.IdentityId);
                contextData.Add(context.Identity.IdentityPoolId);
                contextData.Add(context.InvokedFunctionArn);
                contextData.Add(context.Logger.GetType().FullName);
                contextData.Add(context.LogGroupName);
                contextData.Add(context.LogStreamName);
                contextData.Add(context.MemoryLimitInMB.ToString());
                contextData.Add(context.RemainingTime.Ticks.ToString());

                LambdaLogger.Log($"==[{string.Join(";", contextData)}]==");
            }

            // log using ILogger
            var loggerFactory = Common.LoggerFactory;
            if (loggerFactory == null)
                throw new InvalidOperationException("LoggerFactory is null!");

            var nullLogger = loggerFactory.CreateLogger(null);
            nullLogger.LogInformation($"__[nullLogger-{methodName}]__");
            nullLogger.LogTrace($"##[nullLogger-{methodName}]##");

            var testLogger = loggerFactory.CreateLogger("HandlerTest.Logging");
            testLogger.LogInformation($"__[testLogger-{methodName}]__");

            // log using LambdaLogger static
            LambdaLogger.Log($"^^[{methodName}]^^");
        }
    }
}
