using Amazon.Lambda.Logging.AspNetCore;
using System;
using System.Collections.Concurrent;

namespace Microsoft.Extensions.Logging
{
    /// <summary>
    /// The ILoggerProvider implementation that is added to the ASP.NET Core logging system to create loggers
    /// that will send the messages to the CloudWatch LogGroup associated with this Lambda function.
    /// </summary>
    internal class LambdaILoggerProvider : ILoggerProvider, ISupportExternalScope
    {
        // Private fields
        private readonly LambdaLoggerOptions _options;
        private IExternalScopeProvider _scopeProvider;
        private readonly ConcurrentDictionary<string, LambdaILogger> _loggers;

        // Constants
        private const string DEFAULT_CATEGORY_NAME = "Default";

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
            _loggers = new ConcurrentDictionary<string, LambdaILogger>();
            _scopeProvider = options.IncludeScopes ? new LoggerExternalScopeProvider() : NullExternalScopeProvider.Instance;
        }

        /// <summary>
        /// Creates the logger with the specified category.
        /// </summary>
        /// <param name="categoryName"></param>
        /// <returns></returns>
        public ILogger CreateLogger(string categoryName)
        {
            var name = string.IsNullOrEmpty(categoryName) ? DEFAULT_CATEGORY_NAME : categoryName;

            return _loggers.GetOrAdd(name, loggerName => new LambdaILogger(name, _options)
            {
                ScopeProvider = _scopeProvider
            });
        }

        /// <inheritdoc />
        public void SetScopeProvider(IExternalScopeProvider scopeProvider)
        {
            _scopeProvider = scopeProvider;

            foreach (var logger in _loggers)
            {
                logger.Value.ScopeProvider = _scopeProvider;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void Dispose()
        {
        }
    }
}
