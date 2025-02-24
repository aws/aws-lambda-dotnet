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
#if NET8_0_OR_GREATER
        [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("RuntimeSupportInitializer does not support trimming and is meant to be used in class library based Lambda functions.")]
#endif
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

            var userCodeLoader = new UserCodeLoader(_handler, _logger);
            var initializer = new UserCodeInitializer(userCodeLoader, _logger);
            using (var handlerWrapper = HandlerWrapper.GetHandlerWrapper(userCodeLoader.Invoke))
            using (var bootstrap = new LambdaBootstrap(handlerWrapper, _lambdaBootstrapOptions, initializer.InitializeAsync))
            {
                await bootstrap.RunAsync();
            }
        }
    }
}
