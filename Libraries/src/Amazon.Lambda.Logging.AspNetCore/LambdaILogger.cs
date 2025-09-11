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
        public IDisposable BeginScope<TState>(TState state) => ScopeProvider?.Push(state) ?? new NoOpDisposable();

        public bool IsEnabled(LogLevel logLevel)
        {
            return (
                _options.Filter == null ||
                _options.Filter(_categoryName, logLevel));
        }

        /// <summary>
        /// The Log method called by the ILogger framework to log message to logger's target. In the Lambda case the formatted logging will be
        /// sent to the Amazon.Lambda.Core.LambdaLogger's Log method.
        /// </summary>
        /// <typeparam name="TState"></typeparam>
        /// <param name="logLevel"></param>
        /// <param name="eventId"></param>
        /// <param name="state"></param>
        /// <param name="exception"></param>
        /// <param name="formatter"></param>
        /// <exception cref="ArgumentNullException"></exception>
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

            var lambdaLogLevel = ConvertLogLevel(logLevel);

            if (IsLambdaJsonFormatEnabled && state is IEnumerable<KeyValuePair<string, object>> structure)
            {
                string messageTemplate = null;
                var parameters = new List<object>();
                foreach (var property in structure)
                {
                    if (property is { Key: "{OriginalFormat}", Value: string value })
                    {
                        messageTemplate = value;
                    }
                    else
                    {
                        parameters.Add(property.Value);
                    }
                }

                if (messageTemplate == null)
                {
                    messageTemplate = formatter.Invoke(state, exception);
                }

                Amazon.Lambda.Core.LambdaLogger.Log(lambdaLogLevel, exception, messageTemplate, parameters.ToArray());
            }
            else
            {
                var components = new List<string>(4);
                if (_options.IncludeLogLevel)
                {
                    components.Add($"[{logLevel}]");
                }

                GetScopeInformation(components);

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

                Amazon.Lambda.Core.LambdaLogger.Log(lambdaLogLevel, finalText);
            }
        }

        private static Amazon.Lambda.Core.LogLevel ConvertLogLevel(LogLevel logLevel)
        {
            switch (logLevel)
            {
                case LogLevel.Trace:
                    return Amazon.Lambda.Core.LogLevel.Trace;
                case LogLevel.Debug:
                    return Amazon.Lambda.Core.LogLevel.Debug;
                case LogLevel.Information:
                    return Amazon.Lambda.Core.LogLevel.Information;
                case LogLevel.Warning:
                    return Amazon.Lambda.Core.LogLevel.Warning;
                case LogLevel.Error:
                    return Amazon.Lambda.Core.LogLevel.Error;
                case LogLevel.Critical:
                    return Amazon.Lambda.Core.LogLevel.Critical;
                default:
                    return Amazon.Lambda.Core.LogLevel.Information;
            }
        }

        private void GetScopeInformation(List<string> logMessageComponents)
        {
            var scopeProvider = ScopeProvider;

            if (_options.IncludeScopes && scopeProvider != null)
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

        private bool IsLambdaJsonFormatEnabled
        {
            get
            {
                return string.Equals(Environment.GetEnvironmentVariable("AWS_LAMBDA_LOG_FORMAT"), "JSON", StringComparison.InvariantCultureIgnoreCase);
            }
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
