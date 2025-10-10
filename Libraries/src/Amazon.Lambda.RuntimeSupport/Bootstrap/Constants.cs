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

using System.Reflection;

namespace Amazon.Lambda.RuntimeSupport.Bootstrap
{
    internal class Constants
    {
        // For local debugging purposes this environment variable can be set to run a Lambda executable assembly and process one event
        // and then shut down cleanly. Useful for profiling or running local tests with the .NET Lambda Test Tool. This environment
        // variable should never be set when function is deployed to Lambda.
        internal const string ENVIRONMENT_VARIABLE_AWS_LAMBDA_DOTNET_DEBUG_RUN_ONCE = "AWS_LAMBDA_DOTNET_DEBUG_RUN_ONCE";

        // Lambda Environment variable used to check if user has configured the function to run in multi concurrency mode.
        // To be in multi concurrency mode the environment has to exist and have an int value greater then 1.
        internal const string ENVIRONMENT_VARIABLE_AWS_LAMBDA_MAX_CONCURRENCY = "AWS_LAMBDA_MAX_CONCURRENCY";

        // .NET Lambda runtime specific environment variable used to override the number of .NET Tasks started
        // that will reach out to the Lambda Runtime API for new events to process. This environment variable is only
        // used if AWS_LAMBDA_MAX_CONCURRENCY environment variable is set.
        internal const string ENVIRONMENT_VARIABLE_AWS_LAMBDA_DOTNET_PROCESSING_TASKS = "AWS_LAMBDA_DOTNET_PROCESSING_TASKS";

        internal const string ENVIRONMENT_VARIABLE_DISABLE_HEAP_MEMORY_LIMIT = "AWS_LAMBDA_DOTNET_DISABLE_MEMORY_LIMIT_CHECK";

        internal const string ENVIRONMENT_VARIABLE_AWS_LAMBDA_DOTNET_PREJIT = "AWS_LAMBDA_DOTNET_PREJIT";
        internal const string ENVIRONMENT_VARIABLE_AWS_LAMBDA_INITIALIZATION_TYPE = "AWS_LAMBDA_INITIALIZATION_TYPE";
        internal const string ENVIRONMENT_VARIABLE_LANG = "LANG";
        internal const string ENVIRONMENT_VARIABLE_TELEMETRY_LOG_FD = "_LAMBDA_TELEMETRY_LOG_FD";
        internal const string AWS_LAMBDA_INITIALIZATION_TYPE_PC = "provisioned-concurrency";
        internal const string AWS_LAMBDA_INITIALIZATION_TYPE_ON_DEMAND = "on-demand";
        internal const string AWS_LAMBDA_INITIALIZATION_TYPE_SNAP_START = "snap-start";


        internal const string NET_RIC_LOG_LEVEL_ENVIRONMENT_VARIABLE = "AWS_LAMBDA_HANDLER_LOG_LEVEL";
        internal const string NET_RIC_LOG_FORMAT_ENVIRONMENT_VARIABLE = "AWS_LAMBDA_HANDLER_LOG_FORMAT";

        internal const string LAMBDA_LOG_LEVEL_ENVIRONMENT_VARIABLE = "AWS_LAMBDA_LOG_LEVEL";
        internal const string LAMBDA_LOG_FORMAT_ENVIRONMENT_VARIABLE = "AWS_LAMBDA_LOG_FORMAT";

        internal const string LAMBDA_LOG_FORMAT_JSON = "Json";

        internal const string LAMBDA_ERROR_TYPE_BEFORE_SNAPSHOT = "Runtime.BeforeSnapshotError";
        internal const string LAMBDA_ERROR_TYPE_AFTER_RESTORE = "Runtime.AfterRestoreError";

        // For testing purposes only, this command line arg can be used to specify the Lambda Runtime API endpoint
        // instead of using environment variables.
        internal const string CMDLINE_ARG_RUNTIME_API_CLIENT = "--runtime-api-endpoint";

        internal enum AwsLambdaDotNetPreJit
        {
            Never,
            Always,
            ProvisionedConcurrency
        }

        internal const BindingFlags DefaultFlags = BindingFlags.DeclaredOnly | BindingFlags.NonPublic | BindingFlags.Public
                                                   | BindingFlags.Instance | BindingFlags.Static;
    }
}
