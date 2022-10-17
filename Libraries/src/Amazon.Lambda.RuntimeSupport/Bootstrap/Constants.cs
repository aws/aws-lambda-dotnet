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

        internal const string ENVIRONMENT_VARIABLE_AWS_LAMBDA_DOTNET_PREJIT = "AWS_LAMBDA_DOTNET_PREJIT";
        internal const string ENVIRONMENT_VARIABLE_AWS_LAMBDA_INITIALIZATION_TYPE = "AWS_LAMBDA_INITIALIZATION_TYPE";
        internal const string ENVIRONMENT_VARIABLE_LANG = "LANG";
        internal const string ENVIRONMENT_VARIABLE_TELEMETRY_LOG_FD = "_LAMBDA_TELEMETRY_LOG_FD";
        internal const string AWS_LAMBDA_INITIALIZATION_TYPE_PC = "provisioned-concurrency";
        internal const string AWS_LAMBDA_INITIALIZATION_TYPE_ON_DEMAND = "on-demand";

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