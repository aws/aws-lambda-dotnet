// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;

namespace Amazon.Lambda.AspNetCoreServer.Internal;

internal class SnapStartEmptyLambdaContext : ILambdaContext, ICognitoIdentity, IClientContext
{
    private LambdaEnvironment _lambdaEnvironment = new();

    public string TraceId => string.Empty;
    public string AwsRequestId => string.Empty;
    public IClientContext ClientContext => this;
    public string FunctionName => _lambdaEnvironment.FunctionName;
    public string FunctionVersion => _lambdaEnvironment.FunctionVersion;
    public ICognitoIdentity Identity => this;
    public string InvokedFunctionArn => string.Empty;
    public ILambdaLogger Logger => null;
    public string LogGroupName => _lambdaEnvironment.LogGroupName;
    public string LogStreamName => _lambdaEnvironment.LogStreamName;
    public int MemoryLimitInMB => 128;
    public TimeSpan RemainingTime => TimeSpan.FromMilliseconds(100);
    public string IdentityId { get; }
    public string IdentityPoolId { get; }
    public IDictionary<string, string> Environment { get; } = new Dictionary<string, string>();
    public IClientApplication Client { get; }
    public IDictionary<string, string> Custom { get; } = new Dictionary<string, string>();
}
