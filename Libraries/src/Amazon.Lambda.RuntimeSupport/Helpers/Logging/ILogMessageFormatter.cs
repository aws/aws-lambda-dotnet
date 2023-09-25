#if NET6_0_OR_GREATER

namespace Amazon.Lambda.RuntimeSupport.Helpers.Logging
{
    /// <summary>
    /// The interface for defining log formatters that the ConsoleLogFormatter will use to format the incoming log messages
    /// before sending the log message to the Lambda service.
    /// </summary>
    public interface ILogMessageFormatter
    {
        /// <summary>
        /// Format the log message
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        string FormatMessage(MessageState state);
    }
}
#endif