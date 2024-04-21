#if NET6_0_OR_GREATER

using System.Text;

namespace Amazon.Lambda.RuntimeSupport.Helpers.Logging
{
    /// <summary>
    /// The default log message formatter that log the message as a simple string. Replacing any message properties
    /// in the message with provided arguments. The message will be prefixed with the timestamp, request id and 
    /// log level unless constructed passing in false for addPrefix.
    /// 
    /// This formatter matches is the logging format introduced as part of the .NET 6 managed runtime.
    /// </summary>
    public class DefaultLogMessageFormatter : AbstractLogMessageFormatter
    {
        /// <summary>
        /// If true timestamp, request id and log level are added as a prefix to every log message.
        /// </summary>
        public bool AddPrefix { get; }

        /// <summary>
        /// Constructs an instance of DefaultLogMessageFormatter.
        /// </summary>
        /// <param name="addPrefix">If true timestamp, request id and log level are added as a prefix to every log message.</param>
        public DefaultLogMessageFormatter(bool addPrefix)
        {
            AddPrefix = addPrefix;
        }

        /// <summary>
        /// Format the log message applying in message property replacements and adding a prefix unless disabled.
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        public override string FormatMessage(MessageState state)
        {
            string message;

            // If there are no arguments then this is not a parameterized log message so skip parsing logic.
            if(state.MessageArguments?.Length == 0)
            {
                message = state.MessageTemplate;
            }
            else
            {
                // Parse the message template for any message properties like "{count}".
                var messageProperties = ParseProperties(state.MessageTemplate);

                // Replace any message properties in the message template with the provided argument values.
                message = ApplyMessageProperties(state.MessageTemplate, messageProperties, state.MessageArguments);
            }

            var displayLevel = state.Level != null ? ConvertLogLevelToLabel(state.Level.Value) : null;

            var sb = new StringBuilder(
                                    25 + // Length for timestamp
                                    (state.AwsRequestId?.Length).GetValueOrDefault() +
                                    displayLevel.Length +
                                    (message?.Length).GetValueOrDefault() +
                                    5 // Padding for tabs
                                    );

            if (!AddPrefix)
            {
                return message;
            }


            if (AddPrefix)
            {
                sb.Append(FormatTimestamp(state));
                sb.Append('\t');
                sb.Append(state.AwsRequestId);

                if (!string.IsNullOrEmpty(displayLevel))
                {
                    sb.Append('\t');
                    sb.Append(displayLevel);
                }

                sb.Append('\t');
            }


            if (!string.IsNullOrEmpty(message))
            {
                sb.Append(message);
            }

            if (state.Exception != null)
            {
                if (!string.IsNullOrEmpty(message))
                {
                    sb.Append('\n');
                }
                sb.Append(state.Exception.ToString());
            }

            return sb.ToString();
        }

        /// <summary>
        /// Convert LogLevel enums to the the same string label that console provider for Microsoft.Extensions.Logging.ILogger uses.
        /// </summary>
        /// <param name="level"></param>
        /// <returns></returns>
        private string ConvertLogLevelToLabel(LogLevelLoggerWriter.LogLevel level)
        {
            switch (level)
            {
                case LogLevelLoggerWriter.LogLevel.Trace:
                    return "trce";
                case LogLevelLoggerWriter.LogLevel.Debug:
                    return "dbug";
                case LogLevelLoggerWriter.LogLevel.Information:
                    return "info";
                case LogLevelLoggerWriter.LogLevel.Warning:
                    return "warn";
                case LogLevelLoggerWriter.LogLevel.Error:
                    return "fail";
                case LogLevelLoggerWriter.LogLevel.Critical:
                    return "crit";
            }

            return level.ToString();
        }
    }
}
#endif