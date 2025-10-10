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
using Amazon.Lambda.RuntimeSupport.Helpers;

namespace Amazon.Lambda.RuntimeSupport
{
    internal class LambdaContext : ILambdaContext
    {
        internal static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private readonly LambdaEnvironment _lambdaEnvironment;
        private readonly RuntimeApiHeaders _runtimeApiHeaders;
        private readonly IDateTimeHelper _dateTimeHelper;
        private readonly long _deadlineMs;
        private readonly int _memoryLimitInMB;
        private readonly Lazy<CognitoIdentity> _cognitoIdentityLazy;
        private readonly Lazy<CognitoClientContext> _cognitoClientContextLazy;
        private readonly IConsoleLoggerWriter _consoleLogger;

        public LambdaContext(RuntimeApiHeaders runtimeApiHeaders, LambdaEnvironment lambdaEnvironment, IConsoleLoggerWriter consoleLogger)
            : this(runtimeApiHeaders, lambdaEnvironment, new DateTimeHelper(), consoleLogger)
        {
        }

        public LambdaContext(RuntimeApiHeaders runtimeApiHeaders, LambdaEnvironment lambdaEnvironment, IDateTimeHelper dateTimeHelper, IConsoleLoggerWriter consoleLogger)
        {

            _lambdaEnvironment = lambdaEnvironment;
            _runtimeApiHeaders = runtimeApiHeaders;
            _dateTimeHelper = dateTimeHelper;
            _consoleLogger = consoleLogger;

            int.TryParse(_lambdaEnvironment.FunctionMemorySize, out _memoryLimitInMB);
            long.TryParse(_runtimeApiHeaders.DeadlineMs, out _deadlineMs);
            _cognitoIdentityLazy = new Lazy<CognitoIdentity>(() => CognitoIdentity.FromJson(runtimeApiHeaders.CognitoIdentityJson));
            _cognitoClientContextLazy = new Lazy<CognitoClientContext>(() => CognitoClientContext.FromJson(runtimeApiHeaders.ClientContextJson));
        }

        public string TraceId => _runtimeApiHeaders.TraceId;

        public string AwsRequestId => _runtimeApiHeaders.AwsRequestId;

        public IClientContext ClientContext => _cognitoClientContextLazy.Value;

        public string FunctionName => _lambdaEnvironment.FunctionName;

        public string FunctionVersion => _lambdaEnvironment.FunctionVersion;

        public ICognitoIdentity Identity => _cognitoIdentityLazy.Value;

        public string InvokedFunctionArn => _runtimeApiHeaders.InvokedFunctionArn;

        public ILambdaLogger Logger => new LambdaConsoleLogger(_consoleLogger);

        public string LogGroupName => _lambdaEnvironment.LogGroupName;

        public string LogStreamName => _lambdaEnvironment.LogStreamName;

        public int MemoryLimitInMB => _memoryLimitInMB;

        public TimeSpan RemainingTime => TimeSpan.FromMilliseconds(_deadlineMs - (_dateTimeHelper.UtcNow - UnixEpoch).TotalMilliseconds);

        public string TenantId => _runtimeApiHeaders.TenantId;

        internal IRuntimeApiHeaders RuntimeApiHeaders => _runtimeApiHeaders;
    }
}
