#if NET6_0_OR_GREATER
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport.Helpers;
using System;
using System.Buffers.Binary;
using System.Globalization;
using System.IO;
using System.Text;
using Xunit;

namespace Amazon.Lambda.RuntimeSupport.UnitTests
{
    public class LogLevelLoggerWriterTest
    {
        [Fact]
        public void WriteBytesUnformattedShouldWriteLogFrame()
        {
            using var outputStream = new MemoryStream();
            using var streamWriter = FileDescriptorLogFactory.InitializeWriter(outputStream);

            const string logMessage = "hello world\nsomething else on a new line.";
            var loggerWriter = new LogLevelLoggerWriter(streamWriter, streamWriter, streamWriter.BaseStream);
            loggerWriter.SetLogFormatType(LogFormatType.Unformatted);
            loggerWriter.FormattedWriteBytes(null, streamWriter.Encoding.GetBytes(logMessage));

            AssertLogFrame(outputStream.ToArray(), m => Assert.Equal(m, logMessage));
        }

        [Fact]
        public void WriteBytesFormattedShouldWriteFormattedLogFrame()
        {
            const string logMessage = "hello world\nsomething else on a new line.";

            using var outputStream = new MemoryStream();
            using var streamWriter = FileDescriptorLogFactory.InitializeWriter(outputStream);

            var requestId = Guid.NewGuid().ToString();
            var loggerWriter = new LogLevelLoggerWriter(streamWriter, streamWriter, streamWriter.BaseStream);
            loggerWriter.SetCurrentAwsRequestId(requestId);
            loggerWriter.SetLogFormatType(LogFormatType.Default);
            loggerWriter.FormattedWriteBytes(null, streamWriter.Encoding.GetBytes(logMessage));

            AssertLogFrame(outputStream.ToArray(), actualMsg =>
            {
                // make sure that the message starts with a valid datetime
                const string dateFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
                var dateString = actualMsg.Substring(0, dateFormat.Length);
                DateTime.ParseExact(dateString, dateFormat, CultureInfo.InvariantCulture);

                // output should contains request ID
                Assert.Contains(requestId, actualMsg);

                // output should end with the actual message
                Assert.EndsWith(logMessage, actualMsg);
            });
        }

        [Fact]
        public void LambdaLoggerShouldWriteToLogLevelLoggerWriter()
        {
            using var outputStream = new MemoryStream();
            using var streamWriter = FileDescriptorLogFactory.InitializeWriter(outputStream);

            const string logMessage = "hello world\nsomething else on a new line.";
            var loggerWriter = new LogLevelLoggerWriter(streamWriter, streamWriter, streamWriter.BaseStream);
            loggerWriter.SetLogFormatType(LogFormatType.Unformatted);

            LambdaLogger.Log(streamWriter.Encoding.GetBytes(logMessage));

            // verify that the log message is directed to LogLevelLoggerWriter 
            AssertLogFrame(outputStream.ToArray(), m => Assert.Equal(m, logMessage));
        }

        private static void AssertLogFrame(ReadOnlySpan<byte> frame, Action<string> messageAssertion)
        {
            Assert.True(frame.Length >= 8);

            var frameType = BinaryPrimitives.ReadUInt32BigEndian(frame.Slice(0, 4));
            Assert.Equal(frameType, FileDescriptorLogFactory.LambdaTelemetryLogHeaderFrameType);

            var length = BinaryPrimitives.ReadInt32BigEndian(frame.Slice(4, 4));
            var actualMessage = new UTF8Encoding(false, false).GetString(frame.Slice(8, length));
            messageAssertion(actualMessage);
        }
    }
}
#endif
