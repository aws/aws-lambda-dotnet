// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using Amazon.Lambda.Core;

namespace Amazon.Lambda.AspNetCoreServer.Internal;

internal class SnapStartEmptyLambdaContext : ILambdaContext, ICognitoIdentity, IClientContext
{
    private static Dictionary<string, string> _environmentVariables = new();

    // Copied from Amazon.Lambda.RuntimeSupport.LambdaEnvironment to avoid adding
    // a reference to that project
    private const string EnvVarFunctionMemorySize = "AWS_LAMBDA_FUNCTION_MEMORY_SIZE";
    private const string EnvVarFunctionName = "AWS_LAMBDA_FUNCTION_NAME";
    private const string EnvVarFunctionVersion = "AWS_LAMBDA_FUNCTION_VERSION";
    private const string EnvVarLogGroupName = "AWS_LAMBDA_LOG_GROUP_NAME";
    private const string EnvVarLogStreamName = "AWS_LAMBDA_LOG_STREAM_NAME";
    

    static SnapStartEmptyLambdaContext()
    {
        AddEnvValue(EnvVarFunctionMemorySize, "128");
        AddEnvValue(EnvVarFunctionName, "fallbackFunctionName");
        AddEnvValue(EnvVarFunctionVersion, "0");
        AddEnvValue(EnvVarLogGroupName, "fallbackLogGroup");
        AddEnvValue(EnvVarLogStreamName, "fallbackLogStream");
    }

    private static void AddEnvValue(string envName, string fallback)
    {
        var val = System.Environment.GetEnvironmentVariable(envName);

        val = string.IsNullOrEmpty(val) ? fallback : val;

        _environmentVariables[envName] = val;
    }

    public SnapStartEmptyLambdaContext()
    {
        // clone the static environment variables into the local instance
        foreach (var k in _environmentVariables.Keys)
            Environment[k] = _environmentVariables[k];
    }


    public string TraceId => string.Empty;
    public string AwsRequestId => string.Empty;
    public IClientContext ClientContext => this;
    public string FunctionName => Environment[EnvVarFunctionName];
    public string FunctionVersion => Environment[EnvVarFunctionVersion];
    public ICognitoIdentity Identity => this;
    public string InvokedFunctionArn => string.Empty;
    public ILambdaLogger Logger => null;
    public string LogGroupName => Environment[EnvVarLogGroupName];
    public string LogStreamName => Environment[EnvVarLogStreamName];
    public int MemoryLimitInMB => int.Parse(Environment[EnvVarFunctionMemorySize]);
    public TimeSpan RemainingTime => TimeSpan.FromSeconds(5);
    public string IdentityId { get; }
    public string IdentityPoolId { get; }
    public IDictionary<string, string> Environment { get; } = new Dictionary<string, string>();
    public IClientApplication Client { get; }
    public IDictionary<string, string> Custom { get; } = new Dictionary<string, string>();
}
