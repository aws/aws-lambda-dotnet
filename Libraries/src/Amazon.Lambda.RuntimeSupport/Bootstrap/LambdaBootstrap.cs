﻿/*
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
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport.Bootstrap;
using Amazon.Lambda.RuntimeSupport.Helpers;

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
        /// <summary>
        /// The Lambda container freezes the process at a point where an HTTP request is in progress.
        /// We need to make sure we don't timeout waiting for the next invocation.
        /// </summary>
        private static readonly TimeSpan RuntimeApiHttpTimeout = TimeSpan.FromHours(12);

        private LambdaBootstrapInitializer _initializer;
        private LambdaBootstrapHandler _handler;
        private bool _ownsHttpClient;
        private InternalLogger _logger = InternalLogger.GetDefaultLogger();

        private HttpClient _httpClient;
        private LambdaBootstrapConfiguration _configuration;
        internal IRuntimeApiClient Client { get; set; }

        /// <summary>
        /// Create a LambdaBootstrap that will call the given initializer and handler.
        /// </summary>
        /// <param name="httpClient">The HTTP client to use with the Lambda runtime.</param>
        /// <param name="handler">Delegate called for each invocation of the Lambda function.</param>
        /// <param name="initializer">Delegate called to initialize the Lambda function.  If not provided the initialization step is skipped.</param>
        /// <returns></returns>
        public LambdaBootstrap(HttpClient httpClient, LambdaBootstrapHandler handler, LambdaBootstrapInitializer initializer = null)
            : this(httpClient, handler, initializer, ownsHttpClient: false)
        { }

        /// <summary>
        /// Create a LambdaBootstrap that will call the given initializer and handler.
        /// </summary>
        /// <param name="handler">Delegate called for each invocation of the Lambda function.</param>
        /// <param name="initializer">Delegate called to initialize the Lambda function.  If not provided the initialization step is skipped.</param>
        /// <returns></returns>
        public LambdaBootstrap(LambdaBootstrapHandler handler, LambdaBootstrapInitializer initializer = null)
            : this(ConstructHttpClient(), handler, initializer, ownsHttpClient: true )
        { }

        /// <summary>
        /// Create a LambdaBootstrap that will call the given handler, initializer and options.
        /// </summary>
        /// <param name="handler">Delegate called for each invocation of the Lambda function.</param>
        /// <param name="lambdaBootstrapOptions">Lambda bootstrap configuration options.</param>
        /// <param name="initializer">Delegate called to initialize the Lambda function.  If not provided the initialization step is skipped.</param>
        public LambdaBootstrap(LambdaBootstrapHandler handler, LambdaBootstrapOptions lambdaBootstrapOptions, LambdaBootstrapInitializer initializer = null)
            : this(ConstructHttpClient(), handler, initializer, ownsHttpClient: true, lambdaBootstrapOptions: lambdaBootstrapOptions )
        { }

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
        /// Create a LambdaBootstrap that will call the given handler, initializer and options.
        /// </summary>
        /// <param name="handlerWrapper">The HandlerWrapper to call for each invocation of the Lambda function.</param>
        /// <param name="lambdaBootstrapOptions">Lambda bootstrap configuration options.</param>
        /// <param name="initializer">Delegate called to initialize the Lambda function.  If not provided the initialization step is skipped.</param>
        public LambdaBootstrap(HandlerWrapper handlerWrapper, LambdaBootstrapOptions lambdaBootstrapOptions, LambdaBootstrapInitializer initializer = null)
            : this(handlerWrapper.Handler, lambdaBootstrapOptions, initializer)
        { }

        /// <summary>
        /// Create a LambdaBootstrap that will call the given initializer and handler.
        /// </summary>
        /// <param name="httpClient">The HTTP client to use with the Lambda runtime.</param>
        /// <param name="handlerWrapper">The HandlerWrapper to call for each invocation of the Lambda function.</param>
        /// <param name="initializer">Delegate called to initialize the Lambda function.  If not provided the initialization step is skipped.</param>
        /// <returns></returns>
        public LambdaBootstrap(HttpClient httpClient, HandlerWrapper handlerWrapper, LambdaBootstrapInitializer initializer = null)
            : this(httpClient, handlerWrapper.Handler, initializer, ownsHttpClient: false)
        { }

        /// <summary>
        /// Create a LambdaBootstrap that will call the given handler, initializer and options.
        /// </summary>
        /// <param name="httpClient">The HTTP client to use with the Lambda runtime.</param>
        /// <param name="handlerWrapper">The HandlerWrapper to call for each invocation of the Lambda function.</param>
        /// <param name="lambdaBootstrapOptions">Lambda bootstrap configuration options.</param>
        /// <param name="initializer">Delegate called to initialize the Lambda function.  If not provided the initialization step is skipped.</param>
        public LambdaBootstrap(HttpClient httpClient, HandlerWrapper handlerWrapper, LambdaBootstrapOptions lambdaBootstrapOptions, LambdaBootstrapInitializer initializer = null)
            : this(httpClient, handlerWrapper.Handler, initializer, ownsHttpClient: false, lambdaBootstrapOptions: lambdaBootstrapOptions)
        { }

        /// <summary>
        /// Create a LambdaBootstrap that will call the given initializer and handler with custom configuration.
        /// </summary>
        /// <param name="handler">Delegate called for each invocation of the Lambda function.</param>
        /// <param name="initializer">Delegate called to initialize the Lambda function.  If not provided the initialization step is skipped.</param>
        /// <param name="configuration"> Get configuration to check if Invoke is with Pre JIT or SnapStart enabled </param>
        /// <returns></returns>
        internal LambdaBootstrap(LambdaBootstrapHandler handler,
            LambdaBootstrapInitializer initializer,
            LambdaBootstrapConfiguration configuration) : this(ConstructHttpClient(), handler, initializer, false, configuration)
        { }

        /// <summary>
        /// Create a LambdaBootstrap that will call the given initializer and handler.
        /// </summary>
        /// <param name="httpClient">The HTTP client to use with the Lambda runtime.</param>
        /// <param name="handler">Delegate called for each invocation of the Lambda function.</param>
        /// <param name="initializer">Delegate called to initialize the Lambda function.  If not provided the initialization step is skipped.</param>
        /// <param name="ownsHttpClient">Whether the instance owns the HTTP client and should dispose of it.</param>
        /// <param name="configuration"> Get configuration to check if Invoke is with Pre JIT or SnapStart enabled </param>
        /// <param name="lambdaBootstrapOptions">Lambda bootstrap configuration options.</param>
        private LambdaBootstrap(HttpClient httpClient, LambdaBootstrapHandler handler, LambdaBootstrapInitializer initializer, bool ownsHttpClient, LambdaBootstrapConfiguration configuration = null, LambdaBootstrapOptions lambdaBootstrapOptions = null)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
            _ownsHttpClient = ownsHttpClient;
            _initializer = initializer;
            _httpClient.Timeout = RuntimeApiHttpTimeout;
            Client = new RuntimeApiClient(new SystemEnvironmentVariables(), _httpClient, lambdaBootstrapOptions);
            _configuration = configuration ?? LambdaBootstrapConfiguration.GetDefaultConfiguration();
        }

        /// <summary>
        /// Run the initialization Func if provided.
        /// Then run the invoke loop, calling the handler for each invocation.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns>A Task that represents the operation.</returns>
#if NET8_0_OR_GREATER
        [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026",
            Justification = "Unreferenced code paths are excluded when RuntimeFeature.IsDynamicCodeSupported is false.")]
#endif

        public async Task RunAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
#if NET8_0_OR_GREATER
            AdjustMemorySettings();
#endif

            if (_configuration.IsCallPreJit)
            {
                this._logger.LogInformation("PreJit: CultureInfo");
                UserCodeInit.LoadStringCultureInfo();

                this._logger.LogInformation("PreJit: Amazon.Lambda.Core");
                UserCodeInit.PreJitAssembly(typeof(Amazon.Lambda.Core.ILambdaContext).Assembly);
            }

            // For local debugging purposes this environment variable can be set to run a Lambda executable assembly and process one event
            // and then shut down cleanly. Useful for profiling or running local tests with the .NET Lambda Test Tool. This environment
            // variable should never be set when function is deployed to Lambda.
            var runOnce = string.Equals(Environment.GetEnvironmentVariable(Constants.ENVIRONMENT_VARIABLE_AWS_LAMBDA_DOTNET_DEBUG_RUN_ONCE), "true", StringComparison.OrdinalIgnoreCase);


            if (_initializer != null && !(await InitializeAsync()))
            {
                return;
            }
#if NET8_0_OR_GREATER
            // Check if Initialization type is SnapStart, and invoke the snapshot restore logic.
            if (_configuration.IsInitTypeSnapstart)
            {
                InternalLogger.GetDefaultLogger().LogInformation($"In LambdaBootstrap, Initializing with SnapStart.");

                object registry = null;
                try
                {
                    registry = SnapstartHelperCopySnapshotCallbacksIsolated.CopySnapshotCallbacks();
                }
                catch (TypeLoadException ex)
                {
                    Client.ConsoleLogger.FormattedWriteLine(
                        Amazon.Lambda.RuntimeSupport.Helpers.LogLevelLoggerWriter.LogLevel.Error.ToString(),
                        $"Failed to retrieve snapshot hooks from Amazon.Lambda.Core.SnapshotRestore, " +
                        $"this can be fixed by updating the version of Amazon.Lambda.Core: {ex}",
                        null);
                }
                // no exceptions in calling SnapStart hooks or /restore/next RAPID endpoint
                if (!(await SnapstartHelperInitializeWithSnapstartIsolatedAsync.InitializeWithSnapstartAsync(Client,
                        registry)))
                {
                    return;
                };
            }
#endif

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await InvokeOnceAsync(cancellationToken);
                    if(runOnce)
                    {
                        _logger.LogInformation("Exiting Lambda processing loop because the run once environment variable was set.");
                        return;
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // Loop cancelled
                }
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
                WriteUnhandledExceptionToLog(exception);
                await Client.ReportInitializationErrorAsync(exception);
#if NET8_0_OR_GREATER
                if (_configuration.IsInitTypeSnapstart)
                {
                    System.Environment.Exit(1); // This needs to be non-zero for Lambda Sandbox to know that Runtime client encountered an exception
                }
#endif
                throw;
            }
        }

        internal async Task InvokeOnceAsync(CancellationToken cancellationToken = default)
        {
            this._logger.LogInformation($"Starting InvokeOnceAsync");
            using (var invocation = await Client.GetNextInvocationAsync(cancellationToken))
            {
                InvocationResponse response = null;
                bool invokeSucceeded = false;

                try
                {
                    this._logger.LogInformation($"Starting invoking handler");
                    response = await _handler(invocation);
                    invokeSucceeded = true;
                }
                catch (Exception exception)
                {
                    WriteUnhandledExceptionToLog(exception);
                    await Client.ReportInvocationErrorAsync(invocation.LambdaContext.AwsRequestId, exception);
                }
                finally
                {
                    this._logger.LogInformation($"Finished invoking handler");
                }

                if (invokeSucceeded)
                {
                    this._logger.LogInformation($"Starting sending response");
                    try
                    {
                        await Client.SendResponseAsync(invocation.LambdaContext.AwsRequestId, response?.OutputStream);
                    }
                    finally
                    {
                        if (response != null && response.DisposeOutputStream)
                        {
                            response.OutputStream?.Dispose();
                        }

                        this._logger.LogInformation($"Finished sending response");
                    }
                }
                this._logger.LogInformation($"Finished InvokeOnceAsync");
            }
        }

        /// <summary>
        /// Utility method for creating an HttpClient used by LambdaBootstrap to interact with the Lambda Runtime API.
        /// </summary>
        /// <returns></returns>
        public static HttpClient ConstructHttpClient()
        {
            var dotnetRuntimeVersion = new DirectoryInfo(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory()).Name;
            if (dotnetRuntimeVersion == "/")
            {
                dotnetRuntimeVersion = "unknown";
            }
            var amazonLambdaRuntimeSupport = typeof(LambdaBootstrap).Assembly.GetName().Version;

#if NET6_0_OR_GREATER
            // Create the SocketsHttpHandler directly to avoid spending cold start time creating the wrapper HttpClientHandler
            var handler = new SocketsHttpHandler
            {
                // Fix for https://github.com/aws/aws-lambda-dotnet/issues/1231. HttpClient by default supports only ASCII characters in headers. Changing it to allow UTF8 characters.
                RequestHeaderEncodingSelector = delegate { return System.Text.Encoding.UTF8; }
            };

            // If we are running in an AOT environment, mark it as such.
            var userAgentString = NativeAotHelper.IsRunningNativeAot() ? $"aws-lambda-dotnet/{dotnetRuntimeVersion}-{amazonLambdaRuntimeSupport}-aot"
                : $"aws-lambda-dotnet/{dotnetRuntimeVersion}-{amazonLambdaRuntimeSupport}";

            var client = new HttpClient(handler);
#else
            var userAgentString = $"aws-lambda-dotnet/{dotnetRuntimeVersion}-{amazonLambdaRuntimeSupport}";
            var client = new HttpClient();
#endif
            client.DefaultRequestHeaders.Add("User-Agent", userAgentString);
            return client;
        }

        private void WriteUnhandledExceptionToLog(Exception exception)
        {
#if NET6_0_OR_GREATER
            Client.ConsoleLogger.FormattedWriteLine(Amazon.Lambda.RuntimeSupport.Helpers.LogLevelLoggerWriter.LogLevel.Error.ToString(), exception, null);
#else
            Console.Error.WriteLine(exception);
#endif
        }

#if NET8_0_OR_GREATER
        /// <summary>
        /// The .NET runtime does not recognize the memory limits placed by Lambda via Lambda's cgroups. This method is run during startup to inform the
        /// .NET runtime the max memory configured for Lambda function. The max memory can be determined using the AWS_LAMBDA_FUNCTION_MEMORY_SIZE environment variable
        /// which has the memory in MB.
        ///
        /// For additional context on setting the heap size refer to this GitHub issue:
        /// https://github.com/dotnet/runtime/issues/70601
        /// </summary>
        private void AdjustMemorySettings()
        {
            try
            {
                // Check the environment variable to see if the user has opted out of RuntimeSupport adjusting
                // the heap memory limit to match the Lambda configured environment. This can be useful for situations
                // where the Lambda environment is being emulated for testing and more then just single Lambda function
                // is running in the process. For example running a test runner over a series of Lambda emulated invocations.
                var value = Environment.GetEnvironmentVariable(Constants.ENVIRONMENT_VARIABLE_DISABLE_HEAP_MEMORY_LIMIT);
                if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
                    return;


                int lambdaMemoryInMb;
                if (!int.TryParse(Environment.GetEnvironmentVariable(LambdaEnvironment.EnvVarFunctionMemorySize), out lambdaMemoryInMb))
                    return;

                ulong memoryInBytes = (ulong)lambdaMemoryInMb * LambdaEnvironment.OneMegabyte;

                // If the user has already configured the hard heap limit to something lower then is available
                // then make no adjustments to honor the user's setting.
                if ((ulong)GC.GetGCMemoryInfo().TotalAvailableMemoryBytes < memoryInBytes)
                    return;

                AppContext.SetData("GCHeapHardLimit", memoryInBytes);

#pragma warning disable CA2252
                GC.RefreshMemoryLimit();
#pragma warning disable CA2252
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Failed to communicate to the .NET runtime the amount of memory configured for the Lambda function via the AWS_LAMBDA_FUNCTION_MEMORY_SIZE environment variable.");
            }
        }
#endif

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing && _ownsHttpClient)
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
