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
using System.Runtime.CompilerServices;
using Amazon.Lambda.RuntimeSupport.Bootstrap;

namespace Amazon.Lambda.RuntimeSupport.Helpers
{
    internal static class Utils
    {
        // The event name emitted in the worker pool initialization debug log. This matches the event name used by the
        // Java and Python Lambda runtimes so the log is queryable consistently across languages.
        internal const string WorkerPoolInitializingEvent = "runtime_worker_pool_initializing";

        // The message template used for the worker pool initialization debug log. The named properties are emitted as
        // top level fields in the structured JSON log (event, workerCount, executionEnvironmentMaxConcurrency).
        internal const string WorkerPoolInitializingLogTemplate = "{event} workerCount={workerCount} executionEnvironmentMaxConcurrency={executionEnvironmentMaxConcurrency}";

        public static bool IsRunningNativeAot()
        {
            // If dynamic code is not supported we are most likely running in an AOT environment.
            return !RuntimeFeature.IsDynamicCodeSupported;
        }

        /// <summary>
        /// Determines if the customer configured the Lambda function to use the JSON log format. This mirrors the
        /// resolution done by <see cref="LogLevelLoggerWriter"/>: the .NET runtime specific environment variable is
        /// checked first, falling back to the Lambda platform environment variable.
        /// </summary>
        internal static bool IsJsonLogFormat(IEnvironmentVariables environmentVariables)
        {
            var logFormat = environmentVariables.GetEnvironmentVariable(Constants.NET_RIC_LOG_FORMAT_ENVIRONMENT_VARIABLE);
            if (string.IsNullOrEmpty(logFormat))
            {
                logFormat = environmentVariables.GetEnvironmentVariable(Constants.LAMBDA_LOG_FORMAT_ENVIRONMENT_VARIABLE);
            }

            return string.Equals(logFormat, Constants.LAMBDA_LOG_FORMAT_JSON, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Emits a one time DEBUG log during init reporting the number of worker (processing) tasks and the execution
        /// environment max concurrency. This is only emitted when the function is running in multi concurrency mode
        /// (Lambda managed instances) and the JSON log format is configured. The DEBUG level means the log is only
        /// surfaced when the function log level is set to Debug or Trace; that filtering is handled by the console
        /// logger writer so it is not re-checked here.
        /// </summary>
        internal static void EmitWorkerPoolInitializingLog(IConsoleLoggerWriter consoleLogger, IEnvironmentVariables environmentVariables, int workerCount, int maxConcurrency)
        {
            if (consoleLogger == null)
                return;

            if (!IsUsingMultiConcurrency(environmentVariables) || !IsJsonLogFormat(environmentVariables))
                return;

            consoleLogger.FormattedWriteLine(
                LogLevelLoggerWriter.LogLevel.Debug.ToString(),
                WorkerPoolInitializingLogTemplate,
                WorkerPoolInitializingEvent,
                workerCount,
                maxConcurrency);
        }

        /// <summary>
        /// Determines if the Lambda function is running in multi concurrency mode.
        /// </summary>
        internal static bool IsUsingMultiConcurrency(IEnvironmentVariables environmentVariables)
        {
            return !string.IsNullOrEmpty(environmentVariables.GetEnvironmentVariable(Constants.ENVIRONMENT_VARIABLE_AWS_LAMBDA_MAX_CONCURRENCY));
        }

        /// <summary>
        /// Determines the number of .NET Tasks that should be created that will iterate a loop polling the Lambda runtime for new events.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        internal static int DetermineProcessingTaskCount(IEnvironmentVariables environmentVariables, int processorCount)
        {
            var processingTaskCount = 1;
            if (IsUsingMultiConcurrency(environmentVariables))
            {
                // Check the .NET specific environment variable that allows customers the option to override our default computed value.
                var overrideCount = environmentVariables.GetEnvironmentVariable(Constants.ENVIRONMENT_VARIABLE_AWS_LAMBDA_DOTNET_PROCESSING_TASKS);
                if (!string.IsNullOrEmpty(overrideCount))
                {
                    if (!int.TryParse(overrideCount, out processingTaskCount) || processingTaskCount <= 0)
                    {
                        throw new ArgumentException($"Value {overrideCount} for environment variable {Constants.ENVIRONMENT_VARIABLE_AWS_LAMBDA_DOTNET_PROCESSING_TASKS} failed to parse as an integer greater then 0");
                    }
                }
                else
                {
                    // Use the max concurrency value as the default polling task count so there are
                    // enough polling tasks to fill all available concurrency slots. Fall back to
                    // the processor-based heuristic if the value cannot be parsed.
                    var maxConcurrency = GetMaxConcurrency(environmentVariables);
                    processingTaskCount = maxConcurrency > 0
                        ? maxConcurrency
                        : Math.Max(2, processorCount);
                }
            }

            return processingTaskCount;
        }

        /// <summary>
        /// Parses the AWS_LAMBDA_MAX_CONCURRENCY environment variable as an integer.
        /// Returns the parsed value if valid and greater than 0, otherwise returns 0.
        /// </summary>
        internal static int GetMaxConcurrency(IEnvironmentVariables environmentVariables)
        {
            var value = environmentVariables.GetEnvironmentVariable(Constants.ENVIRONMENT_VARIABLE_AWS_LAMBDA_MAX_CONCURRENCY);
            if (!string.IsNullOrEmpty(value) && int.TryParse(value, out var maxConcurrency) && maxConcurrency > 0)
            {
                return maxConcurrency;
            }
            return 0;
        }

        /// <summary>
        /// Create an Action callback that can be used for setting the trace id on the AWS SDK for .NET if the SDK is present.
        /// If the AWS .NET SDK is not found then null is returned.
        /// </summary>
        /// <returns></returns>
        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026",
            Justification = "Loading the type is okay to fail if the user is not using the AWS SDK for .NET or it is an old version. If they are using an SDK with the SDKTaskContext the SDK has the attributes to avoid the Set method being trimmed.")]
        internal static Action<string> FindAWSSDKTraceIdSetter(IEnvironmentVariables environmentVariables)
        {
            if (!Utils.IsUsingMultiConcurrency(environmentVariables))
                return null;

            // Since the AWSSDK.Core is strongly named we need to check the assembly version for
            // both V3 and V4. The assembly version is only changed on major releases.
            // In V3 the assembly version was updated to 3.3.0.0 when .NET Core support was added and then was never
            // updated again that major version.
            var sdkTaskContextType = Type.GetType("Amazon.Runtime.SDKTaskContext, AWSSDK.Core, Version=4.0.0.0, Culture=neutral, PublicKeyToken=885c28607f98e604") ??
                Type.GetType("Amazon.Runtime.SDKTaskContext, AWSSDK.Core, Version=3.3.0.0, Culture=neutral, PublicKeyToken=885c28607f98e604");

            if (sdkTaskContextType == null)
                return null;

            var defaultProperty = sdkTaskContextType.GetProperty("Default", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (defaultProperty == null)
                return null;

            var defaultInstance = defaultProperty.GetValue(null);
            if (defaultInstance == null)
                return null;

            var setMethod = sdkTaskContextType.GetMethod("Set", new Type[] { typeof(string), typeof(object) });
            if (setMethod == null)
                return null;

            return (string traceId) =>
            {
                setMethod.Invoke(defaultInstance, new object[] { "_X_AMZN_TRACE_ID", traceId });
            };
        }
    }
}
