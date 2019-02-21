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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Amazon.Lambda.RuntimeSupport
{
    /// <summary>
    /// Client to call the AWS Lambda Runtime API.
    /// </summary>
    public class RuntimeApiClient : IRuntimeApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly IInternalRuntimeApiClient _internalClient;
        private readonly LambdaEnvironment _lambdaEnvironment;

        internal Func<Exception, ExceptionInfo> ExceptionConverter { set; get; }
        internal IEnvironmentVariables EnvironmentVariables { get; private set;  }

        /// <summary>
        /// Create a new RuntimeApiClient
        /// </summary>
        /// <param name="httpClient">The HttpClient to use to communicate with the Runtime API.</param>
        public RuntimeApiClient(HttpClient httpClient)
            : this(new SystemEnvironmentVariables(), httpClient)
        {
        }

        internal RuntimeApiClient(IEnvironmentVariables environmentVariables, HttpClient httpClient)
        {
            ExceptionConverter = ExceptionInfo.GetExceptionInfo;
            EnvironmentVariables = environmentVariables;
            _httpClient = httpClient;
            _lambdaEnvironment = new LambdaEnvironment(environmentVariables);
            var internalClient = new InternalRuntimeApiClient(httpClient);
            internalClient.BaseUrl = "http://" + _lambdaEnvironment.RuntimeServerHostAndPort + internalClient.BaseUrl;
            _internalClient = internalClient;
        }

        internal RuntimeApiClient(IEnvironmentVariables environmentVariables, IInternalRuntimeApiClient internalClient)
        {
            _lambdaEnvironment = new LambdaEnvironment(environmentVariables);
            _internalClient = internalClient;
            ExceptionConverter = ExceptionInfo.GetExceptionInfo;
        }

        /// <summary>
        /// Report an initialization error as an asynchronous operation.
        /// </summary>
        /// <param name="exception">The exception to report.</param>
        /// <returns>A Task representing the asynchronous operation.</returns>
        public Task ReportInitializationErrorAsync(Exception exception)
        {
            if (exception == null)
                throw new ArgumentNullException(nameof(exception));

            return _internalClient.ErrorAsync(null, LambdaJsonExceptionWriter.WriteJson(ExceptionInfo.GetExceptionInfo(exception)));
        }

        /// <summary>
        /// Send an initialization error with a type string but no other information as an asynchronous operation.
        /// This can be used to directly control flow in Step Functions without creating an Exception class and throwing it.
        /// </summary>
        /// <param name="errorType">The type of the error to report to Lambda.  This does not need to be a .NET type name.</param>
        /// <returns>A Task representing the asynchronous operation.</returns>
        public Task ReportInitializationErrorAsync(string errorType)
        {
            if (errorType == null)
                throw new ArgumentNullException(nameof(errorType));

            return _internalClient.ErrorAsync(errorType, null);
        }

        /// <summary>
        /// Get the next function invocation from the Runtime API as an asynchronous operation.
        /// Completes when the next invocation is received.
        /// </summary>
        /// <returns>A Task representing the asynchronous operation.</returns>
        public async Task<InvocationRequest> GetNextInvocationAsync()
        {
            SwaggerResponse<Stream> response = await _internalClient.NextAsync(System.Threading.CancellationToken.None);

            var lambdaContext = new LambdaContext(new RuntimeApiHeaders(response.Headers), _lambdaEnvironment);
            return new InvocationRequest
            {
                InputStream = response.Result,
                LambdaContext = lambdaContext,
            };
        }

        /// <summary>
        /// Report an invocation error as an asynchronous operation.
        /// </summary>
        /// <param name="awsRequestId">The ID of the function request that caused the error.</param>
        /// <param name="exception">The exception to report.</param>
        /// <returns>A Task representing the asynchronous operation.</returns>
        public Task ReportInvocationErrorAsync(string awsRequestId, Exception exception)
        {
            if (awsRequestId == null)
                throw new ArgumentNullException(nameof(awsRequestId));

            if (exception == null)
                throw new ArgumentNullException(nameof(exception));

            return _internalClient.Error2Async(awsRequestId, null, LambdaJsonExceptionWriter.WriteJson(ExceptionInfo.GetExceptionInfo(exception)));
        }

        /// <summary>
        /// Send an initialization error with a type string but no other information as an asynchronous operation.
        /// This can  be used to directly control flow in Step Functions without creating an Exception class and throwing it.
        /// </summary>
        /// <param name="awsRequestId">The ID of the function request that caused the error.</param>
        /// <param name="errorType">The type of the error to report to Lambda.  This does not need to be a .NET type name.</param>
        /// <returns>A Task representing the asynchronous operation.</returns>
        public Task ReportInvocationErrorAsync(string awsRequestId, string errorType)
        {
            return _internalClient.Error2Async(awsRequestId, errorType, null);
        }

        /// <summary>
        /// Send a response to a function invocation to the Runtime API as an asynchronous operation.
        /// </summary>
        /// <param name="awsRequestId">The ID of the function request being responded to.</param>
        /// <param name="outputStream">The content of the response to the function invocation.</param>
        /// <returns></returns>
        public async Task SendResponseAsync(string awsRequestId, Stream outputStream)
        {
            await _internalClient.ResponseAsync(awsRequestId, outputStream, CancellationToken.None);
        }
    }
}