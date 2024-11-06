using System;
using System.Runtime.Versioning;

namespace Amazon.Lambda.Core
{
#if NET6_0_OR_GREATER
    /// <summary>
    /// Log Level for logging messages
    /// </summary>
    public enum LogLevel 
    {
        /// <summary>
        /// Trace level logging
        /// </summary>
        Trace = 0,
        /// <summary>
        /// Debug level logging
        /// </summary>
        Debug = 1,

        /// <summary>
        /// Information level logging
        /// </summary>
        Information = 2,

        /// <summary>
        /// Warning level logging
        /// </summary>
        Warning = 3,

        /// <summary>
        /// Error level logging
        /// </summary>
        Error = 4,

        /// <summary>
        /// Critical level logging
        /// </summary>
        Critical = 5
    }
#endif
    /// <summary>
    /// Lambda runtime logger.
    /// </summary>
    public interface ILambdaLogger
    {
        /// <summary>
        /// Logs a message to AWS CloudWatch Logs.
        /// 
        /// Logging will not be done:
        ///  If the role provided to the function does not have sufficient permissions.
        /// </summary>
        /// <param name="message"></param>
        void Log(string message);

        /// <summary>
        /// Logs a message, followed by the current line terminator, to AWS CloudWatch Logs.
        /// 
        /// Logging will not be done:
        ///  If the role provided to the function does not have sufficient permissions.
        /// </summary>
        /// <param name="message"></param>
        void LogLine(string message);

#if NET6_0_OR_GREATER

        /// <summary>
        /// Log message categorized by the given log level
        /// <para>
        /// To configure the minimum log level set the AWS_LAMBDA_HANDLER_LOG_LEVEL environment variable. The value should be set
        /// to one of the values in the LogLevel enumeration. The default minimum log level is "Information".
        /// </para>
        /// </summary>
        /// <param name="level"></param>
        /// <param name="message"></param>
        void Log(string level, string message) => LogLine(message);

        /// <summary>
        /// Log message categorized by the given log level
        /// <para>
        /// To configure the minimum log level set the AWS_LAMBDA_HANDLER_LOG_LEVEL environment variable. The value should be set
        /// to one of the values in the LogLevel enumeration. The default minimum log level is "Information".
        /// </para>
        /// </summary>
        /// <param name="level"></param>
        /// <param name="message"></param>
        void Log(LogLevel level, string message) => Log(level.ToString(), message);

        /// <summary>
        /// Log trace message
        /// <para>
        /// To configure the minimum log level set the AWS_LAMBDA_HANDLER_LOG_LEVEL environment variable. The value should be set
        /// to one of the values in the LogLevel enumeration. The default minimum log level is "Information".
        /// </para>
        /// </summary>
        /// <param name="message"></param>
        void LogTrace(string message) => Log(LogLevel.Trace.ToString(), message);

        /// <summary>
        /// Log debug message
        /// <para>
        /// To configure the minimum log level set the AWS_LAMBDA_HANDLER_LOG_LEVEL environment variable. The value should be set
        /// to one of the values in the LogLevel enumeration. The default minimum log level is "Information".
        /// </para>
        /// </summary>
        /// <param name="message"></param>
        void LogDebug(string message) => Log(LogLevel.Debug.ToString(), message);

        /// <summary>
        /// Log information message
        /// <para>
        /// To configure the minimum log level set the AWS_LAMBDA_HANDLER_LOG_LEVEL environment variable. The value should be set
        /// to one of the values in the LogLevel enumeration. The default minimum log level is "Information".
        /// </para>
        /// </summary>
        /// <param name="message"></param>
        void LogInformation(string message) => Log(LogLevel.Information.ToString(), message);

        /// <summary>
        /// Log warning message
        /// <para>
        /// To configure the minimum log level set the AWS_LAMBDA_HANDLER_LOG_LEVEL environment variable. The value should be set
        /// to one of the values in the LogLevel enumeration. The default minimum log level is "Information".
        /// </para>
        /// </summary>
        /// <param name="message"></param>
        void LogWarning(string message) => Log(LogLevel.Warning.ToString(), message);

        /// <summary>
        /// Log error message
        /// <para>
        /// To configure the minimum log level set the AWS_LAMBDA_HANDLER_LOG_LEVEL environment variable. The value should be set
        /// to one of the values in the LogLevel enumeration. The default minimum log level is "Information".
        /// </para>
        /// </summary>
        /// <param name="message"></param>
        void LogError(string message) => Log(LogLevel.Error.ToString(), message);

        /// <summary>
        /// Log critical message
        /// <para>
        /// To configure the minimum log level set the AWS_LAMBDA_HANDLER_LOG_LEVEL environment variable. The value should be set
        /// to one of the values in the LogLevel enumeration. The default minimum log level is "Information".
        /// </para>
        /// </summary>
        /// <param name="message"></param>
        void LogCritical(string message) => Log(LogLevel.Critical.ToString(), message);


        /// <summary>
        /// Log message categorized by the given log level
        /// <para>
        /// To configure the minimum log level set the AWS_LAMBDA_HANDLER_LOG_LEVEL environment variable. The value should be set
        /// to one of the values in the LogLevel enumeration. The default minimum log level is "Information".
        /// </para>
        /// </summary>
        /// <param name="level">Log level of the message.</param>
        /// <param name="message">Message to log.</param>
        /// <param name="args">Values to be replaced in log messages that are parameterized.</param>
        void Log(string level, string message, params object[] args) => Log(level, message, args);

        /// <summary>
        /// Log message categorized by the given log level
        /// <para>
        /// To configure the minimum log level set the AWS_LAMBDA_HANDLER_LOG_LEVEL environment variable. The value should be set
        /// to one of the values in the LogLevel enumeration. The default minimum log level is "Information".
        /// </para>
        /// </summary>
        /// <param name="level">Log level of the message.</param>
        /// <param name="exception">Exception to include with the logging.</param>
        /// <param name="message">Message to log.</param>
        /// <param name="args">Values to be replaced in log messages that are parameterized.</param>
        void Log(string level, Exception exception, string message, params object[] args)
        {
            Log(level, message, args);
            Log(level, exception.ToString(), args);
        }

        /// <summary>
        /// Log message categorized by the given log level
        /// <para>
        /// To configure the minimum log level set the AWS_LAMBDA_HANDLER_LOG_LEVEL environment variable. The value should be set
        /// to one of the values in the LogLevel enumeration. The default minimum log level is "Information".
        /// </para>
        /// </summary>
        /// <param name="level">Log level of the message.</param>
        /// <param name="message">Message to log.</param>
        /// <param name="args">Values to be replaced in log messages that are parameterized.</param>
        void Log(LogLevel level, string message, params object[] args) => Log(level.ToString(), message, args);

        /// <summary>
        /// Log message categorized by the given log level
        /// <para>
        /// To configure the minimum log level set the AWS_LAMBDA_HANDLER_LOG_LEVEL environment variable. The value should be set
        /// to one of the values in the LogLevel enumeration. The default minimum log level is "Information".
        /// </para>
        /// </summary>
        /// <param name="level">Log level of the message.</param>
        /// <param name="exception">Exception to include with the logging.</param>
        /// <param name="message">Message to log.</param>
        /// <param name="args">Values to be replaced in log messages that are parameterized.</param>
        void Log(LogLevel level, Exception exception, string message, params object[] args) => Log(level.ToString(), exception, message, args);

        /// <summary>
        /// Log trace message.
        /// <para>
        /// To configure the minimum log level set the AWS_LAMBDA_HANDLER_LOG_LEVEL environment variable. The value should be set
        /// to one of the values in the LogLevel enumeration. The default minimum log level is "Information".
        /// </para>
        /// </summary>
        /// <param name="message">Message to log.</param>
        /// <param name="args">Values to be replaced in log messages that are parameterized.</param>
        void LogTrace(string message, params object[] args) => Log(LogLevel.Trace.ToString(), message, args);

        /// <summary>
        /// Log trace message.
        /// <para>
        /// To configure the minimum log level set the AWS_LAMBDA_HANDLER_LOG_LEVEL environment variable. The value should be set
        /// to one of the values in the LogLevel enumeration. The default minimum log level is "Information".
        /// </para>
        /// </summary>
        /// <param name="exception">Exception to include with the logging.</param>
        /// <param name="message">Message to log.</param>
        /// <param name="args">Values to be replaced in log messages that are parameterized.</param>
        void LogTrace(Exception exception, string message, params object[] args) => Log(LogLevel.Trace.ToString(), exception, message, args);

        /// <summary>
        /// Log debug message.
        /// <para>
        /// To configure the minimum log level set the AWS_LAMBDA_HANDLER_LOG_LEVEL environment variable. The value should be set
        /// to one of the values in the LogLevel enumeration. The default minimum log level is "Information".
        /// </para>
        /// </summary>
        /// <param name="message">Message to log.</param>
        /// <param name="args">Values to be replaced in log messages that are parameterized.</param>
        void LogDebug(string message, params object[] args) => Log(LogLevel.Debug.ToString(), message, args);

        /// <summary>
        /// Log debug message.
        /// <para>
        /// To configure the minimum log level set the AWS_LAMBDA_HANDLER_LOG_LEVEL environment variable. The value should be set
        /// to one of the values in the LogLevel enumeration. The default minimum log level is "Information".
        /// </para>
        /// </summary>
        /// <param name="exception">Exception to include with the logging.</param>
        /// <param name="message">Message to log.</param>
        /// <param name="args">Values to be replaced in log messages that are parameterized.</param>
        void LogDebug(Exception exception, string message, params object[] args) => Log(LogLevel.Debug.ToString(), exception, message, args);

        /// <summary>
        /// Log information message.
        /// <para>
        /// To configure the minimum log level set the AWS_LAMBDA_HANDLER_LOG_LEVEL environment variable. The value should be set
        /// to one of the values in the LogLevel enumeration. The default minimum log level is "Information".
        /// </para>
        /// </summary>
        /// <param name="message">Message to log.</param>
        /// <param name="args">Values to be replaced in log messages that are parameterized.</param>
        void LogInformation(string message, params object[] args) => Log(LogLevel.Information.ToString(), message, args);

        /// <summary>
        /// Log information message.
        /// <para>
        /// To configure the minimum log level set the AWS_LAMBDA_HANDLER_LOG_LEVEL environment variable. The value should be set
        /// to one of the values in the LogLevel enumeration. The default minimum log level is "Information".
        /// </para>
        /// </summary>
        /// <param name="exception">Exception to include with the logging.</param>
        /// <param name="message">Message to log.</param>
        /// <param name="args">Values to be replaced in log messages that are parameterized.</param>
        void LogInformation(Exception exception, string message, params object[] args) => Log(LogLevel.Information.ToString(), exception, message, args);

        /// <summary>
        /// Log warning message.
        /// <para>
        /// To configure the minimum log level set the AWS_LAMBDA_HANDLER_LOG_LEVEL environment variable. The value should be set
        /// to one of the values in the LogLevel enumeration. The default minimum log level is "Information".
        /// </para>
        /// </summary>
        /// <param name="message">Message to log.</param>
        /// <param name="args">Values to be replaced in log messages that are parameterized.</param>
        void LogWarning(string message, params object[] args) => Log(LogLevel.Warning.ToString(), message, args);

        /// <summary>
        /// Log warning message.
        /// <para>
        /// To configure the minimum log level set the AWS_LAMBDA_HANDLER_LOG_LEVEL environment variable. The value should be set
        /// to one of the values in the LogLevel enumeration. The default minimum log level is "Information".
        /// </para>
        /// </summary>
        /// <param name="exception">Exception to include with the logging.</param>
        /// <param name="message">Message to log.</param>
        /// <param name="args">Values to be replaced in log messages that are parameterized.</param>
        void LogWarning(Exception exception, string message, params object[] args) => Log(LogLevel.Warning.ToString(), exception, message, args);

        /// <summary>
        /// Log error message.
        /// <para>
        /// To configure the minimum log level set the AWS_LAMBDA_HANDLER_LOG_LEVEL environment variable. The value should be set
        /// to one of the values in the LogLevel enumeration. The default minimum log level is "Information".
        /// </para>
        /// </summary>
        /// <param name="message">Message to log.</param>
        /// <param name="args">Values to be replaced in log messages that are parameterized.</param>
        void LogError(string message, params object[] args) => Log(LogLevel.Error.ToString(), message, args);

        /// <summary>
        /// Log error message.
        /// <para>
        /// To configure the minimum log level set the AWS_LAMBDA_HANDLER_LOG_LEVEL environment variable. The value should be set
        /// to one of the values in the LogLevel enumeration. The default minimum log level is "Information".
        /// </para>
        /// </summary>
        /// <param name="exception">Exception to include with the logging.</param>
        /// <param name="message">Message to log.</param>
        /// <param name="args">Values to be replaced in log messages that are parameterized.</param>
        void LogError(Exception exception, string message, params object[] args) => Log(LogLevel.Error.ToString(), exception, message, args);

        /// <summary>
        /// Log critical message.
        /// <para>
        /// To configure the minimum log level set the AWS_LAMBDA_HANDLER_LOG_LEVEL environment variable. The value should be set
        /// to one of the values in the LogLevel enumeration. The default minimum log level is "Information".
        /// </para>
        /// </summary>
        /// <param name="message">Message to log.</param>
        /// <param name="args">Values to be replaced in log messages that are parameterized.</param>
        void LogCritical(string message, params object[] args) => Log(LogLevel.Critical.ToString(), message, args);

        /// <summary>
        /// Log critical message.
        /// <para>
        /// To configure the minimum log level set the AWS_LAMBDA_HANDLER_LOG_LEVEL environment variable. The value should be set
        /// to one of the values in the LogLevel enumeration. The default minimum log level is "Information".
        /// </para>
        /// </summary>
        /// <param name="exception">Exception to include with the logging.</param>
        /// <param name="message">Message to log.</param>
        /// <param name="args">Values to be replaced in log messages that are parameterized.</param>
        void LogCritical(Exception exception, string message, params object[] args) => Log(LogLevel.Critical.ToString(), exception, message, args);

#endif

    }
}