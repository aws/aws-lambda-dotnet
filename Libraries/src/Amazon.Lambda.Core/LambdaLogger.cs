using System;

namespace Amazon.Lambda.Core
{
    /// <summary>
    /// Static class which sends a message to AWS CloudWatch Logs.
    /// When used outside of a Lambda environment, logs are written to
    /// Console.Out.
    /// </summary>
    public static class LambdaLogger
    {
        // Logging action, logs to Console by default
        private static Action<string> _loggingAction = LogToConsole;

#if NET6_0_OR_GREATER
        private static System.Buffers.ReadOnlySpanAction<byte, object> _dataLoggingAction = LogUtf8BytesToConsole;
#endif

        // Logs message to console
        private static void LogToConsole(string message)
        {
            Console.WriteLine(message);
        }

#if NET6_0_OR_GREATER
        private static void LogUtf8BytesToConsole(ReadOnlySpan<byte> utf8Message, object state)
        {
            try
            {
                Console.WriteLine(Console.OutputEncoding.GetString(utf8Message));
            }
            catch
            {
                // ignore any encoding error
            }
        }

        /// <summary>
        /// Logs a message to AWS CloudWatch Logs. <br/>
        /// Logging will not be done:
        /// If the role provided to the function does not have sufficient permissions.
        /// </summary>
        /// <param name="utf8Message">The message as UTF-8 encoded data.</param>
        public static void Log(ReadOnlySpan<byte> utf8Message)
        {
            _dataLoggingAction(utf8Message, null);
        }
#endif

        /// <summary>
        /// Logs a message to AWS CloudWatch Logs.
        /// 
        /// Logging will not be done:
        ///  If the role provided to the function does not have sufficient permissions.
        /// </summary>
        /// <param name="message"></param>
        public static void Log(string message)
        {
            _loggingAction(message);
        }
    }
}
