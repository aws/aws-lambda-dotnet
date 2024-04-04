/*
 * Copyright 2019 Amazon.com, Inc. or its affiliates. All Rights Reserved.
 *
 * Licensed under the Apache License, Version 2.0 (the "License").
 * You may not use this file except in compliance with the License.
 * A copy of the License is located at
 *
 *  http://aws.amazon.com/apache2.0
 *
 * or in the "license" file accompanying this file. This file is distributed
 * on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either
 * express or implied. See the License for the specific language governing
 * permissions and limitations under the License.
 */

using Amazon.Lambda.RuntimeSupport.Bootstrap;
using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


#if NET6_0_OR_GREATER
using Amazon.Lambda.RuntimeSupport.Helpers.Logging;
#endif

namespace Amazon.Lambda.RuntimeSupport.Helpers
{
    /// <summary>
    /// Interface used by bootstrap to format logging message as well as Console WriteLine messages.
    /// </summary>
    public interface IConsoleLoggerWriter
    {
        /// <summary>
        /// The current aws request id
        /// </summary>
        /// <param name="awsRequestId">The AWS request id for the function invocation added to each log message.</param>
        void SetCurrentAwsRequestId(string awsRequestId);

        /// <summary>
        /// Format message with default log level
        /// </summary>
        /// <param name="message">Message to log.</param>
        void FormattedWriteLine(string message);

        /// <summary>
        /// Format message with given log level
        /// </summary>
        /// <param name="level">The level of the log message.</param>
        /// <param name="message">Message to log.</param>
        /// <param name="args">Arguments to be applied to the log message.</param>
        void FormattedWriteLine(string level, string message, params object[] args);

        /// <summary>
        /// Format message with given log level
        /// </summary>
        /// <param name="level">The level of the log message.</param>
        /// <param name="exception">Exception to log.</param>
        /// <param name="message">Message to log.</param>
        /// <param name="args">Arguments to be applied to the log message.</param>
        void FormattedWriteLine(string level, Exception exception, string message, params object[] args);
    }

    /// <summary>
    /// Simple logger to maintain compatibility with versions of .NET before .NET 6
    /// </summary>
    public class SimpleLoggerWriter : IConsoleLoggerWriter
    {
        TextWriter _writer;

        /// <summary>
        /// Default Constructor
        /// </summary>
        public SimpleLoggerWriter()
        {
            // Look to see if Lambda's telemetry log file descriptor is available. If so use that for logging.
            // This will make sure multiline log messages use a single CloudWatch Logs record.
            var fileDescriptorLogId = Environment.GetEnvironmentVariable(Constants.ENVIRONMENT_VARIABLE_TELEMETRY_LOG_FD);
            if (fileDescriptorLogId != null)
            {
                try
                {
                    _writer = FileDescriptorLogFactory.GetWriter(fileDescriptorLogId);
                    InternalLogger.GetDefaultLogger().LogInformation("Using file descriptor stream writer for logging");
                }
                catch (Exception ex)
                {
                    _writer = Console.Out;
                    InternalLogger.GetDefaultLogger().LogError(ex, "Error creating file descriptor log stream writer. Fallback to stdout.");
                }
            }
            else
            {
                _writer = Console.Out;
                InternalLogger.GetDefaultLogger().LogInformation("Using stdout for logging");
            }
        }

        /// <inheritdoc/>
        public void SetCurrentAwsRequestId(string awsRequestId)
        {
        }

        /// <inheritdoc/>
        public void FormattedWriteLine(string message)
        {
            _writer.WriteLine(message);
        }

        /// <inheritdoc/>
        public void FormattedWriteLine(string level, string message, params object[] args)
        {
            _writer.WriteLine(message);
        }

        /// <inheritdoc/>
        public void FormattedWriteLine(string level, Exception exception, string message, params object[] args)
        {
            _writer.WriteLine(message);
            if (exception != null)
            {
                _writer.WriteLine(exception.ToString());
            }
        }
    }

#if NET6_0_OR_GREATER

    /// <summary>
    /// Formats log messages with time, request id, log level and message
    /// </summary>
    public class LogLevelLoggerWriter : IConsoleLoggerWriter
    {
        /// <summary>
        /// A mirror of the LogLevel defined in Amazon.Lambda.Core. The version in
        /// Amazon.Lambda.Core can not be relied on because the Lambda Function could be using
        /// an older version of Amazon.Lambda.Core before LogLevel existed in Amazon.Lambda.Core.
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

        WrapperTextWriter _wrappedStdOutWriter;
        WrapperTextWriter _wrappedStdErrorWriter;

        /// <summary>
        /// Constructor used by bootstrap to put in place a wrapper TextWriter around stdout and stderror so all Console.WriteLine calls
        /// will be formatted.
        ///
        /// Stdout will default log messages to be Information
        /// Stderror will default log messages to be Error
        /// </summary>
        public LogLevelLoggerWriter()
        {
            // Look to see if Lambda's telemetry log file descriptor is available. If so use that for logging.
            // This will make sure multiline log messages use a single CloudWatch Logs record.
            var fileDescriptorLogId = Environment.GetEnvironmentVariable(Constants.ENVIRONMENT_VARIABLE_TELEMETRY_LOG_FD);
            if (fileDescriptorLogId != null)
            {
                try
                {
                    var stdOutWriter = FileDescriptorLogFactory.GetWriter(fileDescriptorLogId);
                    var stdErrorWriter = FileDescriptorLogFactory.GetWriter(fileDescriptorLogId);
                    Initialize(stdOutWriter, stdErrorWriter);
                    InternalLogger.GetDefaultLogger().LogInformation("Using file descriptor stream writer for logging.");
                }
                catch(Exception ex)
                {
                    InternalLogger.GetDefaultLogger().LogError(ex, "Error creating file descriptor log stream writer. Fallback to stdout and stderr.");
                    Initialize(Console.Out, Console.Error);
                }
            }
            else
            {
                Initialize(Console.Out, Console.Error);
                InternalLogger.GetDefaultLogger().LogInformation("Using stdout and stderr for logging.");
            }

            // SetOut will wrap our WrapperTextWriter with a synchronized TextWriter. Pass in the new synchronized
            // TextWriter into our writer to make sure we obtain a lock on that instance before writing to the stdout.
            Console.SetOut(_wrappedStdOutWriter);
            _wrappedStdOutWriter.LockObject = Console.Out;

            Console.SetError(_wrappedStdErrorWriter);
            _wrappedStdErrorWriter.LockObject = Console.Error;

            ConfigureLoggingActionField();
        }

        /// <summary>
        /// Construct an instance wrapping std out and std error.
        /// </summary>
        /// <param name="stdOutWriter"></param>
        /// <param name="stdErrorWriter"></param>
        public LogLevelLoggerWriter(TextWriter stdOutWriter, TextWriter stdErrorWriter)
        {
            Initialize(stdOutWriter, stdErrorWriter);
        }

        private void Initialize(TextWriter stdOutWriter, TextWriter stdErrorWriter)
        {
            _wrappedStdOutWriter = new WrapperTextWriter(stdOutWriter, LogLevel.Information.ToString());
            _wrappedStdErrorWriter = new WrapperTextWriter(stdErrorWriter, LogLevel.Error.ToString());
        }

        /// <summary>
        /// Set a special callback on Amazon.Lambda.Core.LambdaLogger to redirect its logging to FormattedWriteLine.
        /// This allows outputting logging with time and request id but not have LogLevel. This is important for
        /// Amazon.Lambda.Logging.AspNetCore which already provides a string with a log level.
        /// </summary>
        private void ConfigureLoggingActionField()
        {
            var lambdaILoggerType = typeof(Amazon.Lambda.Core.LambdaLogger);
            if (lambdaILoggerType == null)
                return;

            var loggingActionField = lambdaILoggerType.GetTypeInfo().GetField("_loggingAction", BindingFlags.NonPublic | BindingFlags.Static);
            if (loggingActionField == null)
                return;

            Action<string> callback = (message => FormattedWriteLine(null, message));
            loggingActionField.SetValue(null, callback);
        }

        /// <inheritdoc/>
        public void SetCurrentAwsRequestId(string awsRequestId)
        {
            _wrappedStdOutWriter.CurrentAwsRequestId = awsRequestId;
            _wrappedStdErrorWriter.CurrentAwsRequestId = awsRequestId;
        }

        /// <inheritdoc/>
        public void FormattedWriteLine(string message)
        {
            _wrappedStdOutWriter.FormattedWriteLine(message);
        }

        /// <inheritdoc/>
        public void FormattedWriteLine(string level, string message, params object[] args)
        {
            _wrappedStdOutWriter.FormattedWriteLine(level, (Exception)null, message, args);
        }

        /// <inheritdoc/>
        public void FormattedWriteLine(string level, Exception exception, string message, params object[] args)
        {
            _wrappedStdOutWriter.FormattedWriteLine(level, exception, message, args);
        }


        /// <summary>
        /// Wraps around a provided TextWriter. In normal usage the wrapped TextWriter will either be stdout or stderr.
        /// For all calls besides WriteLine and WriteLineAsync call into the wrapped TextWriter. For the WriteLine and WriteLineAsync
        /// format the message with time, request id, log level and the provided message.
        /// </summary>
        class WrapperTextWriter : TextWriter
        {
            private readonly TextWriter _innerWriter;
            private string _defaultLogLevel;

            const string NET_RIC_LOG_LEVEL_ENVIRONMENT_VARIABLE = "AWS_LAMBDA_HANDLER_LOG_LEVEL";
            const string NET_RIC_LOG_FORMAT_ENVIRONMENT_VARIABLE = "AWS_LAMBDA_HANDLER_LOG_FORMAT";

            const string LAMBDA_LOG_LEVEL_ENVIRONMENT_VARIABLE = "AWS_LAMBDA_LOG_LEVEL";
            const string LAMBDA_LOG_FORMAT_ENVIRONMENT_VARIABLE = "AWS_LAMBDA_LOG_FORMAT";

            private LogLevel _minmumLogLevel = LogLevel.Information;

            enum LogFormatType { Default, Unformatted, Json }

            private LogFormatType _logFormatType = LogFormatType.Default;

            private ILogMessageFormatter _logMessageFormatter;

            public string CurrentAwsRequestId { get; set; } = string.Empty;

            /// <summary>
            /// This is typically set to either Console.Out or Console.Error to make sure we acquiring a lock
            /// on that object whenever we are going through FormattedWriteLine. This is important for
            /// logging that goes through ILambdaLogger that skips going through Console.WriteX. Without
            /// this ILambdaLogger only acquires one lock but Console.WriteX acquires 2 locks and we can get deadlocks.
            /// </summary>
            internal object LockObject { get; set; } = new object();

            /// <summary>
            /// Create an instance
            /// </summary>
            /// <param name="innerWriter"></param>
            /// <param name="defaultLogLevel"></param>
            public WrapperTextWriter(TextWriter innerWriter, string defaultLogLevel)
            {
                _innerWriter = innerWriter;
                _defaultLogLevel = defaultLogLevel;

                var envLogLevel = GetEnviromentVariable(NET_RIC_LOG_LEVEL_ENVIRONMENT_VARIABLE, LAMBDA_LOG_LEVEL_ENVIRONMENT_VARIABLE);
                if (!string.IsNullOrEmpty(envLogLevel))
                {
                    // Map Lambda's fatal logging level to the .NET RIC critical
                    if(string.Equals(envLogLevel, "fatal", StringComparison.InvariantCultureIgnoreCase))
                    {
                        _minmumLogLevel = LogLevel.Critical;
                    }
                    else if (Enum.TryParse<LogLevel>(envLogLevel, true, out var result))
                    {
                        _minmumLogLevel = result;
                    }
                    else
                    {
                        InternalLogger.GetDefaultLogger().LogInformation($"Failed to parse log level enum value: {envLogLevel}");
                    }
                }

                var envLogFormat = GetEnviromentVariable(NET_RIC_LOG_FORMAT_ENVIRONMENT_VARIABLE, LAMBDA_LOG_FORMAT_ENVIRONMENT_VARIABLE);
                if (!string.IsNullOrEmpty(envLogFormat))
                {
                    if (Enum.TryParse<LogFormatType>(envLogFormat, true, out var result))
                    {
                        _logFormatType = result;
                    }
                }

                if(_logFormatType == LogFormatType.Json)
                {
                    _logMessageFormatter = new JsonLogMessageFormatter();
                }
                else
                {
                    _logMessageFormatter = new DefaultLogMessageFormatter(_logFormatType != LogFormatType.Unformatted);
                }
            }

            private string GetEnviromentVariable(string envName, string fallbackEnvName)
            {
                var value = Environment.GetEnvironmentVariable(envName);
                if(string.IsNullOrEmpty(value) && fallbackEnvName != null)
                {
                    value = Environment.GetEnvironmentVariable(fallbackEnvName);
                }

                return value;
            }

            internal void FormattedWriteLine(string message)
            {
                FormattedWriteLine(_defaultLogLevel, (Exception)null, message);
            }

            internal void FormattedWriteLine(string level, Exception exeception, string messageTemplate, params object[] args)
            {
                lock(LockObject)
                {
                    var displayLevel = level;
                    if (Enum.TryParse<LogLevel>(level, true, out var levelEnum))
                    {
                        if (levelEnum < _minmumLogLevel)
                            return;
                    }

                    var messageState = new MessageState();

                    messageState.MessageTemplate = messageTemplate;
                    messageState.MessageArguments = args;
                    messageState.TimeStamp = DateTime.UtcNow;
                    messageState.AwsRequestId = CurrentAwsRequestId;
                    messageState.TraceId = Environment.GetEnvironmentVariable(LambdaEnvironment.EnvVarTraceId);
                    messageState.Level = levelEnum;
                    messageState.Exception = exeception;

                    var message = _logMessageFormatter.FormatMessage(messageState);
                    _innerWriter.WriteLine(message);
                }
            }

            private Task FormattedWriteLineAsync(string message)
            {
                FormattedWriteLine(message);
                return Task.CompletedTask;
            }

            #region WriteLine redirects to formatting
            [CLSCompliant(false)]
            public override void WriteLine(ulong value) => FormattedWriteLine(value.ToString(FormatProvider));

            [CLSCompliant(false)]
            public override void WriteLine(uint value) => FormattedWriteLine(value.ToString(FormatProvider));


            public override void WriteLine(string format, params object[] arg) => FormattedWriteLine(string.Format(format, arg));

            public override void WriteLine(string format, object arg0, object arg1, object arg2) => FormattedWriteLine(string.Format(format, arg0, arg1, arg2));

            public override void WriteLine(string format, object arg0) => FormattedWriteLine(string.Format(format, arg0));

            public override void WriteLine(string value) => FormattedWriteLine(value);

            public override void WriteLine(float value) => FormattedWriteLine(value.ToString(FormatProvider));

            public override void WriteLine(string format, object arg0, object arg1) => FormattedWriteLine(string.Format(format, arg0, arg1));

            public override void WriteLine(object value) => FormattedWriteLine(value == null ? String.Empty : value.ToString());


            public override void WriteLine(bool value) => FormattedWriteLine(value.ToString(FormatProvider));

            public override void WriteLine(char value) => FormattedWriteLine(value.ToString(FormatProvider));

            public override void WriteLine(char[] buffer) => FormattedWriteLine(buffer == null ? String.Empty : new string(buffer));

            public override void WriteLine() => FormattedWriteLine(string.Empty);

            public override void WriteLine(decimal value) => FormattedWriteLine(value.ToString(FormatProvider));

            public override void WriteLine(double value) => FormattedWriteLine(value.ToString(FormatProvider));

            public override void WriteLine(int value) => FormattedWriteLine(value.ToString(FormatProvider));

            public override void WriteLine(long value) => FormattedWriteLine(value.ToString(FormatProvider));

            public override void WriteLine(char[] buffer, int index, int count) => FormattedWriteLine(new string(buffer, index, count));

            public override Task WriteLineAsync(char value) => FormattedWriteLineAsync(value.ToString());

            public Task WriteLineAsync(char[] buffer) => FormattedWriteLineAsync(buffer == null ? String.Empty : new string(buffer));

            public override Task WriteLineAsync(char[] buffer, int index, int count) => FormattedWriteLineAsync(new string(buffer, index, count));


            public override Task WriteLineAsync(string value) => FormattedWriteLineAsync(value);
            public override Task WriteLineAsync() => FormattedWriteLineAsync(string.Empty);


            public override void WriteLine(StringBuilder? value) => FormattedWriteLine(value?.ToString());
            public override void WriteLine(ReadOnlySpan<char> buffer) => FormattedWriteLine(new string(buffer));
            public override Task WriteLineAsync(ReadOnlyMemory<char> buffer, CancellationToken cancellationToken = default) => FormattedWriteLineAsync(new string(buffer.Span));
            public override Task WriteLineAsync(StringBuilder? value, CancellationToken cancellationToken = default) => FormattedWriteLineAsync(value?.ToString());

            #endregion

            #region Simple Redirects
            public override Encoding Encoding => _innerWriter.Encoding;

            public override IFormatProvider FormatProvider => _innerWriter.FormatProvider;

            public override string NewLine
            {
                get { return _innerWriter.NewLine; }
                set { _innerWriter.NewLine = value; }
            }

            public override void Close() => _innerWriter.Close();



            public override void Flush() => _innerWriter.Flush();

            public override Task FlushAsync() => _innerWriter.FlushAsync();

            [CLSCompliant(false)]
            public override void Write(ulong value) => _innerWriter.Write(value);

            [CLSCompliant(false)]
            public override void Write(uint value) => _innerWriter.Write(value);


            public override void Write(string format, params object[] arg) => _innerWriter.Write(format, arg);

            public override void Write(string format, object arg0, object arg1, object arg2) => _innerWriter.Write(format, arg0, arg1, arg2);

            public override void Write(string format, object arg0, object arg1) => _innerWriter.Write(format, arg0, arg1);

            public override void Write(string format, object arg0) => _innerWriter.Write(format, arg0);

            public override void Write(string value) => _innerWriter.Write(value);


            public override void Write(object value) => _innerWriter.Write(value);

            public override void Write(long value) => _innerWriter.Write(value);
            public override void Write(int value) => _innerWriter.Write(value);

            public override void Write(double value) => _innerWriter.Write(value);

            public override void Write(decimal value) => _innerWriter.Write(value);

            public override void Write(char[] buffer, int index, int count) => _innerWriter.Write(buffer, index, count);

            public override void Write(char[] buffer) => _innerWriter.Write(buffer);

            public override void Write(char value) => _innerWriter.Write(value);

            public override void Write(bool value) => _innerWriter.Write(value);

            public override void Write(float value) => _innerWriter.Write(value);


            public override Task WriteAsync(string value) => _innerWriter.WriteAsync(value);

            public override Task WriteAsync(char[] buffer, int index, int count) => _innerWriter.WriteAsync(buffer, index, count);

            public Task WriteAsync(char[] buffer) => _innerWriter.WriteAsync(buffer);

            public override Task WriteAsync(char value) => _innerWriter.WriteAsync(value);


            protected override void Dispose(bool disposing) => _innerWriter.Dispose();

            public override void Write(StringBuilder? value) => _innerWriter.Write(value);

            public override void Write(ReadOnlySpan<char> buffer) => _innerWriter.Write(buffer);

            public override Task WriteAsync(StringBuilder? value, CancellationToken cancellationToken = default) => _innerWriter.WriteAsync(value, cancellationToken);

            public override Task WriteAsync(ReadOnlyMemory<char> buffer, CancellationToken cancellationToken = default) => _innerWriter.WriteAsync(buffer, cancellationToken);

            public override ValueTask DisposeAsync() => _innerWriter.DisposeAsync();
            #endregion
        }
    }
#endif
}
