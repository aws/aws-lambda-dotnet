using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Amazon.Lambda.Core;

namespace Amazon.Lambda.TestUtilities
{
    /// <summary>
    /// An implementation if ILambdaLogger that stores all the messages in a buffer and writes the messages to the console.
    /// </summary>
    public class TestLambdaLogger : ILambdaLogger
    {
        /// <summary>
        /// Buffer for all the log messages written to the logger.
        /// </summary>
        public StringBuilder Buffer { get; } = new StringBuilder();

        /// <summary>
        /// Write log messages to the console.
        /// </summary>
        /// <param name="message"></param>
        public void Log(string message)
        {
            LogLine(message);
        }

        /// <summary>
        /// Write log messages to the console.
        /// </summary>
        /// <param name="message"></param>
        public void LogLine(string message)
        {
            Buffer.AppendLine(message);
            Console.WriteLine(message);
        }
    }
}
