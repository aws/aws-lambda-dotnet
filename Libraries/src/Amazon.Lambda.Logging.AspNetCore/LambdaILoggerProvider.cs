using Microsoft.Extensions.Logging;
using System;

namespace Microsoft.Extensions.Logging
{
    /// <summary>
    /// The ILoggerProvider implementation that is added to the ASP.NET Core logging system to create loggers
    /// that will send the messages to the CloudWatch LogGroup associated with this Lambda function.
    /// </summary>
    internal class LambdaILoggerProvider : ILoggerProvider
    {
        // Private fields
        private readonly LambdaLoggerOptions _options;

        /// <summary>
        /// Creates the provider
        /// </summary>
        /// <param name="options"></param>
        public LambdaILoggerProvider(LambdaLoggerOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            _options = options;
        }

        /// <summary>
        /// Creates the logger with the specified category.
        /// </summary>
        /// <param name="categoryName"></param>
        /// <returns></returns>
        public ILogger CreateLogger(string categoryName)
        {
            return new LambdaILogger(categoryName, _options);
        }

        /// <summary>
        /// 
        /// </summary>
        public void Dispose()
        {
        }
    }
}
