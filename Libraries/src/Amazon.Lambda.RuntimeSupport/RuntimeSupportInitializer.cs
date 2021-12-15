using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport.Bootstrap;
using Amazon.Lambda.RuntimeSupport.Helpers;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Amazon.Lambda.RuntimeSupport
{
    /// <summary>
    /// RuntimeSuportInitializer class responsible for initializing the UserCodeLoader and LambdaBootstrap given a function handler.
    /// </summary>
    public class RuntimeSupportInitializer
    {
        private readonly string _handler;
        private readonly InternalLogger _logger;
        private readonly RuntimeSupportDebugAttacher _debugAttacher;
        

        /// <summary>
        /// Class constructor that takes a Function Handler and initializes the class.
        /// </summary>
        public RuntimeSupportInitializer(string handler)
        {
            if (string.IsNullOrWhiteSpace(handler))
            {
                throw new ArgumentException("Cannot initialize RuntimeSupportInitializer with a null of empty Function Handler", nameof(handler));
            }

            _logger = InternalLogger.GetDefaultLogger();

            _handler = handler;
            _debugAttacher = new RuntimeSupportDebugAttacher();
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
            using (var bootstrap = new LambdaBootstrap(handlerWrapper, initializer.InitializeAsync))
            {
                await bootstrap.RunAsync();
            }
        }
    }
}
