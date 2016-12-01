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
    }
}
