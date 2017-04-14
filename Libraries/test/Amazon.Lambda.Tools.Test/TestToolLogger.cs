using System;
using System.Collections.Generic;
using System.Text;

namespace Amazon.Lambda.Tools.Test
{
    public class TestToolLogger : IToolLogger
    {
        StringBuilder _buffer = new StringBuilder();
        public void WriteLine(string message)
        {
            this._buffer.AppendLine(message);
            Console.WriteLine(message);
        }

        public void ClearBuffer()
        {
            this._buffer.Clear();
        }

        public string Buffer
        {
            get { return this._buffer.ToString(); }
        }
    }
}
