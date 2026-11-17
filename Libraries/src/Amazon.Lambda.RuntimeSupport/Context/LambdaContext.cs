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
using System.Collections.Generic;
using System.Threading;
using Amazon.Lambda.RuntimeSupport.Helpers;

namespace Amazon.Lambda.RuntimeSupport
{
    internal class LambdaContext : ILambdaContext
    {
        internal static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private static readonly IDateTimeHelper DefaultDateTimeHelper = new DateTimeHelper();

        // Cached delegates and a placeholder for a null value, used to create the values that are parsed
        // from the headers only when they are first used without allocating for every invocation.
        private static readonly Func<string, CognitoIdentity> CreateCognitoIdentity = CognitoIdentity.FromJson;
        private static readonly Func<string, CognitoClientContext> CreateCognitoClientContext = CognitoClientContext.FromJson;
        private static readonly Func<string, IReadOnlyDictionary<string, string>> CreateW3C = W3CTraceContext.FromClientContextJson;
        private static readonly object NullValue = new object();

        private readonly LambdaEnvironment _lambdaEnvironment;
        private readonly RuntimeApiHeaders _runtimeApiHeaders;
        private readonly IDateTimeHelper _dateTimeHelper;
        private readonly long _deadlineMs;
        private readonly int _memoryLimitInMB;
        private readonly IConsoleLoggerWriter _consoleLogger;
        private object _cognitoIdentity;
        private object _cognitoClientContext;
        private object _w3c;

        public LambdaContext(RuntimeApiHeaders runtimeApiHeaders, LambdaEnvironment lambdaEnvironment, IConsoleLoggerWriter consoleLogger)
            : this(runtimeApiHeaders, lambdaEnvironment, DefaultDateTimeHelper, consoleLogger)
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
        }

        public string TraceId => _runtimeApiHeaders.TraceId;

        public string AwsRequestId => _runtimeApiHeaders.AwsRequestId;

        public IClientContext ClientContext => GetOrCreate(ref _cognitoClientContext, _runtimeApiHeaders.ClientContextJson, CreateCognitoClientContext);

        public string FunctionName => _lambdaEnvironment.FunctionName;

        public string FunctionVersion => _lambdaEnvironment.FunctionVersion;

        public ICognitoIdentity Identity => GetOrCreate(ref _cognitoIdentity, _runtimeApiHeaders.CognitoIdentityJson, CreateCognitoIdentity);

        public string InvokedFunctionArn => _runtimeApiHeaders.InvokedFunctionArn;

        public ILambdaLogger Logger => new LambdaConsoleLogger(_consoleLogger);

        public string LogGroupName => _lambdaEnvironment.LogGroupName;

        public string LogStreamName => _lambdaEnvironment.LogStreamName;

        public int MemoryLimitInMB => _memoryLimitInMB;

        public TimeSpan RemainingTime => TimeSpan.FromMilliseconds(_deadlineMs - (_dateTimeHelper.UtcNow - UnixEpoch).TotalMilliseconds);

        public string TenantId => _runtimeApiHeaders.TenantId;

        public IReadOnlyDictionary<string, string> W3C => GetOrCreate(ref _w3c, _runtimeApiHeaders.ClientContextJson, CreateW3C);

        /// <summary>
        /// The serializer the Lambda function registered with the runtime, surfaced via
        /// <see cref="ILambdaContext.Serializer"/>. Assigned per-invocation by
        /// <see cref="LambdaBootstrap"/>.
        /// </summary>
        public ILambdaSerializer Serializer { get; internal set; }

        internal IRuntimeApiHeaders RuntimeApiHeaders => _runtimeApiHeaders;

        /// <summary>
        /// The unique-per-invocation identifier echoed back to the Runtime API on the response
        /// and error calls for cross-wiring protection. Null when the Runtime API did not provide
        /// the <c>Lambda-Runtime-Invocation-Id</c> header, in which case nothing is echoed.
        /// </summary>
        internal string InvocationId => _runtimeApiHeaders.InvocationId;

        /// <summary>
        /// Gets the value stored in the specified location, creating it from the specified JSON if it has not been
        /// created yet. The value is created from immutable header values, so if it is created concurrently the
        /// first value to be stored is the one that is returned to all callers.
        /// </summary>
        private static T GetOrCreate<T>(ref object location, string json, Func<string, T> factory)
            where T : class
        {
            var value = Volatile.Read(ref location);

            if (value == null)
            {
                value = (object)factory(json) ?? NullValue;
                value = Interlocked.CompareExchange(ref location, value, null) ?? value;
            }

            return ReferenceEquals(value, NullValue) ? null : (T)value;
        }
    }
}
