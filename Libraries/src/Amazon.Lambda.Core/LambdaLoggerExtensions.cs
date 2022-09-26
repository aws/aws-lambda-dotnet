namespace Amazon.Lambda.Core
{
#if NET6_0_OR_GREATER
    using System;
    using System.Collections;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text;

    /// <summary>
    /// Provides extension methods to write structured logs.
    /// </summary>
    public static class LambdaLoggerExtensions
    {
        private static readonly char[] PARAM_FORMAT_DELIMITERS = { ',', ':' };

        /// <summary>
        /// Logs a trace message.
        /// </summary>
        /// <param name="logger">Instance of <see cref="ILambdaLogger"/>.</param>
        /// <param name="format">Log message format.</param>
        /// <param name="args">Log parameters.</param>
        public static void Trace(this ILambdaLogger logger, string format, params object[] args) => LogMessageEntry(logger, LogLevel.Trace, null, format, args);

        /// <summary>
        /// Logs a debug message.
        /// </summary>
        /// <param name="logger">Instance of <see cref="ILambdaLogger"/>.</param>
        /// <param name="format">Log message format.</param>
        /// <param name="args">Log parameters.</param>
        public static void Debug(this ILambdaLogger logger, string format, params object[] args) => LogMessageEntry(logger, LogLevel.Debug, null, format, args);

        /// <summary>
        /// Logs an information message.
        /// </summary>
        /// <param name="logger">Instance of <see cref="ILambdaLogger"/>.</param>
        /// <param name="format">Log message format.</param>
        /// <param name="args">Log parameters.</param>
        public static void Info(this ILambdaLogger logger, string format, params object[] args) => LogMessageEntry(logger, LogLevel.Information, null, format, args);

        /// <summary>
        /// Logs a warning message.
        /// </summary>
        /// <param name="logger">Instance of <see cref="ILambdaLogger"/>.</param>
        /// <param name="format">Log message format.</param>
        /// <param name="args">Log parameters.</param>
        public static void Warning(this ILambdaLogger logger, string format, params object[] args) => LogMessageEntry(logger, LogLevel.Warning, null, format, args);

        /// <summary>
        /// Logs an error message.
        /// </summary>
        /// <param name="logger">Instance of <see cref="ILambdaLogger"/>.</param>
        /// <param name="format">Log message format.</param>
        /// <param name="args">Log parameters.</param>
        public static void Error(this ILambdaLogger logger, string format, params object[] args) => LogMessageEntry(logger, LogLevel.Error, null, format, args);

        /// <summary>
        /// Logs an error message.
        /// </summary>
        /// <param name="logger">Instance of <see cref="ILambdaLogger"/>.</param>
        /// <param name="exception">Exception to include in the log.</param>
        /// <param name="format">Log message format.</param>
        /// <param name="args">Log parameters.</param>
        public static void Error(this ILambdaLogger logger, Exception exception, string format, params object[] args) => LogMessageEntry(logger, LogLevel.Error, exception, format, args);

        /// <summary>
        /// Logs a critical message.
        /// </summary>
        /// <param name="logger">Instance of <see cref="ILambdaLogger"/>.</param>
        /// <param name="format">Log message format.</param>
        /// <param name="args">Log parameters.</param>
        public static void Critical(this ILambdaLogger logger, string format, params object[] args) => LogMessageEntry(logger, LogLevel.Critical, null, format, args);

        /// <summary>
        /// Logs a critical message.
        /// </summary>
        /// <param name="logger">Instance of <see cref="ILambdaLogger"/>.</param>
        /// <param name="exception">Exception to include in the log.</param>
        /// <param name="format">Log message format.</param>
        /// <param name="args">Log parameters.</param>
        public static void Critical(this ILambdaLogger logger, Exception exception, string format, params object[] args) => LogMessageEntry(logger, LogLevel.Critical, exception, format, args);

        private static void LogMessageEntry(ILambdaLogger logger, LogLevel level, Exception exception, string format, params object[] args)
            => logger.LogEntry(level, new FormattedMessageEntry(format, exception, args));

        internal readonly struct FormattedMessageEntry : IMessageEntry, IReadOnlyList<KeyValuePair<string, object>>
        {
            /// <summary>
            /// Use a cache to look-up formatter so we don't have to parse the format for every entry.
            /// </summary>
            private static readonly ConcurrentDictionary<string, MessageFormatter> GLOBAL_FORMATTER_CACHE = new ConcurrentDictionary<string, MessageFormatter>();
            private const int GLOBAL_FORMATTER_CACHE_MAXSIZE = 1024;

            private readonly MessageFormatter _formatter;
            private readonly object[] _args;

            public FormattedMessageEntry(string format, Exception exception, object[] args) : this(format, exception, args, GLOBAL_FORMATTER_CACHE, GLOBAL_FORMATTER_CACHE_MAXSIZE)
            {
            }

            internal FormattedMessageEntry(string format, Exception exception, object[] args, ConcurrentDictionary<string, MessageFormatter> formatterCache, int formatterCacheMaxSize)
            {
                _args = args ?? Array.Empty<object>();
                Exception = exception;

                if (format is null)
                {
                    _formatter = null;
                }
                else if (formatterCache.Count >= formatterCacheMaxSize)
                {
                    // avoid letting the cache grow indefinitely
                    _formatter = formatterCache.TryGetValue(format, out var fmt) ? fmt : new MessageFormatter(format);
                }
                else
                {
                    _formatter = formatterCache.GetOrAdd(format, valueFactory: f => new MessageFormatter(f));
                }
            }

            public KeyValuePair<string, object> this[int index] => _formatter != null
                ? new KeyValuePair<string, object>(_formatter.ParameterNames[index], _args[index])
                : throw new IndexOutOfRangeException(nameof(index));

            public IReadOnlyList<KeyValuePair<string, object>> State => this;

            public Exception Exception { get; }

            public int Count => _formatter is null ? 0 : _formatter.ParameterNames.Count;

            public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
            {
                for (int i = 0, c = Count; i < c; i++)
                {
                    yield return this[i];
                }
            }

            public override string ToString() => _formatter?.Format(_args);

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        internal sealed class MessageFormatter
        {
            private readonly string _format;

            public MessageFormatter(string formatString)
            {
                ArgumentNullException.ThrowIfNull(formatString);
                _format = ParseLogFormatString(formatString, ParameterNames);
            }

            public string Format(object[] args) => string.Format(CultureInfo.InvariantCulture, _format, args);

            public List<string> ParameterNames { get; } = new List<string>();

            /// <summary>
            /// Parses a log format string for the format and parameter names.
            /// </summary>
            /// <param name="formatString">Log format string.</param>
            /// <param name="names">The list to which parameter names are added.</param>
            /// <returns>The format string that can be provided to <see cref="string.Format(string, object?[])"/> to form the log message.</returns>
            internal static string ParseLogFormatString(string formatString, List<string> names)
            {
                var state = LogFormatParserState.InMessage;
                var sb = new StringBuilder();

                int paramStartIdx = -1;
                for (int i = 0, l = formatString.Length; i < l; i++)
                {
                    var c = formatString[i];
                    switch (c)
                    {
                        case '{':
                            if (state == LogFormatParserState.InMessage)
                            {
                                // regardless of whether this is the opening of a parameter we'd still need to add {
                                sb.Append(c);
                                state = LogFormatParserState.PossibleParameterOpen;
                            }
                            else if (state == LogFormatParserState.PossibleParameterOpen)
                            {
                                sb.Append(c);
                                state = LogFormatParserState.InMessage;
                            }
                            else
                            {
                                // ignore, consider '{' as part of parameter name
                            }
                            break;
                        case '}':
                            if (state == LogFormatParserState.InMessage)
                            {
                                sb.Append(c);
                            }
                            else
                            {
                                ParseParameterAndFormat(formatString.AsSpan().Slice(paramStartIdx, i - paramStartIdx), sb, names);
                                sb.Append('}');
                                state = LogFormatParserState.InMessage;
                            }
                            break;
                        default:
                            if (state == LogFormatParserState.InMessage)
                            {
                                sb.Append(c);
                            }
                            else if (state == LogFormatParserState.PossibleParameterOpen)
                            {
                                paramStartIdx = i;
                                state = LogFormatParserState.InParameter;
                            }
                            else
                            {
                                // ignore
                            }
                            break;
                    }
                }

                return sb.ToString();
            }

            private static void ParseParameterAndFormat(ReadOnlySpan<char> paramFormatString, StringBuilder formatBuilder, List<string> names)
            {
                // replace the param name in the format with the appropriate index in string.Format
                formatBuilder.Append(names.Count);

                // syntax of the parameter format string is '{name,alignment:format}'
                var idxOfDelimeter = paramFormatString.IndexOfAny(PARAM_FORMAT_DELIMITERS);
                if (idxOfDelimeter < 0)
                {
                    // entire format string is param name
                    names.Add(paramFormatString.ToString());
                    return;
                }

                // there is alignment/format, extract the param name
                names.Add(paramFormatString.Slice(0, idxOfDelimeter).ToString());

                // append the alignment/format portion
                formatBuilder.Append(paramFormatString.Slice(idxOfDelimeter).ToString());
            }
        }

        /// <summary>
        /// States in the log format parser state machine.
        /// </summary>
        private enum LogFormatParserState : byte
        {
            InMessage,
            PossibleParameterOpen,
            InParameter
        }
    }
#endif
}
