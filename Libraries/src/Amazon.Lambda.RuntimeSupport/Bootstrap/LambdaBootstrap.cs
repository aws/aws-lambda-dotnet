using Amazon.Lambda.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Amazon.Lambda.RuntimeSupport
{
    public delegate Task<Stream> LambdaBootstrapHandler(InvocationRequest invocation);
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
            Client = new RuntimeApiClient(new HttpClient());
            EnvironmentVariables = new SystemEnvironmentVariables();
        }

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

                try
                {
                    using (var outputStream = await _handler(invocation))
                    {
                        await Client.SendResponseAsync(invocation.LambdaContext.AwsRequestId, outputStream);
                    }
                }
                catch (Exception exception)
                {
                    await Client.ReportInvocationErrorAsync(invocation.LambdaContext.AwsRequestId, exception);
                }
            }
        }

        public void Dispose()
        {
            var disposableClient = Client as IDisposable;
            if (disposableClient != null)
            {
                disposableClient.Dispose();
            }
        }
    }
}
