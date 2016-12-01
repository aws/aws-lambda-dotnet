using Microsoft.Extensions.Logging;
using System;

namespace Microsoft.Extensions.Logging
{
    internal class LambdaILoggerProvider : ILoggerProvider
    {
        // Private fields
        private readonly LambdaLoggerOptions _options;

        // Constructor
        public LambdaILoggerProvider(LambdaLoggerOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            _options = options;
        }

        // Interface methods
        public ILogger CreateLogger(string categoryName)
        {
            return new LambdaILogger(categoryName, _options);
        }
        public void Dispose()
        {
        }
    }
}
