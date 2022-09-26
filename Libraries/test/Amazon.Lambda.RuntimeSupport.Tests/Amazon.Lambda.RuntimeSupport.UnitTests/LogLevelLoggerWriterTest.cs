using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport.Helpers;
using Amazon.Lambda.RuntimeSupport.UnitTests.TestHelpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace Amazon.Lambda.RuntimeSupport.UnitTests
{
#if NET6_0_OR_GREATER
    public class LogLevelLoggerWriterTest
    {
        private class TestMessageEntry : MessageEntry
        {
            public override IReadOnlyList<KeyValuePair<string, object>> State { get; } = new Dictionary<string, object>
            {
                ["stringVal"] = "string",
                ["boolVal"] = true,
                ["intVal"] = int.MaxValue,
                ["dateVal"] = DateTime.UtcNow
            }.ToArray();

            public override Exception Exception { get; } = new Exception("TestMessageEntry test exception");

            public override string ToString() => "TestMessageEntry test message";

            public static TestMessageEntry Instance { get; } = new TestMessageEntry();
        }

        public static IEnumerable<object[]> LogLevels => new List<object[]>
        {
            new object[]{ LogLevel.Trace },
            new object[]{ LogLevel.Debug },
            new object[]{ LogLevel.Information },
            new object[]{ LogLevel.Warning },
            new object[]{ LogLevel.Error },
            new object[]{ LogLevel.Critical }
        };

        [Theory]
        [MemberData(nameof(LogLevels))]
        public void FormattedWriteLine_Unformatted_WritesOnlyTextInLine(LogLevel logLevel)
        {
            using var output = new TestOutputTextWriter();
            var logWriter = new LogLevelLoggerWriter(output, output, logLevel.ToString(), "Unformatted");
            logWriter.SetCurrentAwsRequestId("fake-request");

            const string message = "Test log message";
            logWriter.FormattedWriteLine(logLevel.ToString(), message);

            Assert.Equal(message, output.Lines[0]);
        }

        [Theory]
        [MemberData(nameof(LogLevels))]
        public void FormattedWriteLine_DefaultFormat_WritesTimestampAndRequestId(LogLevel logLevel)
        {
            const string timestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
            using var output = new TestOutputTextWriter();
            var logWriter = new LogLevelLoggerWriter(output, output, logLevel.ToString(), "Default");

            var requestId = Guid.NewGuid().ToString();
            logWriter.SetCurrentAwsRequestId(requestId);

            const string message = "Test log message";
            logWriter.FormattedWriteLine(logLevel.ToString(), message);

            var outputMsg = output.Lines.Single();

            // log should start with timestamp
            Assert.True(DateTime.TryParse(outputMsg.AsSpan().Slice(0, timestampFormat.Length), out _));
            // log should contain request Id
            Assert.Contains(requestId, outputMsg);
            // log should contain level
            Assert.Contains(ConvertLogLevelToLabel(logLevel), outputMsg);
            // log should contain message
            Assert.Contains(message, outputMsg);
        }

        [Theory]
        [MemberData(nameof(LogLevels))]
        public void FormattedWriteLine_JsonFormat_WritesJsonObject(LogLevel logLevel)
        {
            using var output = new TestOutputTextWriter();
            var logWriter = new LogLevelLoggerWriter(output, output, logLevel.ToString(), "Json");
            var requestId = Guid.NewGuid().ToString("");
            logWriter.SetCurrentAwsRequestId(requestId);

            const string message = "Test log message";
            logWriter.FormattedWriteLine(logLevel.ToString(), message);

            // assert json output
            using var json = JsonDocument.Parse(output.Lines.Single());
            Assert.True(DateTime.TryParse(json.RootElement.GetProperty("Timestamp").GetString(), out _));
            Assert.Equal(requestId, json.RootElement.GetProperty("AwsRequestId").GetString());
            Assert.Equal(ConvertLogLevelToLabel(logLevel), json.RootElement.GetProperty("Level").GetString());
            Assert.Equal(message, json.RootElement.GetProperty("Message").GetString());
        }

        [Theory]
        [MemberData(nameof(LogLevels))]
        public void FormattedWriteEntry_Unformatted_WritesOnlyTextInLine(LogLevel logLevel)
        {
            using var output = new TestOutputTextWriter();
            var logWriter = new LogLevelLoggerWriter(output, output, logLevel.ToString(), "Unformatted");
            logWriter.SetCurrentAwsRequestId("fake-request");

            logWriter.FormattedWriteEntry(logLevel, TestMessageEntry.Instance);

            Assert.Equal(TestMessageEntry.Instance.ToString(), output.Lines[0]);
        }

        [Theory]
        [MemberData(nameof(LogLevels))]
        public void FormattedWriteEntry_DefaultFormat_WritesTimestampAndRequestId(LogLevel logLevel)
        {
            const string timestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
            using var output = new TestOutputTextWriter();
            var logWriter = new LogLevelLoggerWriter(output, output, logLevel.ToString(), "Default");

            var requestId = Guid.NewGuid().ToString("");
            logWriter.SetCurrentAwsRequestId(requestId);

            logWriter.FormattedWriteEntry(logLevel, TestMessageEntry.Instance);

            var outputMsg = output.Lines.Single();

            // log should start with timestamp
            Assert.True(DateTime.TryParse(outputMsg.AsSpan().Slice(0, timestampFormat.Length), out _));
            // log should contain request Id
            Assert.Contains(requestId, outputMsg);
            // log should contain level
            Assert.Contains(ConvertLogLevelToLabel(logLevel), outputMsg);
            // log should contain message
            Assert.Contains(TestMessageEntry.Instance.ToString(), outputMsg);
        }

        [Theory]
        [MemberData(nameof(LogLevels))]
        public void FormattedWriteEntry_JsonFormat_WritesJsonObject(LogLevel logLevel)
        {
            using var output = new TestOutputTextWriter();
            var logWriter = new LogLevelLoggerWriter(output, output, logLevel.ToString(), "Json");
            var requestId = Guid.NewGuid().ToString();
            logWriter.SetCurrentAwsRequestId(requestId);

            logWriter.FormattedWriteEntry(logLevel, TestMessageEntry.Instance);

            // assert json output
            using var json = JsonDocument.Parse(output.Lines.Single());
            Assert.True(DateTime.TryParse(json.RootElement.GetProperty("Timestamp").GetString(), out _));
            Assert.Equal(requestId, json.RootElement.GetProperty("AwsRequestId").GetString());
            Assert.Equal(ConvertLogLevelToLabel(logLevel), json.RootElement.GetProperty("Level").GetString());
            Assert.Equal(TestMessageEntry.Instance.ToString(), json.RootElement.GetProperty("Message").GetString());
            Assert.Contains(TestMessageEntry.Instance.Exception.Message, json.RootElement.GetProperty("Exception").GetString());

            var jsonState = json.RootElement.GetProperty("State");
            foreach (var stateProperty in TestMessageEntry.Instance.State)
            {
                var jsonValue = jsonState.GetProperty(stateProperty.Key);
                switch (stateProperty.Value)
                {
                    case DateTime:
                        Assert.True(DateTime.TryParse(jsonValue.GetString(), out _));
                        break;
                    case string stringVal:
                        Assert.Equal(stringVal, jsonValue.GetString());
                        break;
                    case bool boolVal:
                        Assert.Equal(boolVal, jsonValue.GetBoolean());
                        break;
                    case int intVal:
                        Assert.Equal(intVal, jsonValue.GetInt32());
                        break;
                    default:
                        Assert.True(false, "Unexpected value type");
                        break;
                }
            }
        }

        [Theory]
        [InlineData(LogLevel.Information, LogLevel.Debug)]
        [InlineData(LogLevel.Information, LogLevel.Trace)]
        [InlineData(LogLevel.Warning, LogLevel.Information)]
        [InlineData(LogLevel.Error, LogLevel.Information)]
        public void FormattedWriteEntry_LowerLevel_WritesJsonObject(LogLevel minLevel, LogLevel writeLevel)
        {
            using var output = new TestOutputTextWriter();
            var logWriter = new LogLevelLoggerWriter(output, output, minLevel.ToString(), "Json");

            logWriter.FormattedWriteEntry(writeLevel, TestMessageEntry.Instance);
            Assert.Empty(output.Lines);
        }

        private static string ConvertLogLevelToLabel(LogLevel level)
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
    }
#endif
}
