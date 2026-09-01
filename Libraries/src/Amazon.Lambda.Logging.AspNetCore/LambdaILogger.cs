using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;

namespace Microsoft.Extensions.Logging
{
    internal class LambdaILogger : ILogger
    {
        private const string ORIGINAL_FORMAT_KEY = "{OriginalFormat}";
        private const int MESSAGE_TEMPLATE_PARSE_CACHE_MAXSIZE = 1024;

        private static readonly ConcurrentDictionary<string, IReadOnlyList<string>> MESSAGE_TEMPLATE_PARSE_CACHE =
            new ConcurrentDictionary<string, IReadOnlyList<string>>();
        private static readonly char[] PROPERTY_NAME_DELIMITERS = { ',', ':' };

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
                var properties = new List<KeyValuePair<string, object>>();
                foreach (var property in structure)
                {
                    if (property is { Key: ORIGINAL_FORMAT_KEY, Value: string value })
                    {
                        messageTemplate = value;
                    }
                    else
                    {
                        properties.Add(property);
                    }
                }

                object[] parameters;
                if (messageTemplate == null)
                {
                    messageTemplate = formatter.Invoke(state, exception);
                    parameters = GetPropertyValues(properties);
                }
                else
                {
                    parameters = OrderParametersByMessageTemplate(messageTemplate, properties);
                }

                Amazon.Lambda.Core.LambdaLogger.Log(lambdaLogLevel, exception, messageTemplate, parameters);
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

        private static object[] OrderParametersByMessageTemplate(
            string messageTemplate,
            IReadOnlyList<KeyValuePair<string, object>> properties)
        {
            var templateProperties = ParseTemplateProperties(messageTemplate);
            if (templateProperties.Count == 0)
            {
                return GetPropertyValues(properties);
            }

            if (PropertiesMatchTemplateOrder(templateProperties, properties))
            {
                return GetPropertyValues(properties);
            }

            if (UsesPositionalArguments(templateProperties))
            {
                return GetPropertyValues(properties);
            }

            var parameters = new List<object>(Math.Max(templateProperties.Count, properties.Count));
            var consumedProperties = new bool[properties.Count];
            var propertyIndexes = new Dictionary<string, List<int>>(StringComparer.Ordinal);
            var propertyOccurrences = new Dictionary<string, int>(StringComparer.Ordinal);

            for (var i = 0; i < properties.Count; i++)
            {
                if (properties[i].Key == null)
                {
                    continue;
                }

                if (!propertyIndexes.TryGetValue(properties[i].Key, out var indexes))
                {
                    indexes = new List<int>();
                    propertyIndexes.Add(properties[i].Key, indexes);
                }

                indexes.Add(i);
            }

            foreach (var templateProperty in templateProperties)
            {
                var propertyName = GetStatePropertyName(templateProperty);
                if (!propertyIndexes.TryGetValue(propertyName, out var indexes) &&
                    (propertyName.Length <= 1 ||
                     (propertyName[0] != '@' && propertyName[0] != '$') ||
                     !propertyIndexes.TryGetValue(propertyName.Substring(1), out indexes)))
                {
                    // Keep the slot so a missing value cannot shift subsequent properties.
                    parameters.Add(null);
                    continue;
                }

                var matchedName = properties[indexes[0]].Key;
                propertyOccurrences.TryGetValue(matchedName, out var occurrence);
                propertyOccurrences[matchedName] = occurrence + 1;

                // A custom state can expose one value for a repeated placeholder. Reuse
                // that sole value; multiple same-name values are consumed in order.
                var propertyIndex = indexes.Count == 1
                    ? indexes[0]
                    : occurrence < indexes.Count ? indexes[occurrence] : -1;
                if (propertyIndex < 0)
                {
                    parameters.Add(null);
                    continue;
                }

                consumedProperties[propertyIndex] = true;
                parameters.Add(properties[propertyIndex].Value);
            }

            // Preserve state values that are not represented in the template. The JSON
            // formatter ignores trailing arguments, but custom LambdaLogger callbacks may
            // still inspect them.
            for (var i = 0; i < properties.Count; i++)
            {
                if (!consumedProperties[i])
                {
                    parameters.Add(properties[i].Value);
                }
            }

            return parameters.ToArray();
        }

        private static bool PropertiesMatchTemplateOrder(
            IReadOnlyList<string> templateProperties,
            IReadOnlyList<KeyValuePair<string, object>> properties)
        {
            if (templateProperties.Count != properties.Count)
            {
                return false;
            }

            for (var i = 0; i < templateProperties.Count; i++)
            {
                var propertyName = GetStatePropertyName(templateProperties[i]);
                if (string.Equals(properties[i].Key, propertyName, StringComparison.Ordinal))
                {
                    continue;
                }

                if (propertyName.Length <= 1 ||
                    (propertyName[0] != '@' && propertyName[0] != '$') ||
                    !string.Equals(properties[i].Key, propertyName.Substring(1), StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static object[] GetPropertyValues(IReadOnlyList<KeyValuePair<string, object>> properties)
        {
            var parameters = new object[properties.Count];
            for (var i = 0; i < properties.Count; i++)
            {
                parameters[i] = properties[i].Value;
            }

            return parameters;
        }

        private static IReadOnlyList<string> ParseTemplateProperties(string messageTemplate)
        {
            if (MESSAGE_TEMPLATE_PARSE_CACHE.TryGetValue(messageTemplate, out var cachedProperties))
            {
                return cachedProperties;
            }

            var properties = new List<string>();
            var parserState = LogFormatParserState.InMessage;
            var propertyStartIndex = -1;

            for (var i = 0; i < messageTemplate.Length; i++)
            {
                switch (messageTemplate[i])
                {
                    case '{':
                        if (parserState == LogFormatParserState.InMessage)
                        {
                            parserState = LogFormatParserState.PossiblePropertyOpen;
                        }
                        else if (parserState == LogFormatParserState.PossiblePropertyOpen)
                        {
                            // Escaped "{{" is message text, not a property.
                            parserState = LogFormatParserState.InMessage;
                        }
                        break;
                    case '}':
                        if (parserState != LogFormatParserState.InMessage)
                        {
                            if (propertyStartIndex >= 0)
                            {
                                properties.Add(messageTemplate.Substring(propertyStartIndex, i - propertyStartIndex));
                            }

                            parserState = LogFormatParserState.InMessage;
                            propertyStartIndex = -1;
                        }
                        break;
                    default:
                        if (parserState == LogFormatParserState.PossiblePropertyOpen)
                        {
                            propertyStartIndex = i;
                            parserState = LogFormatParserState.InProperty;
                        }
                        break;
                }
            }

            var parsedProperties = properties.AsReadOnly();
            if (MESSAGE_TEMPLATE_PARSE_CACHE.Count < MESSAGE_TEMPLATE_PARSE_CACHE_MAXSIZE)
            {
                MESSAGE_TEMPLATE_PARSE_CACHE.TryAdd(messageTemplate, parsedProperties);
            }

            return parsedProperties;
        }

        private static string GetStatePropertyName(string templateProperty)
        {
            var delimiterIndex = templateProperty.IndexOfAny(PROPERTY_NAME_DELIMITERS);
            var propertyName = delimiterIndex >= 0
                ? templateProperty.Substring(0, delimiterIndex)
                : templateProperty;
            return propertyName.Trim();
        }

        private static bool UsesPositionalArguments(IReadOnlyList<string> templateProperties)
        {
            var minimumPosition = int.MaxValue;
            var maximumPosition = int.MinValue;
            var positions = new HashSet<int>();

            foreach (var templateProperty in templateProperties)
            {
                var propertyName = templateProperty;
                if (propertyName.Length > 0 && propertyName[0] == '@')
                {
                    propertyName = propertyName.Substring(1);
                }

                var formatDelimiterIndex = propertyName.IndexOf(':');
                if (formatDelimiterIndex >= 0)
                {
                    propertyName = propertyName.Substring(0, formatDelimiterIndex);
                }

                if (!int.TryParse(propertyName.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var position))
                {
                    return false;
                }

                positions.Add(position);
                minimumPosition = Math.Min(minimumPosition, position);
                maximumPosition = Math.Max(maximumPosition, position);
            }

            return minimumPosition == 0 && positions.Count == maximumPosition + 1;
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

        private enum LogFormatParserState : byte
        {
            InMessage,
            PossiblePropertyOpen,
            InProperty
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
