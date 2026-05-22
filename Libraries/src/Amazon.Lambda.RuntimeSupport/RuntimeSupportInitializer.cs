/*
 * Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
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

using Amazon.Lambda.RuntimeSupport.Bootstrap;
using Amazon.Lambda.RuntimeSupport.Helpers;
using System;
using System.Threading.Tasks;

namespace Amazon.Lambda.RuntimeSupport
{
    /// <summary>
    /// RuntimeSupportInitializer class responsible for initializing the UserCodeLoader and LambdaBootstrap given a function handler.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("RuntimeSupportInitializer does not support trimming and is meant to be used in class library based Lambda functions.")]
    public class RuntimeSupportInitializer
    {
        private readonly string _handler;
        private readonly LambdaBootstrapOptions _lambdaBootstrapOptions;
        private readonly InternalLogger _logger;
        private readonly RuntimeSupportDebugAttacher _debugAttacher;

        /// <summary>
        /// Class constructor that takes a Function Handler and initializes the class.
        /// </summary>
        public RuntimeSupportInitializer(string handler) : this(handler, new LambdaBootstrapOptions()) { }

        /// <summary>
        /// Class constructor that takes a Function Handler and Lambda Bootstrap Options and initializes the class.
        /// </summary>
        public RuntimeSupportInitializer(string handler, LambdaBootstrapOptions lambdaBootstrapOptions)
        {
            if (string.IsNullOrWhiteSpace(handler))
            {
                throw new ArgumentException("Cannot initialize RuntimeSupportInitializer with a null of empty Function Handler", nameof(handler));
            }

            _logger = InternalLogger.GetDefaultLogger();

            _handler = handler;
            _debugAttacher = new RuntimeSupportDebugAttacher();
            _lambdaBootstrapOptions = lambdaBootstrapOptions;
        }

        /// <summary>
        /// Initializes the UserCodeLoader using the Function Handler and runs LambdaBootstrap asynchronously.
        /// </summary>
        public async Task RunLambdaBootstrap()
        {
            await _debugAttacher.TryAttachDebugger();

            var environmentVariables = new SystemEnvironmentVariables();
            var userCodeLoader = new UserCodeLoader(environmentVariables, _handler, _logger);
            var initializer = new UserCodeInitializer(userCodeLoader, _logger);
            // Pre-declare so the wrapped initializer can reference it. The closure runs
            // later (inside bootstrap.RunAsync) by which time bootstrap is assigned.
            LambdaBootstrap bootstrap = null;
            // Wrap init to plumb the serializer ([assembly: LambdaSerializer]) onto the
            // bootstrap right after UserCodeLoader resolves it. The bootstrap then
            // surfaces it on ILambdaContext.Serializer for every invocation via the
            // Isolated shim.
            LambdaBootstrapInitializer wrappedInit = async () =>
            {
                var initResult = await initializer.InitializeAsync();
                if (initResult)
                {
                    bootstrap.SetSerializer(userCodeLoader.CustomerSerializerInstance as Amazon.Lambda.Core.ILambdaSerializer);
                }
                return initResult;
            };
            using (var handlerWrapper = HandlerWrapper.GetHandlerWrapper(userCodeLoader.Invoke))
            using (bootstrap = new LambdaBootstrap(
                        httpClient: null,
                        handler: handlerWrapper.Handler,
                        initializer: wrappedInit,
                        ownsHttpClient: true,
                        lambdaBootstrapOptions: _lambdaBootstrapOptions,
                        environmentVariables: environmentVariables))
            {
                await bootstrap.RunAsync();
            }
        }
    }
}
