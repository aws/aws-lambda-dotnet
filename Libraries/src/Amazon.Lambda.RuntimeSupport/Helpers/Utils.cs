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
        public static bool IsRunningNativeAot()
        {
            // If dynamic code is not supported we are most likely running in an AOT environment. 
#if NET6_0_OR_GREATER
            return !RuntimeFeature.IsDynamicCodeSupported;
#else
            return false;
#endif

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
                    processingTaskCount = Math.Max(2, processorCount);
                }
            }

            return processingTaskCount;
        }

        /// <summary>
        /// Create an Action callback that can be used for setting the trace id on the AWS SDK for .NET if the SDK is present.
        /// If the AWS .NET SDK is not found then null is returned.
        /// </summary>
        /// <returns></returns>
#if NET8_0_OR_GREATER
        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026",
            Justification = "Loading the type is okay to fail if the user is not using the AWS SDK for .NET or it is an old version. If they are using an SDK with the SDKTaskContext the SDK has the attributes to avoid the Set method being trimmed.")]
#endif
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
