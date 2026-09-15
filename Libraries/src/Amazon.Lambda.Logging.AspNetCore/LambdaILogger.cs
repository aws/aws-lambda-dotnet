using System;
using System.Collections.Generic;

namespace Microsoft.Extensions.Logging
{
    internal class LambdaILogger : ILogger
    {
        /// <summary>
        /// The set of JSON property names written unconditionally by the Lambda RuntimeSupport JSON log formatter
        /// (Amazon.Lambda.RuntimeSupport.Helpers.Logging.JsonLogMessageFormatter). Scope values are never allowed to
        /// use these names so that a scope entry can never overwrite/corrupt these reserved metadata fields.
        /// </summary>
        private static readonly HashSet<string> ReservedMessagePropertyNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "timestamp",
            "level",
            "requestId",
            "tenantId",
            "traceId",
            "message",
            "errorType",
            "errorMessage",
            "stackTrace",
        };

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

                // Append structured scope key/value pairs to the template and parameters so they
                // are emitted as named JSON properties by the Lambda JSON formatter.
                if (_options.IncludeScopes && ScopeProvider != null)
                {
                    // Names already claimed by the message template itself (explicit message properties) always win.
                    // Scope entries that collide with these names, or with each other, must never be allowed to
                    // silently corrupt the message or reserved JSON metadata fields written by the RuntimeSupport
                    // JSON formatter (e.g. "timestamp", "level", "message", ...).
                    var messagePropertyNames = ExtractTemplatePropertyNames(messageTemplate);

                    // Preserves the order scope property names were first encountered, while allowing a later
                    // (i.e. more inner/nested) scope to overwrite the value of an earlier (outer) scope that used
                    // the same key. IExternalScopeProvider.ForEachScope invokes the callback from the outermost
                    // scope to the innermost scope, so later invocations here represent inner scopes.
                    var orderedScopeKeys = new List<string>();
                    var scopeValuesByKey = new Dictionary<string, object>(StringComparer.Ordinal);

                    ScopeProvider.ForEachScope((scope, list) =>
                    {
                        if (scope is IEnumerable<KeyValuePair<string, object>> scopeKvps)
                        {
                            foreach (var kvp in scopeKvps)
                            {
                                if (!IsSupportedScopeKey(kvp.Key) || messagePropertyNames.Contains(kvp.Key))
                                {
                                    continue;
                                }

                                if (!scopeValuesByKey.ContainsKey(kvp.Key))
                                {
                                    list.Add(kvp.Key);
                                }

                                // Overwrite with the latest value seen for this key so that, within a single
                                // scope's enumerable and across nested scopes, the innermost/last value wins.
                                scopeValuesByKey[kvp.Key] = kvp.Value;
                            }
                        }
                    }, orderedScopeKeys);

                    if (orderedScopeKeys.Count > 0)
                    {
                        var sb = new System.Text.StringBuilder(messageTemplate);
                        foreach (var key in orderedScopeKeys)
                        {
                            sb.Append($" {{{key}}}");
                            parameters.Add(scopeValuesByKey[key]);
                        }
                        messageTemplate = sb.ToString();
                    }
                }

                if (_options.IncludeCategory)
                {
                    // Unlike the text format, the JSON format otherwise drops the
                    // category entirely, so IncludeCategory has no effect. Prepend the
                    // category as literal text rather than a "{Category}" placeholder:
                    // a placeholder would be picked up by the formatter's positional
                    // argument detection and shift how the template's own {0}/{name}
                    // placeholders get matched to the caller's arguments.
                    messageTemplate = $"[{_categoryName}] " + messageTemplate;
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

        /// <summary>
        /// Determines whether a scope key can be safely appended as a "{key}" message-template placeholder
        /// and understood correctly by the Lambda RuntimeSupport JSON log formatter's message template parser.
        /// </summary>
        /// <remarks>
        /// The RuntimeSupport parser (Amazon.Lambda.RuntimeSupport.Helpers.Logging.AbstractLogMessageFormatter /
        /// MessageProperty) treats '{' and '}' as structural delimiters, an optional leading '@' as a directive
        /// that switches the value to JSON serialization, and the first ':' as the start of a .NET format string
        /// applied to the value. None of these characters can appear in the key without changing how the
        /// template is parsed or silently truncating/renaming the resulting JSON property. Keys containing
        /// whitespace are also rejected since they do not represent a well-formed identifier for a JSON
        /// property name. Unsupported keys are skipped entirely rather than sanitized/renamed to avoid
        /// introducing new collisions or misleading data.
        /// </remarks>
        /// <param name="key">The scope dictionary key to validate.</param>
        /// <returns>True if the key is safe to use as a message-template property name.</returns>
        private static bool IsSupportedScopeKey(string key)
        {
            if (string.IsNullOrEmpty(key) || key == "{OriginalFormat}")
            {
                return false;
            }

            if (ReservedMessagePropertyNames.Contains(key))
            {
                return false;
            }

            foreach (var c in key)
            {
                if (c == '{' || c == '}' || c == ':' || c == '@' || char.IsWhiteSpace(c))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Parses a message template to determine the set of message-property names it explicitly defines
        /// (e.g. "User {Name} logged in" defines the property name "Name"). Used to ensure scope values never
        /// override an explicit message property with the same name.
        /// </summary>
        /// <param name="messageTemplate">The message template to inspect.</param>
        /// <returns>The set of property names already used by the message template.</returns>
        private static HashSet<string> ExtractTemplatePropertyNames(string messageTemplate)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);

            if (string.IsNullOrEmpty(messageTemplate))
            {
                return names;
            }

            var inParameter = false;
            var possibleParameterOpen = false;
            int paramStartIdx = -1;

            for (int i = 0, l = messageTemplate.Length; i < l; i++)
            {
                var c = messageTemplate[i];
                if (c == '{')
                {
                    if (!inParameter && !possibleParameterOpen)
                    {
                        possibleParameterOpen = true;
                    }
                    else if (possibleParameterOpen)
                    {
                        // escaped "{{"
                        possibleParameterOpen = false;
                    }
                }
                else if (c == '}')
                {
                    if (inParameter || possibleParameterOpen)
                    {
                        if (paramStartIdx != -1)
                        {
                            var token = messageTemplate.Substring(paramStartIdx, i - paramStartIdx);
                            if (token.Length > 0 && token[0] == '@')
                            {
                                token = token.Substring(1);
                            }
                            var colonIdx = token.IndexOf(':');
                            if (colonIdx >= 0)
                            {
                                token = token.Substring(0, colonIdx);
                            }
                            names.Add(token.Trim());
                        }

                        inParameter = false;
                        possibleParameterOpen = false;
                        paramStartIdx = -1;
                    }
                }
                else if (possibleParameterOpen)
                {
                    paramStartIdx = i;
                    possibleParameterOpen = false;
                    inParameter = true;
                }
            }

            return names;
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
