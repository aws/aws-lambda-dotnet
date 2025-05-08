using System;
using System.Text;
using Amazon.Lambda.Core;

namespace Amazon.Lambda.TestTool.Runtime.LambdaMocks
{
    public class LocalLambdaLogger : ILambdaLogger
    {
        private StringBuilder _buffer = new StringBuilder();

        public void Log(string level, string message)
        {
            _buffer.AppendLine($"Level = {level}, Message = {message}");
        }

        public void Log(string level, string message, params object[] args)
        {
            _buffer.Append($"Level = {level}, Message = {message}");
            if (args?.Length > 0)
                _buffer.AppendLine($", Arguments = {string.Join(',', args)}");
            else
                _buffer.AppendLine();
        }

        public void Log(string level, Exception exception, string message, params object[] args)
        {

            _buffer.Append($"Level = {level}, Message = {message}");
            if (args?.Length > 0)
                _buffer.AppendLine($", Arguments = {string.Join(',', args)}");
            else
                _buffer.AppendLine();

            _buffer.AppendLine(exception.ToString());
        }

        public void Log(string message)
        {
            _buffer.Append(message);
        }

        public void LogLine(string message)
        {
            _buffer.AppendLine(message);
        }

        public string Buffer
        {
            get { return this._buffer.ToString(); }
        }
    }
}