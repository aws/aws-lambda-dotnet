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

using Amazon.Lambda.RuntimeSupport.Helpers;
using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Lambda.RuntimeSupport.Bootstrap;

namespace Amazon.Lambda.RuntimeSupport
{
    /// <summary>
    /// Client to call the AWS Lambda Runtime API.
    /// </summary>
    public class RuntimeApiClient : IRuntimeApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly IInternalRuntimeApiClient _internalClient;

#if NET6_0_OR_GREATER
        private readonly IConsoleLoggerWriter _consoleLoggerRedirector;
#else
        private readonly IConsoleLoggerWriter _consoleLoggerRedirector;
#endif

        internal Func<Exception, ExceptionInfo> ExceptionConverter { get;  set; }
        internal LambdaEnvironment LambdaEnvironment { get; set; }

        /// <inheritdoc/>
        public IConsoleLoggerWriter ConsoleLogger => _consoleLoggerRedirector;

        /// <summary>
        /// Create a new RuntimeApiClient
        /// </summary>
        /// <param name="httpClient">The HttpClient to use to communicate with the Runtime API.</param>
        public RuntimeApiClient(HttpClient httpClient)
            : this(new SystemEnvironmentVariables(), httpClient)
        {
        }

        internal RuntimeApiClient(IEnvironmentVariables environmentVariables, HttpClient httpClient, LambdaBootstrapOptions lambdaBootstrapOptions = null)
        {
#if NET6_0_OR_GREATER
            _consoleLoggerRedirector = new LogLevelLoggerWriter(environmentVariables);
#else
            _consoleLoggerRedirector = new SimpleLoggerWriter(environmentVariables);
#endif

            ExceptionConverter = ExceptionInfo.GetExceptionInfo;
            _httpClient = httpClient;
            LambdaEnvironment = new LambdaEnvironment(environmentVariables, lambdaBootstrapOptions);
            var internalClient = new InternalRuntimeApiClient(httpClient);
            internalClient.BaseUrl = "http://" + LambdaEnvironment.RuntimeServerHostAndPort + internalClient.BaseUrl;
            _internalClient = internalClient;
        }

        internal RuntimeApiClient(IEnvironmentVariables environmentVariables, IInternalRuntimeApiClient internalClient, LambdaBootstrapOptions lambdaBootstrapOptions = null)
        {
            LambdaEnvironment = new LambdaEnvironment(environmentVariables, lambdaBootstrapOptions);
            _internalClient = internalClient;
            ExceptionConverter = ExceptionInfo.GetExceptionInfo;
        }

        /// <summary>
        /// Report an initialization error as an asynchronous operation.
        /// </summary>
        /// <param name="exception">The exception to report.</param>
        /// <param name="errorType">An optional errorType string that can be used to log higher-context error to customer instead of generic Runtime.Unknown by the Lambda Sandbox. </param>
        /// <param name="cancellationToken">The optional cancellation token to use.</param>
        /// <returns>A Task representing the asynchronous operation.</returns>
        public Task ReportInitializationErrorAsync(Exception exception, String errorType = null, CancellationToken cancellationToken = default)
        {
            if (exception == null)
                throw new ArgumentNullException(nameof(exception));

            return _internalClient.ErrorAsync(errorType, LambdaJsonExceptionWriter.WriteJson(ExceptionInfo.GetExceptionInfo(exception)), cancellationToken);
        }

        /// <summary>
        /// Send an initialization error with a type string but no other information as an asynchronous operation.
        /// This can be used to directly control flow in Step Functions without creating an Exception class and throwing it.
        /// </summary>
        /// <param name="errorType">The type of the error to report to Lambda. This does not need to be a .NET type name.</param>
        /// <param name="cancellationToken">The optional cancellation token to use.</param>
        /// <returns>A Task representing the asynchronous operation.</returns>
        public Task ReportInitializationErrorAsync(string errorType, CancellationToken cancellationToken = default)
        {
            if (errorType == null)
                throw new ArgumentNullException(nameof(errorType));

            return _internalClient.ErrorAsync(errorType, null, cancellationToken);
        }

        /// <summary>
        /// Get the next function invocation from the Runtime API as an asynchronous operation.
        /// Completes when the next invocation is received.
        /// </summary>
        /// <param name="cancellationToken">The optional cancellation token to use to stop listening for the next invocation.</param>
        /// <returns>A Task representing the asynchronous operation.</returns>
        public async Task<InvocationRequest> GetNextInvocationAsync(CancellationToken cancellationToken = default)
        {
            SwaggerResponse<Stream> response = await _internalClient.NextAsync(cancellationToken);

            var headers = new RuntimeApiHeaders(response.Headers);
            var lambdaContext = new LambdaContext(headers, LambdaEnvironment, _consoleLoggerRedirector);
            return new InvocationRequest
            {
                InputStream = response.Result,
                LambdaContext = lambdaContext
            };
        }

        /// <summary>
        /// Report an invocation error as an asynchronous operation.
        /// </summary>
        /// <param name="awsRequestId">The ID of the function request that caused the error.</param>
        /// <param name="exception">The exception to report.</param>
        /// <param name="cancellationToken">The optional cancellation token to use.</param>
        /// <returns>A Task representing the asynchronous operation.</returns>
        public Task ReportInvocationErrorAsync(string awsRequestId, Exception exception, CancellationToken cancellationToken = default)
        {
            if (awsRequestId == null)
                throw new ArgumentNullException(nameof(awsRequestId));

            if (exception == null)
                throw new ArgumentNullException(nameof(exception));

            var exceptionInfo = ExceptionInfo.GetExceptionInfo(exception);

            var exceptionInfoJson = LambdaJsonExceptionWriter.WriteJson(exceptionInfo);
            var exceptionInfoXRayJson = LambdaXRayExceptionWriter.WriteJson(exceptionInfo);

            return _internalClient.ErrorWithXRayCauseAsync(awsRequestId, exceptionInfo.ErrorType, exceptionInfoJson, exceptionInfoXRayJson, cancellationToken);
        }

#if NET8_0_OR_GREATER

        /// <summary>
        ///  Triggers the snapshot to be taken, and then after resume, restores the lambda
        /// context from the Runtime API as an asynchronous operation when SnapStart is enabled.
        /// </summary>
        /// <param name="cancellationToken">The optional cancellation token to use.</param>
        /// <returns>A Task representing the asynchronous operation.</returns>
        public async Task RestoreNextInvocationAsync(CancellationToken cancellationToken = default)
        {
            await _internalClient.RestoreNextAsync(cancellationToken);
        }

        /// <summary>
        /// Report a restore error as an asynchronous operation when SnapStart is enabled.
        /// </summary>
        /// <param name="exception">The exception to report.</param>
        /// <param name="errorType">An optional errorType string that can be used to log higher-context error to customer instead of generic Runtime.Unknown by the Lambda Sandbox. </param>
        /// <param name="cancellationToken">The optional cancellation token to use.</param>
        /// <returns>A Task representing the asynchronous operation.</returns>
        public Task ReportRestoreErrorAsync(Exception exception, String errorType = null, CancellationToken cancellationToken = default)
        {
            if (exception == null)
                throw new ArgumentNullException(nameof(exception));

            return _internalClient.RestoreErrorAsync(errorType, LambdaJsonExceptionWriter.WriteJson(ExceptionInfo.GetExceptionInfo(exception)), cancellationToken);
        }
#endif


        /// <summary>
        /// Send a response to a function invocation to the Runtime API as an asynchronous operation.
        /// </summary>
        /// <param name="awsRequestId">The ID of the function request being responded to.</param>
        /// <param name="outputStream">The content of the response to the function invocation.</param>
        /// <param name="cancellationToken">The optional cancellation token to use.</param>
        /// <returns></returns>
        public async Task SendResponseAsync(string awsRequestId, Stream outputStream, CancellationToken cancellationToken = default)
        {
            await _internalClient.ResponseAsync(awsRequestId, outputStream, cancellationToken);
        }
    }
}
