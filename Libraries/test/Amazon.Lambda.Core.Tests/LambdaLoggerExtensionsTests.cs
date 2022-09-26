using Moq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using Xunit;
using static Amazon.Lambda.Core.LambdaLoggerExtensions;

namespace Amazon.Lambda.Core.Tests
{
#if NET6_0_OR_GREATER
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "xUnit1026:Theory methods should use all of their parameters", Justification = "Reuse test data")]
    public class LambdaLoggerExtensionsTests
    {
        public static IEnumerable<object[]> FormattedLogTestData => new List<object[]>
        {
            new object[] { "a {message}", new object[] { "message" }, new string[] { "message" }, "a message", },
            new object[] { "a {message} b {message}", new object[] { "messageA", "messageB" }, new string[] { "message", "message" }, "a messageA b messageB", },
            new object[] { "a {message}", new object[] { "message", "extra Message", "another message" }, new string[] { "message" }, "a message", },
            new object[] {
                "string: {stringMessage}, number: {intMessage,5}}}, bool: {boolMessage}, date: {dateMessage:O}",
                new object[] { "some event", 345, true, new DateTime(2022,9,9,9,9,9,DateTimeKind.Utc)},
                new string[] { "stringMessage", "intMessage", "boolMessage", "dateMessage" },
                string.Format("string: {0}, number: {1,5}}}, bool: {2}, date: {3:O}",
                    new object[] { "some event", 345, true, new DateTime(2022,9,9,9,9,9,DateTimeKind.Utc)})
            }
        };

        public static IEnumerable<object[]> LogFormatParserTestData => new List<object[]>
        {
            new object[] { "a {message}", "a {0}", new List<string> { "message" } },
            new object[] { "a0 {message0 with space} a1 {message1}}} {message2} a3:}{message3}", "a0 {0} a1 {1}}} {2} a3:}{3}", new List<string> { "message0 with space", "message1", "message2", "message3" } },
            new object[] { "a0 {{abc}}abc {message0} a1 {message1}", "a0 {{abc}}abc {0} a1 {1}", new List<string> { "message0", "message1" } },
            new object[] { "a0 {abc}}abc {message1} a1 {message2}", "a0 {0}}abc {1} a1 {2}", new List<string> { "abc", "message1", "message2" } },
            new object[] { "a } {message}", "a } {0}", new List<string> { "message" } },
            new object[] { "a0 {same} a1 {same}", "a0 {0} a1 {1}", new List<string> { "same", "same" } },
            new object[] { "a0 {message0} a1 {message1:format1}", "a0 {0} a1 {1:format1}", new List<string> { "message0", "message1" } },
            new object[] { "a0 {message0,1234:format0} a1 {message1,-123:format1}", "a0 {0,1234:format0} a1 {1,-123:format1}", new List<string> { "message0", "message1" } },
        };

        public static IEnumerable<object[]> LogMethodNoExceptionTestData => new List<object[]>
        {
            new object[] { LogLevel.Trace, nameof(LambdaLoggerExtensions.Trace) },
            new object[] { LogLevel.Debug, nameof(LambdaLoggerExtensions.Debug) },
            new object[] { LogLevel.Information, nameof(LambdaLoggerExtensions.Info) },
            new object[] { LogLevel.Warning, nameof(LambdaLoggerExtensions.Warning) },
            new object[] { LogLevel.Error, nameof(LambdaLoggerExtensions.Error) },
            new object[] { LogLevel.Critical, nameof(LambdaLoggerExtensions.Critical) }
        };

        public static IEnumerable<object[]> LogMethodWithExceptionTestData => new List<object[]>
        {
            new object[] { LogLevel.Error, nameof(LambdaLoggerExtensions.Error) },
            new object[] { LogLevel.Critical, nameof(LambdaLoggerExtensions.Critical) }
        };

        [Fact]
        public void MessageFormatter_NullFormat_ThrowsNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new MessageFormatter(null));
        }

        [Theory]
        [MemberData(nameof(LogFormatParserTestData))]
        public void MessageFormatter_ParseLogFormatString_ReturnsStringFormatSyntax(string logFormat, string expectedOutput, List<string> expectedNames)
        {
            var names = new List<string>();
            var outputFormat = LambdaLoggerExtensions.MessageFormatter.ParseLogFormatString(logFormat, names);

            Assert.Equal(expectedOutput, outputFormat);
            Assert.Equal(expectedNames, names);
        }

        [Fact]
        public void FormattedMessageEntry_NullFormat_ReturnsNull()
        {
            var entry = new FormattedMessageEntry(null, null, Array.Empty<object>());
            Assert.Null(entry.ToString());
        }

        [Fact]
        public void FormattedMessageEntry_Ctor_CacheFull_DoesNotAddToCache()
        {
            var formatterCache = new ConcurrentDictionary<string, MessageFormatter>();
            formatterCache["format1"] = new MessageFormatter("format1");

            _ = new FormattedMessageEntry("format2", null, Array.Empty<object>(), formatterCache, 1);
            Assert.False(formatterCache.ContainsKey("format2"));
        }

        [Theory]
        [MemberData(nameof(FormattedLogTestData))]
        public void FormattedMessageEntry_GetState_ReturnsNameValuePairs(string logFormat, object[] parameters, string[] expectedParamNames, string _)
        {
            var entry = new FormattedMessageEntry(logFormat, null, parameters);
            var state = entry.State;

            var expectedState = new List<KeyValuePair<string, object>>();
            for (var i = 0; i < expectedParamNames.Length; i++)
            {
                expectedState.Add(new KeyValuePair<string, object>(expectedParamNames[i], parameters[i]));
            }

            Assert.Equal(expectedState.Count, state.Count);
            Assert.All(state, p => Assert.Contains(expectedState, x => x.Key == p.Key && x.Value == p.Value));
        }

        [Theory]
        [MemberData(nameof(FormattedLogTestData))]
        public void FormattedMessageEntry_ToString_ReturnsFormattedString(string logFormat, object[] parameters, string[] _, string expectedOutput)
        {
            var entry = new FormattedMessageEntry(logFormat, null, parameters);
            Assert.Equal(expectedOutput, entry.ToString());
        }

        [Fact]
        public void FormattedMessageEntry_WithException_ReturnsException()
        {
            var exception = new Exception();
            var entry = new FormattedMessageEntry(null, exception, Array.Empty<object>());

            Assert.Equal(exception, entry.Exception);
        }

        [Theory]
        [MemberData(nameof(LogMethodNoExceptionTestData))]
        public void LogMethod_NoException_CallsLogEntry(LogLevel logLevel, string logMethodName)
        {
            var loggerMock = new Mock<ILambdaLogger>();
            loggerMock
                .Setup(l => l.LogEntry(It.IsAny<LogLevel>(), It.IsAny<FormattedMessageEntry>()))
                .Verifiable();

            var method = typeof(LambdaLoggerExtensions).GetMethod(logMethodName, BindingFlags.Static | BindingFlags.Public, new Type[]
            {
                typeof(ILambdaLogger),
                typeof(string),
                typeof(object[])
            });

            method.Invoke(null, new object[] { loggerMock.Object, "format", Array.Empty<object>() });
            loggerMock.Verify(l => l.LogEntry(It.Is<LogLevel>(l => l == logLevel), It.IsAny<FormattedMessageEntry>()), Times.Once);
        }

        [Theory]
        [MemberData(nameof(LogMethodWithExceptionTestData))]
        public void LogMethod_WithException_CallsLogEntry(LogLevel logLevel, string logMethodName)
        {
            var exception = new Exception();
            var loggerMock = new Mock<ILambdaLogger>();
            loggerMock
                .Setup(l => l.LogEntry(It.IsAny<LogLevel>(), It.IsAny<FormattedMessageEntry>()))
                .Verifiable();

            var method = typeof(LambdaLoggerExtensions).GetMethod(logMethodName, BindingFlags.Static | BindingFlags.Public, new Type[]
            {
                typeof(ILambdaLogger),
                typeof(Exception),
                typeof(string),
                typeof(object[])
            });

            method.Invoke(null, new object[] { loggerMock.Object, exception, "format", Array.Empty<object>() });
            loggerMock.Verify(l => l.LogEntry(It.Is<LogLevel>(l => l == logLevel), It.Is<FormattedMessageEntry>(e => e.Exception == exception)), Times.Once);
        }
    }
#endif
}
