using Microsoft.Extensions.Logging.Abstractions.Internal;
using System;
using System.Collections.Generic;

namespace Microsoft.Extensions.Logging
{
    internal class LambdaILogger : ILogger
    {
        // Private fields
        private readonly string _categoryName;
        private readonly LambdaLoggerOptions _options;


        internal IExternalScopeProvider ScopeProvider { get; set; }

        // Constructor
        public LambdaILogger(string categoryName, LambdaLoggerOptions options)
        {
            _categoryName = categoryName;
            _options = options;
        }

        // ILogger methods
        public IDisposable BeginScope<TState>(TState state) => ScopeProvider?.Push(state) ?? NullScope.Instance;

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
            //  {[LogLevel] }{ => Scopes : }{Category: }{EventId: }MessageText {Exception}{\n}

            var components = new List<string>(4);
            if (_options.IncludeLogLevel)
            {
                components.Add($"[{logLevel}]");
            }

            GetScopeInformation(components, multiLine: _options.IncludeNewline);

            if (_options.IncludeCategory)
            {
                components.Add($"{_categoryName}:");
            }
            if (_options.IncludeEventId)
            {
                components.Add($"[{eventId}]:");
            }

            var text = formatter.Invoke(state, exception);
            components.Add(text);

            if (_options.IncludeException)
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

        private void GetScopeInformation(List<string> logMessageComponents, bool multiLine)
        {
            var scopeProvider = ScopeProvider;
            if (scopeProvider != null)
            {
                var initialCount = logMessageComponents.Count;

                scopeProvider.ForEachScope((scope, list) =>
                {
                    list.Add(scope.ToString());
                }, (logMessageComponents));

                if (logMessageComponents.Count > initialCount)
                {
                    logMessageComponents.Add("=>");
                }
            }
        }
    }
}
