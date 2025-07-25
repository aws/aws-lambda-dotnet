using System;
using System.Reflection.Emit;
using System.Runtime.Versioning;
using System.Text;

namespace Amazon.Lambda.Core
{
    /// <summary>
    /// Static class which sends a message to AWS CloudWatch Logs.
    /// When used outside of a Lambda environment, logs are written to
    /// Console.Out.
    /// </summary>
    public static class LambdaLogger
    {
        // The name of this field must not change or be readonly because Amazon.Lambda.RuntimeSupport will use reflection to replace the
        // value with an Action that directs the logging into its logging system.
#pragma warning disable IDE0044 // Add readonly modifier
        private static Action<string> _loggingAction = LogToConsole;
#pragma warning restore IDE0044 // Add readonly modifier

        // Logs message to console
        private static void LogToConsole(string message)
        {
            Console.WriteLine(message);
        }

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

#if NET6_0_OR_GREATER

        // The name of this field must not change or be readonly because Amazon.Lambda.RuntimeSupport will use reflection to replace the
        // value with an Action that directs the logging into its logging system.
#pragma warning disable IDE0044 // Add readonly modifier
        private static Action<string, string, object[]> _loggingWithLevelAction = LogWithLevelToConsole;
        private static Action<string, Exception, string, object[]> _loggingWithLevelAndExceptionAction = LogWithLevelAndExceptionToConsole;
#pragma warning restore IDE0044 // Add readonly modifier

        // Logs message to console
        private static void LogWithLevelToConsole(string level, string message, params object[] args)
        {
            // Formatting here is not important, it is used for debugging Amazon.Lambda.Core only.
            // In a real scenario Amazon.Lambda.RuntimeSupport will change the value of _loggingWithLevelAction
            // to an Action inside it's logging system to handle the real formatting.
            var sb = new StringBuilder();
            sb.Append(level).Append(": ").Append(message);
            if (args?.Length > 0)
            {
                sb.Append(" Arguments:");
                foreach(var arg in args)
                {
                    sb.Append(" \"");
                    sb.Append(arg);
                    sb.Append("\"");
                }
            }
            Console.WriteLine(sb.ToString());
        }

        private static void LogWithLevelAndExceptionToConsole(string level, Exception exception, string message, params object[] args)
        {
            // Formatting here is not important, it is used for debugging Amazon.Lambda.Core only.
            // In a real scenario Amazon.Lambda.RuntimeSupport will change the value of _loggingWithLevelAction
            // to an Action inside it's logging system to handle the real formatting.
            LogWithLevelToConsole(level, message, args);
            Console.WriteLine(exception);
        }

        /// <summary>
        /// Logs a message to AWS CloudWatch Logs.
        /// 
        /// Logging will not be done:
        ///  If the role provided to the function does not have sufficient permissions.
        /// </summary>
        /// <param name="level">The log level of the message</param>
        /// <param name="message">Message to log. The message may have format arguments.</param>
        /// <param name="args">Arguments to format the message with.</param>
        public static void Log(string level, string message, params object[] args)
        {
            _loggingWithLevelAction(level, message, args);
        }

        /// <summary>
        /// Logs a message to AWS CloudWatch Logs.
        /// 
        /// Logging will not be done:
        ///  If the role provided to the function does not have sufficient permissions.
        /// </summary>
        /// <param name="level">The log level of the message</param>
        /// <param name="message">Message to log. The message may have format arguments.</param>
        /// <param name="args">Arguments to format the message with.</param>
        public static void Log(LogLevel level, string message, params object[] args) => Log(level.ToString(), message, args);

        /// <summary>
        /// Logs a message to AWS CloudWatch Logs.
        /// 
        /// Logging will not be done:
        ///  If the role provided to the function does not have sufficient permissions.
        /// </summary>
        /// <param name="level">The log level of the message</param>
        /// <param name="exception">Exception to include with the logging.</param>
        /// <param name="message">Message to log. The message may have format arguments.</param>
        /// <param name="args">Arguments to format the message with.</param>
        public static void Log(string level, Exception exception, string message, params object[] args)
        {
            _loggingWithLevelAndExceptionAction(level, exception, message, args);
        }

        /// <summary>
        /// Logs a message to AWS CloudWatch Logs.
        /// 
        /// Logging will not be done:
        ///  If the role provided to the function does not have sufficient permissions.
        /// </summary>
        /// <param name="level">The log level of the message</param>
        /// <param name="exception">Exception to include with the logging.</param>
        /// <param name="message">Message to log. The message may have format arguments.</param>
        /// <param name="args">Arguments to format the message with.</param>
        public static void Log(LogLevel level, Exception exception, string message, params object[] args) => Log(level.ToString(), exception, message, args);
#endif
    }
}
