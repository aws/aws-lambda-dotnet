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
        /// Write log messages to the console and the Buffer without appending a newline terminator.
        /// </summary>
        /// <param name="message"></param>
        public void Log(string message)
        {
            Buffer.Append(message);
            Console.Write(message);
        }

        /// <summary>
        /// Write log messages to the console and the Buffer with a newline terminator.
        /// </summary>
        /// <param name="message"></param>
        public void LogLine(string message)
        {
            Buffer.AppendLine(message);
            Console.WriteLine(message);
        }

        /// <summary>
        /// Write log messages to the console and the Buffer.
        /// </summary>
        /// <param name="level"></param>
        /// <param name="message"></param>
        public void Log(string level, string message)
        {
            var formmattedString = $"{level}: {message}";
            Buffer.AppendLine(formmattedString);
            Console.WriteLine(formmattedString);
        }

        /// <summary>
        /// Write log messages to the console and the Buffer.
        /// </summary>
        /// <param name="level"></param>
        /// <param name="message"></param>
        /// <param name="args"></param>
        public void Log(string level, string message, params object[] args)
        {
            var builder = new StringBuilder();
            builder.Append($"{level}: {message}");
            if (args != null && args.Length > 0)
            {
                builder.AppendLine();
                foreach (var arg in args)
                {
                    builder.AppendLine($"\t{arg}");
                }
            }

            var formmattedString = builder.ToString();
            Buffer.AppendLine(formmattedString);
            Console.WriteLine(formmattedString);
        }

        /// <summary>
        /// Write log messages to the console and the Buffer.
        /// </summary>
        /// <param name="level"></param>
        /// <param name="exception"></param>
        /// <param name="message"></param>
        /// <param name="args"></param>
        public void Log(string level, Exception exception, string message, params object[] args)
        {
            var builder = new StringBuilder();
            builder.Append($"{level}: {message}");
            if (args != null && args.Length > 0)
            {
                builder.AppendLine();
                foreach (var arg in args)
                {
                    builder.AppendLine($"\t{arg}");
                }
            }
            if (exception != null)
            {
                builder.AppendLine();
                builder.AppendLine(exception.ToString());
            }

            var formmattedString = builder.ToString();
            Buffer.AppendLine(formmattedString);
            Console.WriteLine(formmattedString);
        }
    }
}
