#if NET6_0_OR_GREATER
using System;

namespace Amazon.Lambda.RuntimeSupport.Helpers.Logging
{
    /// <summary>
    /// The state of the environment for a log message that needs to be logged.
    /// </summary>
    public class MessageState
    {
        /// <summary>
        /// The timestamp of the log message.
        /// </summary>
        public DateTime TimeStamp { get; set; }

        /// <summary>
        /// The AWS request id for the Lambda invocation. This property can be null
        /// if logging before the first event.
        /// </summary>
        public string AwsRequestId { get; set; }

        /// <summary>
        /// The current trace id if available.
        /// </summary>
        public string TraceId { get; set; }

        /// <summary>
        /// The message template the Lambda function has sent to RuntimeSupport. It may include message properties in the template
        /// for example the message template "User bought {count} of {product}" has count and product as message properties.
        /// </summary>
        public string MessageTemplate { get; set; }

        /// <summary>
        /// The values to replace for any message properties in the message template.
        /// </summary>
        public object[] MessageArguments { get; set; }

        /// <summary>
        /// The log level of the message being logged.
        /// </summary>
        public LogLevelLoggerWriter.LogLevel? Level { get; set; }

        /// <summary>
        /// An exception to be logged along with the log message.
        /// </summary>
        public Exception Exception { get; set; }
    }
}
#endif