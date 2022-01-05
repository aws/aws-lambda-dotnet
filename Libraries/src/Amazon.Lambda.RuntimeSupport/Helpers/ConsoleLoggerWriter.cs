using Amazon.Lambda.RuntimeSupport.Bootstrap;
using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Amazon.Lambda.RuntimeSupport.Helpers
{
    /// <summary>
    /// Interface used by bootstrap to format logging message as well as Console Writeline messages.
    /// </summary>
    public interface IConsoleLoggerWriter
    {
        /// <summary>
        /// The current aws request id
        /// </summary>
        /// <param name="awsRequestId"></param>
        void SetCurrentAwsRequestId(string awsRequestId);

        /// <summary>
        /// Format message with default log level
        /// </summary>
        /// <param name="message"></param>
        void FormattedWriteLine(string message);

        /// <summary>
        /// Format message with given log level
        /// </summary>
        /// <param name="level"></param>
        /// <param name="message"></param>
        void FormattedWriteLine(string level, string message);
    }

    /// <summary>
    /// Simple logger to maintain compatiblity with verisons of .NET before .NET 6
    /// </summary>
    public class SimpleLoggerWriter : IConsoleLoggerWriter
    {
        public void SetCurrentAwsRequestId(string awsRequestId)
        {

        }

        public void FormattedWriteLine(string message)
        {
            Console.WriteLine(message);
        }

        public void FormattedWriteLine(string level, string message)
        {
            Console.WriteLine(message);
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
        enum LogLevel
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

        TextWriter _consoleStdOutWriter;
        TextWriter _consoleErrorWriter;

        WrapperTextWriter _wrappedStdOutWriter;
        WrapperTextWriter _wrappedStdErrorWriter;

        /// <summary>
        /// Constructor used by bootstrap to put in place a wrapper TextWriter around stdout and stderror so all Console.Writeline calls
        /// will be formatted.
        /// 
        /// Stdoud will default log messages to be Information
        /// Stderror will default log messages to be Error
        /// </summary>
        public LogLevelLoggerWriter()
            : this(Console.Out, Console.Error)
        {
            // SetOut will wrap our WrapperTextWriter with a synchronized TextWriter. Pass in the new synchronized
            // TextWriter into our writer to make sure we obtain a lock on that instance before writing to the stdout.
            Console.SetOut(_wrappedStdOutWriter);
            _wrappedStdOutWriter.LockObject = Console.Out;

            Console.SetError(_wrappedStdErrorWriter);
            _wrappedStdErrorWriter.LockObject = Console.Error;

            ConfigureLoggingActionField();
        }

        public LogLevelLoggerWriter(TextWriter stdOutWriter, TextWriter stdErrorWriter)
        {
            _consoleStdOutWriter = stdOutWriter;
            _wrappedStdOutWriter = new WrapperTextWriter(_consoleStdOutWriter, LogLevel.Information.ToString());

            _consoleErrorWriter = stdErrorWriter;
            _wrappedStdErrorWriter = new WrapperTextWriter(_consoleErrorWriter, LogLevel.Error.ToString());
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

        public void SetCurrentAwsRequestId(string awsRequestId)
        {
            _wrappedStdOutWriter.CurrentAwsRequestId = awsRequestId;
            _wrappedStdErrorWriter.CurrentAwsRequestId = awsRequestId;
        }

        public void FormattedWriteLine(string message)
        {
            _wrappedStdOutWriter.FormattedWriteLine(message);
        }

        public void FormattedWriteLine(string level, string message)
        {
            _wrappedStdOutWriter.FormattedWriteLine(level, message);
        }


        /// <summary>
        /// Wraps around a provided TextWriter. In normal usage the wrapped TextWriter will either be stdout or stderr. 
        /// For all calls besides Writeline and WritelineAsync call into the wrapped TextWriter. For the Writeline and WritelineAsync
        /// format the message with time, request id, log level and the provided message.
        /// </summary>
        class WrapperTextWriter : TextWriter
        {
            private readonly TextWriter _innerWriter;
            private string _defaultLogLevel;

            const string LOG_LEVEL_ENVIRONMENT_VARAIBLE = "AWS_LAMBDA_HANDLER_LOG_LEVEL";
            const string LOG_FORMAT_ENVIRONMENT_VARAIBLE = "AWS_LAMBDA_HANDLER_LOG_FORMAT";

            private LogLevel _minmumLogLevel = LogLevel.Information;

            enum LogFormatType { Default, Unformatted }

            private LogFormatType _logFormatType = LogFormatType.Default;

            public string CurrentAwsRequestId { get; set; } = string.Empty;

            /// <summary>
            /// This is typically set to either Console.Out or Console.Error to make sure we acquiring a lock
            /// on that object whenever we are going through FormattedWriteLine. This is important for 
            /// logging that goes through ILambdaLogger that skips going through Console.WriteX. Without
            /// this ILambdaLogger only acquries one lock but Console.WriteX acquires 2 locks and we can get deadlocks.
            /// </summary>
            internal object LockObject { get; set; } = new object();

            public WrapperTextWriter(TextWriter innerWriter, string defaultLogLevel)
            {
                _innerWriter = innerWriter;
                _defaultLogLevel = defaultLogLevel;

                var envLogLevel = Environment.GetEnvironmentVariable(LOG_LEVEL_ENVIRONMENT_VARAIBLE);
                if (!string.IsNullOrEmpty(envLogLevel))
                {
                    if (Enum.TryParse<LogLevel>(envLogLevel, true, out var result))
                    {
                        _minmumLogLevel = result;
                    }
                }

                var envLogFormat = Environment.GetEnvironmentVariable(LOG_FORMAT_ENVIRONMENT_VARAIBLE);
                if (!string.IsNullOrEmpty(envLogFormat))
                {
                    if (Enum.TryParse<LogFormatType>(envLogFormat, true, out var result))
                    {
                        _logFormatType = result;
                    }
                }
            }

            internal void FormattedWriteLine(string message)
            {
                FormattedWriteLine(_defaultLogLevel, message);
            }

            internal void FormattedWriteLine(string level, string message)
            {
                lock(LockObject)
                {
                    var displayLevel = level;
                    if (Enum.TryParse<LogLevel>(level, true, out var levelEnum))
                    {
                        if (levelEnum < _minmumLogLevel)
                            return;

                        displayLevel = ConvertLogLevelToLabel(levelEnum);
                    }

                    if (_logFormatType == LogFormatType.Unformatted)
                    {
                        _innerWriter.WriteLine(message);
                    }
                    else
                    {
                        string line;
                        if (!string.IsNullOrEmpty(displayLevel))
                        {
                            line = $"{DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")}\t{CurrentAwsRequestId}\t{displayLevel}\t{message ?? string.Empty}";
                        }
                        else
                        {
                            line = $"{DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")}\t{CurrentAwsRequestId}\t{message ?? string.Empty}";
                        }

                        _innerWriter.WriteLine(line);
                    }
                }
            }

            private Task FormattedWriteLineAsync(string message)
            {
                FormattedWriteLine(message);
                return Task.CompletedTask;
            }

            /// <summary>
            /// Convert LogLevel enums to the the same string label that console provider for Microsoft.Extensions.Logging.ILogger uses.
            /// </summary>
            /// <param name="level"></param>
            /// <returns></returns>
            private string ConvertLogLevelToLabel(LogLevel level)
            {
                switch (level)
                {
                    case LogLevel.Trace:
                        return "trce";
                    case LogLevel.Debug:
                        return "dbug";
                    case LogLevel.Information:
                        return "info";
                    case LogLevel.Warning:
                        return "warn";
                    case LogLevel.Error:
                        return "fail";
                    case LogLevel.Critical:
                        return "crit";
                }

                return level.ToString();
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
