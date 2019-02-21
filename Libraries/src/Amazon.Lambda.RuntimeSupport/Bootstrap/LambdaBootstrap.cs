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
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Amazon.Lambda.RuntimeSupport
{
    public delegate Task<InvocationResponse> LambdaBootstrapHandler(InvocationRequest invocation);
    public delegate Task<bool> LambdaBootstrapInitializer();

    /// <summary>
    /// Class to communicate with the Lambda Runtime API, handle initialization,
    /// and run the invoke loop for an AWS Lambda function
    /// </summary>
    public class LambdaBootstrap : IDisposable
    {
        internal const string TraceIdEnvVar = "_X_AMZN_TRACE_ID";

        private LambdaBootstrapInitializer _initializer;
        private LambdaBootstrapHandler _handler;

        private HttpClient _httpClient;
        internal IRuntimeApiClient Client { get; set; }
        internal IEnvironmentVariables EnvironmentVariables { get; set; }

        /// <summary>
        /// Create a LambdaBootstrap that will call the given initializer and handler.
        /// </summary>
        /// <param name="handler">Delegate called for each invocation of the Lambda function.</param>
        /// <param name="initializer">Delegate called to initialize the Lambda function.  If not provided the initialization step is skipped.</param>
        /// <returns></returns>
        public LambdaBootstrap(LambdaBootstrapHandler handler, LambdaBootstrapInitializer initializer = null)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
            _initializer = initializer;
            _httpClient = new HttpClient();
            Client = new RuntimeApiClient(_httpClient);
            EnvironmentVariables = new SystemEnvironmentVariables();
        }

        /// <summary>
        /// Create a LambdaBootstrap that will call the given initializer and handler.
        /// </summary>
        /// <param name="handlerWrapper">The HandlerWrapper to call for each invocation of the Lambda function.</param>
        /// <param name="initializer">Delegate called to initialize the Lambda function.  If not provided the initialization step is skipped.</param>
        /// <returns></returns>
        public LambdaBootstrap(HandlerWrapper handlerWrapper, LambdaBootstrapInitializer initializer = null)
            : this(handlerWrapper.Handler, initializer)
        { }

        /// <summary>
        /// Run the initialization Func if provided.
        /// Then run the invoke loop, calling the handler for each invocation.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns>A Task that represents the operation.</returns>
        public async Task RunAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            bool doStartInvokeLoop = _initializer == null || await InitializeAsync();

            while (doStartInvokeLoop && !cancellationToken.IsCancellationRequested)
            {
                await InvokeOnceAsync();
            }
        }

        internal async Task<bool> InitializeAsync()
        {
            try
            {
                return await _initializer();
            }
            catch (Exception exception)
            {
                await Client.ReportInitializationErrorAsync(exception);
                throw;
            }
        }

        internal async Task InvokeOnceAsync()
        {
            using (var invocation = await Client.GetNextInvocationAsync())
            {
                // cast to the internal LambdaContext type because ILambdaContext doesn't include TraceId (yet)
                var traceId = ((LambdaContext)invocation.LambdaContext).TraceId;

                // set environment variable so that if the function uses the XRay client it will work correctly
                EnvironmentVariables.SetEnvironmentVariable(TraceIdEnvVar, traceId);

                InvocationResponse response = null;
                try
                {
                    response = await _handler(invocation);
                    await Client.SendResponseAsync(invocation.LambdaContext.AwsRequestId, response?.OutputStream);
                }
                catch (Exception exception)
                {
                    await Client.ReportInvocationErrorAsync(invocation.LambdaContext.AwsRequestId, exception);
                }
                finally
                {
                    if (response != null && response.DisposeOutputStream)
                    {
                        response.OutputStream?.Dispose();
                    }
                }
            }
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _httpClient?.Dispose();
                }
                disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }
        #endregion
    }
}
