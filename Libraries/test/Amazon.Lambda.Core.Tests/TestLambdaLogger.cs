using Amazon.Lambda.Core;

namespace Amazon.Lambda.Tests
{
    /// <summary>
    /// Implementation of <see cref="ILambdaLogger"/> that keeps the entry being logged.
    /// </summary>
    internal class TestLambdaLogger : ILambdaLogger
    {
        public string Level { get; private set; }
        public object Entry { get; private set; }

        public void Log(string message) => Entry = message;

        public void LogLine(string message) => Entry = message;

        public void LogEntry<TEntry>(string level, TEntry entry)
        {
            Level = level;
            Entry = entry;
        }
    }
}
