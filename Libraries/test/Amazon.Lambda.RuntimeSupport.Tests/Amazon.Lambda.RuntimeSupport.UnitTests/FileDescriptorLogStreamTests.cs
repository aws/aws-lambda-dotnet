using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Amazon.Lambda.RuntimeSupport.Helpers;
using Amazon.Lambda.RuntimeSupport.UnitTests.TestHelpers;
using Xunit;

namespace Amazon.Lambda.RuntimeSupport.UnitTests
{
    public class FileDescriptorLogStreamTests
    {
        private const int HeaderLength = FileDescriptorLogFactory.LambdaTelemetryLogHeaderLength;
        private const int LogEntryMaxLength = FileDescriptorLogFactory.MaxCloudWatchLogEventSize;

        private static readonly byte[] ExpectedMagicBytes =
        {
            0xA5, 0x5A, 0x00, 0x03
        };

        [Fact]
        public void MultilineLoggingInSingleLogEntryWithTlvFormat()
        {
            var logs = new List<byte[]>();
            var offsets = new List<int>();
            var counts = new List<int>();
            var stream = new TestFileStream((log, offset, count) =>
            {
                logs.Add(log);
                offsets.Add(offset);
                counts.Add(count);
            });
            TextWriter writer = FileDescriptorLogFactory.InitializeWriter(stream);
            // assert that initializing the stream does not result in UTF-8 preamble log entry
            Assert.Empty(counts);
            Assert.Empty(offsets);
            Assert.Empty(logs);

            const string logMessage = "hello world\nsomething else on a new line.";
            int logMessageLength = logMessage.Length;
            writer.Write(logMessage);

            Assert.Equal(2, offsets.Count);
            int headerLogEntryOffset = offsets[0];
            int consoleLogEntryOffset = offsets[1];
            Assert.Equal(0, headerLogEntryOffset);
            Assert.Equal(0, consoleLogEntryOffset);

            Assert.Equal(2, counts.Count);
            int headerLogEntrySize = counts[0];
            int consoleLogEntrySize = counts[1];
            Assert.Equal(HeaderLength, headerLogEntrySize);
            Assert.Equal(logMessageLength, consoleLogEntrySize);

            Assert.Equal(2, logs.Count);
            byte[] headerLogEntry = logs[0];
            byte[] consoleLogEntry = logs[1];
            Assert.Equal(HeaderLength, headerLogEntry.Length);
            Assert.Equal(logMessageLength, consoleLogEntry.Length);

            byte[] expectedLengthBytes =
            {
                0x00, 0x00, 0x00, 0x29
            };
            AssertHeaderBytes(headerLogEntry, expectedLengthBytes);
            Assert.Equal(logMessage, Encoding.UTF8.GetString(consoleLogEntry));
        }

        [Fact]
        public void MaxSizeProducesOneLogFrame()
        {
            var logs = new List<byte[]>();
            var offsets = new List<int>();
            var counts = new List<int>();
            var stream = new TestFileStream((log, offset, count) =>
            {
                logs.Add(log);
                offsets.Add(offset);
                counts.Add(count);
            });
            TextWriter writer = FileDescriptorLogFactory.InitializeWriter(stream);
            // assert that initializing the stream does not result in UTF-8 preamble log entry
            Assert.Empty(counts);
            Assert.Empty(offsets);
            Assert.Empty(logs);

            string logMessage = new string('a', LogEntryMaxLength - 1) + "b";
            writer.Write(logMessage);

            Assert.Equal(2, offsets.Count);
            int headerLogEntryOffset = offsets[0];
            int consoleLogEntryOffset = offsets[1];
            Assert.Equal(0, headerLogEntryOffset);
            Assert.Equal(0, consoleLogEntryOffset);

            Assert.Equal(2, counts.Count);
            int headerLogEntrySize = counts[0];
            int consoleLogEntrySize = counts[1];
            Assert.Equal(HeaderLength, headerLogEntrySize);
            Assert.Equal(LogEntryMaxLength, consoleLogEntrySize);

            Assert.Equal(2, logs.Count);
            byte[] headerLogEntry = logs[0];
            byte[] consoleLogEntry = logs[1];
            Assert.Equal(HeaderLength, headerLogEntry.Length);
            Assert.Equal(LogEntryMaxLength, consoleLogEntry.Length);

            byte[] expectedLengthBytes =
            {
                0x00, 0x03, 0xFF, 0xE6
            };
            AssertHeaderBytes(headerLogEntry, expectedLengthBytes);
            Assert.Equal(logMessage, Encoding.UTF8.GetString(consoleLogEntry));
        }

        [Fact]
        public void LogEntryAboveMaxSizeProducesMultipleLogFrames()
        {
            var logs = new List<byte[]>();
            var offsets = new List<int>();
            var counts = new List<int>();
            var stream = new TestFileStream((log, offset, count) =>
            {
                logs.Add(log);
                offsets.Add(offset);
                counts.Add(count);
            });
            TextWriter writer = FileDescriptorLogFactory.InitializeWriter(stream);
            // assert that initializing the stream does not result in UTF-8 preamble log entry
            Assert.Empty(counts);
            Assert.Empty(offsets);
            Assert.Empty(logs);

            string logMessage = new string('a', LogEntryMaxLength) + "b";
            writer.Write(logMessage);

            Assert.Equal(4, offsets.Count);
            int headerLogEntryOffset = offsets[0];
            int consoleLogEntryOffset = offsets[1];
            int headerLogSecondEntryOffset = offsets[2];
            int consoleLogSecondEntryOffset = offsets[3];
            Assert.Equal(0, headerLogEntryOffset);
            Assert.Equal(0, consoleLogEntryOffset);
            Assert.Equal(0, headerLogSecondEntryOffset);
            Assert.Equal(0, consoleLogSecondEntryOffset);

            Assert.Equal(4, counts.Count);
            int headerLogEntrySize = counts[0];
            int consoleLogEntrySize = counts[1];
            int headerLogSecondEntrySize = counts[2];
            int consoleLogSecondEntrySize = counts[3];
            Assert.Equal(HeaderLength, headerLogEntrySize);
            Assert.Equal(LogEntryMaxLength, consoleLogEntrySize);
            Assert.Equal(HeaderLength, headerLogSecondEntrySize);
            Assert.Equal(1, consoleLogSecondEntrySize);

            Assert.Equal(4, logs.Count);
            byte[] headerLogEntry = logs[0];
            byte[] consoleLogEntry = logs[1];
            byte[] headerLogSecondEntry = logs[2];
            byte[] consoleLogSecondEntry = logs[3];
            Assert.Equal(HeaderLength, headerLogEntry.Length);
            Assert.Equal(LogEntryMaxLength, consoleLogEntry.Length);
            Assert.Equal(HeaderLength, headerLogSecondEntry.Length);
            Assert.Single(consoleLogSecondEntry);

            byte[] expectedLengthBytes =
            {
                0x00, 0x03, 0xFF, 0xE6
            };
            AssertHeaderBytes(headerLogEntry, expectedLengthBytes);

            byte[] expectedLengthBytesSecondEntry =
            {
                0x00, 0x00, 0x00, 0x01
            };
            AssertHeaderBytes(headerLogSecondEntry, expectedLengthBytesSecondEntry);
            string expectedLogEntry = logMessage.Substring(0, LogEntryMaxLength);
            string expectedSecondLogEntry = logMessage.Substring(LogEntryMaxLength);
            Assert.Equal(expectedLogEntry, Encoding.UTF8.GetString(consoleLogEntry));
            Assert.Equal(expectedSecondLogEntry, Encoding.UTF8.GetString(consoleLogSecondEntry));
        }

        private static void AssertHeaderBytes(byte[] buf, byte[] expectedLengthBytes)
        {
            byte[] actualHeaderMagicBytes = buf.Take(4).ToArray();
            byte[] actualHeaderLengthBytes = buf.Skip(4).Take(4).ToArray();
            Assert.Equal(ExpectedMagicBytes, actualHeaderMagicBytes);
            Assert.Equal(expectedLengthBytes, actualHeaderLengthBytes);
        }
    }
}
