using Amazon.Lambda.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Amazon.Lambda.Tests
{
#if NET6_0_OR_GREATER
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "xUnit1026:Theory methods should use all of their parameters", Justification = "Reuse test data")]
    public class LambdaLoggerExtensionsTests
    {
        private static readonly IEnumerable<object[]> AllLogMethods = new List<(LogLevel, string)>
        {
            (LogLevel.Trace, nameof(LambdaLoggerExtensions.Trace)),
            (LogLevel.Debug, nameof(LambdaLoggerExtensions.Debug)),
            (LogLevel.Information, nameof(LambdaLoggerExtensions.Info)),
            (LogLevel.Warning, nameof(LambdaLoggerExtensions.Warning)),
            (LogLevel.Error, nameof(LambdaLoggerExtensions.Error)),
            (LogLevel.Critical, nameof(LambdaLoggerExtensions.Critical))
        }.Select(i =>
        {
            var logMethodName = i.Item2;
            var method = typeof(LambdaLoggerExtensions).GetMethod(logMethodName, BindingFlags.Static | BindingFlags.Public, new Type[]
            {
                typeof(ILambdaLogger),
                typeof(string),
                typeof(object[])
            });
            return new object[] { i.Item1, method };
        }).ToList();

        private static readonly IEnumerable<object[]> LogMethodsWithException = new List<(LogLevel, string)>
        {
            (LogLevel.Error, nameof(LambdaLoggerExtensions.Error)),
            (LogLevel.Critical, nameof(LambdaLoggerExtensions.Critical))
        }.Select(i =>
        {
            var logMethodName = i.Item2;
            var method = typeof(LambdaLoggerExtensions).GetMethod(logMethodName, BindingFlags.Static | BindingFlags.Public, new Type[]
            {
                typeof(ILambdaLogger),
                typeof(Exception),
                typeof(string),
                typeof(object[])
            });
            return new object[] { i.Item1, method };
        }).ToList();

        private static readonly IEnumerable<object[]> StateTestData = new List<object[]>
        {
            new object[] { "a literal message", Array.Empty<object>(), Array.Empty<string>(), "a literal message", },
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

        private static readonly IEnumerable<object[]> ParserValidTestData = new List<object[]>
        {
            new object[] { "a0 {message0 with space} a1 {message1}}}", "a0 {0} a1 {1}}}", new List<string> { "message0 with space", "message1", "message2", "message3" } },
            new object[] { "a {message}", "a {0}", new List<string> { "message" } },
            new object[] { "a0 {{abc}}abc {message0} a1 {message1}", "a0 {{abc}}abc {0} a1 {1}", new List<string> { "message0", "message1" } },
            new object[] { "a0 {same} a1 {same}", "a0 {0} a1 {1}", new List<string> { "same", "same" } },
            new object[] { "a0 {message0} a1 {message1:format1}", "a0 {0} a1 {1:format1}", new List<string> { "message0", "message1" } },
            new object[] { "a0 {message0,12:format0} a1 {message1,-1:format1}", "a0 {0,12:format0} a1 {1,-1:format1}", new List<string> { "message0", "message1" } },
            new object[] { "a0 {{{ a1{{{message0}", "a0 {{{0}", new List<string> { " a1{{{message0" } },
        };

        public static IEnumerable<object[]> ParserInvalidTestData = new List<object[]>
        {
            new object[] { "a0 {abc}}abc {message1} a1 {message2}", 3 },
            new object[] { "a } {message}", 1 },
            new object[] { "a {message0} b {message1}", 1 },    // valid format string but insufficient number of parameters
        };

        public static IEnumerable<object[]> ParserTestCases => Multiplex(AllLogMethods, ParserValidTestData);

        public static IEnumerable<object[]> ParserInvalidTestCases => Multiplex(AllLogMethods, ParserInvalidTestData);

        public static IEnumerable<object[]> StateTestCases => Multiplex(AllLogMethods, StateTestData);

        public static IEnumerable<object[]> StateWithExceptionTestCases => Multiplex(LogMethodsWithException, StateTestData);

        [Fact]
        public void Log_NullFormat_WritesNullString()
        {
            var logger = new TestLambdaLogger();
            LambdaLoggerExtensions.Info(logger, null, new object[] { 1, 2, 3 });

            Assert.Equal(LogLevel.Information.ToString(), logger.Level);
            Assert.NotNull(logger.Entry);
            Assert.Null(logger.Entry.ToString());
        }

        /// <summary>
        /// Main purpose is to test the log format parsing behavior.
        /// </summary>
        [Theory]
        [MemberData(nameof(ParserTestCases))]
        public void LogMethod_WritesFormattedString(LogLevel level, MethodInfo method, string logFormat, string expectedStringFormat, List<string> expectedNames)
        {
            var logger = new TestLambdaLogger();
            var logValues = expectedNames.Select((s, i) => $"[{i}]{s}").ToArray();

            method.Invoke(null, new object[] { logger, logFormat, logValues });

            Assert.Equal(level.ToString(), logger.Level);
            // validate the output against string created by string.Format()
            Assert.Equal(string.Format(expectedStringFormat, logValues), logger.Entry?.ToString());
        }

        [Theory]
        [MemberData(nameof(ParserInvalidTestCases))]
        public void Log_InvalidFormatString_ThrowsFormatException(LogLevel _, MethodInfo method, string logFormat, int paramCount)
        {
            var logger = new TestLambdaLogger();
            var logValues = Enumerable.Range(0, paramCount).Select(i => (object)i).ToArray();

            method.Invoke(null, new object[] { logger, logFormat, logValues });

            Assert.Throws<FormatException>(() => logger.Entry.ToString());
        }


        [Theory]
        [MemberData(nameof(StateTestCases))]
        public void Log_NoException_OutputsNameValuePairs(
            LogLevel level,
            MethodInfo method,
            string logFormat,
            object[] parameters,
            string[] expectedParamNames,
            string expectedOutput)
        {
            var logger = new TestLambdaLogger();
            method.Invoke(null, new object[] { logger, logFormat, parameters });

            Assert.Equal(level.ToString(), logger.Level);

            // this will throw cast exception if entry is not expected
            var entry = (IReadOnlyList<KeyValuePair<string, object>>)logger.Entry;

            // validate the list of name-value pairs, including the '{Exception}' entry
            var expectedState = new List<KeyValuePair<string, object>>
            {
                new KeyValuePair<string, object>("{Exception}", null)
            };
            for (var i = 0; i < expectedParamNames.Length; i++)
            {
                expectedState.Add(new KeyValuePair<string, object>(expectedParamNames[i], parameters[i]));
            }

            Assert.Equal(expectedState.Count, entry.Count);
            Assert.All(entry, p => Assert.Contains(expectedState, x => x.Key == p.Key && x.Value == p.Value));
            Assert.Equal(expectedOutput, entry.ToString());
        }


        [Theory]
        [MemberData(nameof(StateWithExceptionTestCases))]
        public void Log_WithException_OutputsNameValuePairsAndException(
            LogLevel level,
            MethodInfo method,
            string logFormat,
            object[] parameters,
            string[] expectedParamNames,
            string expectedOutput)
        {
            var exception = new Exception("test");
            var logger = new TestLambdaLogger();
            method.Invoke(null, new object[] { logger, exception, logFormat, parameters });

            Assert.Equal(level.ToString(), logger.Level);

            // this will throw cast exception if entry is not expected
            var entry = (IReadOnlyList<KeyValuePair<string, object>>)logger.Entry;

            // validate the list of name-value pairs, including the '{Exception}' entry
            var expectedState = new List<KeyValuePair<string, object>>
            {
                new KeyValuePair<string, object>("{Exception}", exception)
            };
            for (var i = 0; i < expectedParamNames.Length; i++)
            {
                expectedState.Add(new KeyValuePair<string, object>(expectedParamNames[i], parameters[i]));
            }

            Assert.Equal(expectedState.Count, entry.Count);
            Assert.All(entry, p => Assert.Contains(expectedState, x => x.Key == p.Key && x.Value == p.Value));
            Assert.Equal(expectedOutput, entry.ToString());
        }

        /// <summary>
        /// Multiplex the test vector so we can test against all overloads.
        /// </summary>
        private static IEnumerable<object[]> Multiplex(IEnumerable<object[]> data1, IEnumerable<object[]> data2)
        {
            var result = new List<object[]>();
            foreach (var d1 in data1)
            {
                foreach (var d2 in data2)
                {
                    var data = new List<object>();
                    data.AddRange(d1);
                    data.AddRange(d2);
                    result.Add(data.ToArray());
                }
            }

            return result;
        }
    }
#endif
}
