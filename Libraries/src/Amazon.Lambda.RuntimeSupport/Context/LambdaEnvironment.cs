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
using System.Linq;
using System.Reflection;

namespace Amazon.Lambda.RuntimeSupport
{
    /// <summary>
    /// Provides access to Environment Variables set by the Lambda runtime environment.
    /// </summary>
    public class LambdaEnvironment
    {
        internal const string EnvVarExecutionEnvironment = "AWS_EXECUTION_ENV";
        internal const string EnvVarFunctionMemorySize = "AWS_LAMBDA_FUNCTION_MEMORY_SIZE";
        internal const string EnvVarFunctionName = "AWS_LAMBDA_FUNCTION_NAME";
        internal const string EnvVarFunctionVersion = "AWS_LAMBDA_FUNCTION_VERSION";
        internal const string EnvVarHandler = "_HANDLER";
        internal const string EnvVarLogGroupName = "AWS_LAMBDA_LOG_GROUP_NAME";
        internal const string EnvVarLogStreamName = "AWS_LAMBDA_LOG_STREAM_NAME";
        internal const string EnvVarServerHostAndPort = "AWS_LAMBDA_RUNTIME_API";
        internal const string EnvVarTraceId = "_X_AMZN_TRACE_ID";
        
        internal const string AwsLambdaDotnetCustomRuntime = "AWS_Lambda_dotnet_custom";
        internal const string AmazonLambdaRuntimeSupportMarker = "amazonlambdaruntimesupport";

        private IEnvironmentVariables _environmentVariables;

        public LambdaEnvironment() : this(new SystemEnvironmentVariables()) { }

        internal LambdaEnvironment(IEnvironmentVariables environmentVariables)
        {
            _environmentVariables = environmentVariables;

            FunctionMemorySize = environmentVariables.GetEnvironmentVariable(EnvVarFunctionMemorySize) as string;
            FunctionName = environmentVariables.GetEnvironmentVariable(EnvVarFunctionName) as string;
            FunctionVersion = environmentVariables.GetEnvironmentVariable(EnvVarFunctionVersion) as string;
            LogGroupName = environmentVariables.GetEnvironmentVariable(EnvVarLogGroupName) as string;
            LogStreamName = environmentVariables.GetEnvironmentVariable(EnvVarLogStreamName) as string;
            RuntimeServerHostAndPort = environmentVariables.GetEnvironmentVariable(EnvVarServerHostAndPort) as string;
            Handler = environmentVariables.GetEnvironmentVariable(EnvVarHandler) as string;

            SetExecutionEnvironment();
        }

        private void SetExecutionEnvironment()
        {

            var envValue = _environmentVariables.GetEnvironmentVariable(EnvVarExecutionEnvironment);
            if (!string.IsNullOrEmpty(envValue) && envValue.Equals(AwsLambdaDotnetCustomRuntime))
            {
                var assemblyVersion = typeof(LambdaBootstrap).Assembly
                    .GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), false)
                    .FirstOrDefault()
                    as AssemblyInformationalVersionAttribute;

                _environmentVariables.SetEnvironmentVariable(EnvVarExecutionEnvironment,
                    $"{envValue}_{AmazonLambdaRuntimeSupportMarker}_{assemblyVersion?.InformationalVersion}");
            }
        }

        internal void SetXAmznTraceId(string xAmznTraceId)
        {
            _environmentVariables.SetEnvironmentVariable(EnvVarTraceId, xAmznTraceId);
        }

        public string FunctionMemorySize { get; private set; }
        public string FunctionName { get; private set; }
        public string FunctionVersion { get; private set; }
        public string LogGroupName { get; private set; }
        public string LogStreamName { get; private set; }
        public string RuntimeServerHostAndPort { get; private set; }
        public string Handler { get; private set; }
        public string XAmznTraceId
        {
            get
            {
                return _environmentVariables.GetEnvironmentVariable(EnvVarTraceId);
            }
        }
        public string ExecutionEnvironment
        {
            get
            {
                return _environmentVariables.GetEnvironmentVariable(EnvVarExecutionEnvironment);
            }
        }
    }
}
