using System.Text;
using Amazon.Lambda.Core;

namespace Amazon.Lambda.TestTool.Runtime.LambdaMocks
{
    public class LocalLambdaLogger : ILambdaLogger
    {
        private StringBuilder _buffer = new StringBuilder();
        
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