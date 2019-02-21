using System;
using System.Collections.Generic;

namespace Microsoft.Extensions.Logging
{
    internal class LambdaILogger : ILogger
    {
        // Private fields
        private readonly string _categoryName;
        private readonly LambdaLoggerOptions _options;

        // Constants
        private const string DEFAULT_CATEGORY_NAME = "Default";

        // Constructor
        public LambdaILogger(string categoryName, LambdaLoggerOptions options)
        {
            _categoryName = string.IsNullOrEmpty(categoryName) ? DEFAULT_CATEGORY_NAME : categoryName;
            _options = options;
        }

        // ILogger methods
        public IDisposable BeginScope<TState>(TState state)
        {
            // No support for scopes at this point
            // https://docs.asp.net/en/latest/fundamentals/logging.html#scopes
            return new NoOpDisposable();
        }
        public bool IsEnabled(LogLevel logLevel)
        {
            return (
                _options.Filter == null ||
                _options.Filter(_categoryName, logLevel));
        }
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (formatter == null)
            {
                throw new ArgumentNullException(nameof(formatter));
            }

            if (!IsEnabled(logLevel))
            {
                return;
            }

            // Format of the logged text, optional components are in {}
            //  {[LogLevel] }{Category: }{EventId: }MessageText {Exception}{\n}

            var components = new List<string>(4);
            if (_options.IncludeLogLevel)
            {
                components.Add($"[{logLevel}]");
            }
            if (_options.IncludeCategory)
            {
                components.Add($"{_categoryName}:");
            }
			if (_options.IncludeEventId)
			{
				components.Add($"{eventId}:");
			}

			var text = formatter.Invoke(state, exception);
            components.Add(text);

			if(_options.IncludeException)
			{
				components.Add($"{exception}");
			}
			if (_options.IncludeNewline)
            {
                components.Add(Environment.NewLine);
            }

            var finalText = string.Join(" ", components);
            Amazon.Lambda.Core.LambdaLogger.Log(finalText);
        }

        // Private classes
        private class NoOpDisposable : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
