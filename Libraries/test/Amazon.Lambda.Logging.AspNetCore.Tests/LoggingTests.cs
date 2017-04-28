using Amazon.Lambda.Logging.AspNetCore.Tests;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Amazon.Lambda.Tests
{

    public class LoggingTests
    {
        private const string SHOULD_APPEAR = "TextThatShouldAppear";
        private const string SHOULD_NOT_APPEAR = "TextThatShouldNotAppear";
        private static string APPSETTINGS_DIR = Directory.GetCurrentDirectory();

        [Fact]
        public void TestConfiguration()
        {
            using (var writer = new StringWriter())
            {
                ConnectLoggingActionToLogger(message => writer.Write(message));

                var configuration = new ConfigurationBuilder()
                    .AddJsonFile(GetAppSettingsPath("appsettings.json"))
                    .Build();

                var loggerOptions = new LambdaLoggerOptions(configuration);
                Assert.False(loggerOptions.IncludeCategory);
                Assert.False(loggerOptions.IncludeLogLevel);
                Assert.False(loggerOptions.IncludeNewline);

                var loggerfactory = new TestLoggerFactory()
                    .AddLambdaLogger(loggerOptions);

                int count = 0;

                var defaultLogger = loggerfactory.CreateLogger("Default");
                defaultLogger.LogTrace(SHOULD_NOT_APPEAR);
                defaultLogger.LogDebug(SHOULD_APPEAR + (count++));
                defaultLogger.LogCritical(SHOULD_APPEAR + (count++));

                defaultLogger = loggerfactory.CreateLogger(null);
                defaultLogger.LogTrace(SHOULD_NOT_APPEAR);
                defaultLogger.LogDebug(SHOULD_APPEAR + (count++));
                defaultLogger.LogCritical(SHOULD_APPEAR + (count++));

                // change settings
                int countAtChange = count;
                loggerOptions.IncludeCategory = true;
                loggerOptions.IncludeLogLevel = true;
                loggerOptions.IncludeNewline = true;

                var msLogger = loggerfactory.CreateLogger("Microsoft");
                msLogger.LogTrace(SHOULD_NOT_APPEAR);
                msLogger.LogInformation(SHOULD_APPEAR + (count++));
                msLogger.LogCritical(SHOULD_APPEAR + (count++));

                var sdkLogger = loggerfactory.CreateLogger("AWSSDK");
                sdkLogger.LogTrace(SHOULD_APPEAR + (count++));
                sdkLogger.LogInformation(SHOULD_APPEAR + (count++));
                sdkLogger.LogCritical(SHOULD_APPEAR + (count++));

                // get text and verify
                var text = writer.ToString();

                // check that there are no unexpected strings in the text
                Assert.False(text.Contains(SHOULD_NOT_APPEAR));

                // check that all expected strings are in the text
                for (int i = 0; i < count; i++)
                {
                    var expected = SHOULD_APPEAR + i;
                    Assert.True(text.Contains(expected), $"Expected to find '{expected}' in '{text}'");
                }

                // check extras that were added mid-way
                int numberOfExtraBits = count - countAtChange;
                
                // count levels
                var logLevelStrings = Enum.GetNames(typeof(LogLevel)).Select(ll => $"[{ll}").ToList();
                Assert.Equal(numberOfExtraBits, CountMultipleOccurences(text, logLevelStrings));

                // count categories
                var categoryStrings = new string[] { "Microsoft", "AWSSDK" };
                Assert.Equal(numberOfExtraBits, CountMultipleOccurences(text, categoryStrings));

                // count newlines
                Assert.Equal(numberOfExtraBits, CountOccurences(text, Environment.NewLine));
            }
        }

        [Fact]
        public void TestWilcardConfiguration()
        {
            using (var writer = new StringWriter())
            {
                ConnectLoggingActionToLogger(message => writer.Write(message));

                var configuration = new ConfigurationBuilder()
                    .AddJsonFile(GetAppSettingsPath("appsettings.wildcard.json"))
                    .Build();

                var loggerOptions = new LambdaLoggerOptions(configuration);
                Assert.False(loggerOptions.IncludeCategory);
                Assert.False(loggerOptions.IncludeLogLevel);
                Assert.False(loggerOptions.IncludeNewline);

                var loggerfactory = new TestLoggerFactory()
                    .AddLambdaLogger(loggerOptions);

                int count = 0;

                // Should match:
                //   "Foo.*": "Information"
                var foobarLogger = loggerfactory.CreateLogger("Foo.Bar");
                foobarLogger.LogTrace(SHOULD_NOT_APPEAR );
                foobarLogger.LogDebug(SHOULD_NOT_APPEAR );
                foobarLogger.LogInformation(SHOULD_APPEAR + (count++));
                foobarLogger.LogWarning(SHOULD_APPEAR + (count++));
                foobarLogger.LogError(SHOULD_APPEAR + (count++));
                foobarLogger.LogCritical(SHOULD_APPEAR + (count++));

                // Should match:
                //   "Foo.Bar.Baz": "Critical"
                var foobarbazLogger = loggerfactory.CreateLogger("Foo.Bar.Baz");
                foobarbazLogger.LogTrace(SHOULD_NOT_APPEAR);
                foobarbazLogger.LogDebug(SHOULD_NOT_APPEAR);
                foobarbazLogger.LogInformation(SHOULD_NOT_APPEAR);
                foobarbazLogger.LogWarning(SHOULD_NOT_APPEAR);
                foobarbazLogger.LogError(SHOULD_NOT_APPEAR);
                foobarbazLogger.LogCritical(SHOULD_APPEAR + (count++));

                // Should match:
                //   "Foo.Bar.*": "Warning"
                var foobarbuzzLogger = loggerfactory.CreateLogger("Foo.Bar.Buzz");
                foobarbuzzLogger.LogTrace(SHOULD_NOT_APPEAR);
                foobarbuzzLogger.LogDebug(SHOULD_NOT_APPEAR);
                foobarbuzzLogger.LogInformation(SHOULD_NOT_APPEAR);
                foobarbuzzLogger.LogWarning(SHOULD_APPEAR + (count++));
                foobarbuzzLogger.LogError(SHOULD_APPEAR + (count++));
                foobarbuzzLogger.LogCritical(SHOULD_APPEAR + (count++));


                // Should match:
                //   "*": "Error"
                var somethingLogger = loggerfactory.CreateLogger("something");
                somethingLogger.LogTrace(SHOULD_NOT_APPEAR);
                somethingLogger.LogDebug(SHOULD_NOT_APPEAR);
                somethingLogger.LogInformation(SHOULD_NOT_APPEAR);
                somethingLogger.LogWarning(SHOULD_NOT_APPEAR);
                somethingLogger.LogError(SHOULD_APPEAR + (count++));
                somethingLogger.LogCritical(SHOULD_APPEAR + (count++));

                // get text and verify
                var text = writer.ToString();

                // check that there are no unexpected strings in the text
                Assert.False(text.Contains(SHOULD_NOT_APPEAR));

                // check that all expected strings are in the text
                for (int i = 0; i < count; i++)
                {
                    var expected = SHOULD_APPEAR + i;
                    Assert.True(text.Contains(expected), $"Expected to find '{expected}' in '{text}'");
                }
            }
        }

        private static string GetAppSettingsPath(string fileName)
        {
            return Path.Combine(APPSETTINGS_DIR, fileName);
        }
        private static void ConnectLoggingActionToLogger(Action<string> loggingAction)
        {
            var lambdaLoggerType = typeof(Amazon.Lambda.Core.LambdaLogger);
            Assert.NotNull(lambdaLoggerType);
            var loggingActionField = lambdaLoggerType
                .GetTypeInfo()
                .GetField("_loggingAction", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(loggingActionField);

            loggingActionField.SetValue(null, loggingAction);
        }
        private static int CountOccurences(string text, string substring)
        {
            int occurences = 0;
            int index = 0;
            do
            {
                index = text.IndexOf(substring, index, StringComparison.Ordinal);
                if (index >= 0)
                {
                    occurences++;
                    index += substring.Length;
                }
            } while (index >= 0);
            return occurences;
        }
        private static int CountMultipleOccurences(string text, IEnumerable<string> substrings)
        {
            int total = 0;
            foreach(var substring in substrings)
            {
                total += CountOccurences(text, substring);
            }
            return total;
        }
    }
}
