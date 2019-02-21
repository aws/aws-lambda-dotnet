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
		private const string SHOULD_APPEAR_EVENT = "EventThatShouldAppear";
		private const string SHOULD_APPEAR_EXCEPTION = "ExceptionThatShouldAppear";
		private static string APPSETTINGS_DIR = Directory.GetCurrentDirectory();
		private static readonly Func<int, EventId> GET_SHOULD_APPEAR_EVENT = (id) => new EventId(451, SHOULD_APPEAR_EVENT + id);
		private static readonly EventId SHOULD_NOT_APPEAR_EVENT = new EventId(333, "EventThatShoulNotdAppear");
		private static readonly Func<int, Exception> GET_SHOULD_APPEAR_EXCEPTION = (id) => new Exception(SHOULD_APPEAR_EXCEPTION + id);
		private static readonly Exception SHOULD_NOT_APPEAR_EXCEPTION = new Exception("ExceptionThatShouldNotAppear");

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
				Assert.DoesNotContain(SHOULD_NOT_APPEAR, text);

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
				foobarLogger.LogTrace(SHOULD_NOT_APPEAR);
				foobarLogger.LogDebug(SHOULD_NOT_APPEAR);
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
				Assert.DoesNotContain(SHOULD_NOT_APPEAR, text);

				// check that all expected strings are in the text
				for (int i = 0; i < count; i++)
				{
					var expected = SHOULD_APPEAR + i;
					Assert.True(text.Contains(expected), $"Expected to find '{expected}' in '{text}'");
				}
			}
		}

		[Fact]
		public void TestOnlyOneWildcardSupported()
		{
			var dict = new Dictionary<string, string>
			{
				{ "Lambda.Logging:LogLevel:*.*", "Information" }
			};

			var configuration = new ConfigurationBuilder()
				.AddInMemoryCollection(dict)
				.Build();

			ArgumentOutOfRangeException exception = null;
			try
			{
				var loggerOptions = new LambdaLoggerOptions(configuration);
			}
			catch (ArgumentOutOfRangeException ex)
			{
				exception = ex;
			}

			// check that there are no unexpected strings in the text
			Assert.NotNull(exception);
			Assert.Contains("only 1 wildcard is supported in a category", exception.Message);
		}

		[Fact]
		public void TestOnlyTerminatingWildcardsSupported()
		{
			var dict = new Dictionary<string, string>
			{
				{ "Lambda.Logging:LogLevel:Foo.*.Bar", "Information" }
			};

			var configuration = new ConfigurationBuilder()
				.AddInMemoryCollection(dict)
				.Build();

			ArgumentException exception = null;
			try
			{
				var loggerOptions = new LambdaLoggerOptions(configuration);
			}
			catch (ArgumentException ex)
			{
				exception = ex;
			}

			// check that there are no unexpected strings in the text
			Assert.NotNull(exception);
			Assert.Contains("wilcards are only supported at the end of a category", exception.Message);
		}

		[Fact]
		public void TestConfigurationReadingForExceptionsEvents()
		{
			// Arrange
			var configuration = new ConfigurationBuilder()
					.AddJsonFile(GetAppSettingsPath("appsettings.exceptions.json"))
					.Build();

			// Act
			var loggerOptions = new LambdaLoggerOptions(configuration);

			// Assert
			Assert.False(loggerOptions.IncludeCategory);
			Assert.False(loggerOptions.IncludeLogLevel);
			Assert.False(loggerOptions.IncludeNewline);
			Assert.True(loggerOptions.IncludeEventId);
			Assert.True(loggerOptions.IncludeException);
		}

		[Fact]
		public void TestLoggingExceptionsAndEvents()
		{
			using (var writer = new StringWriter())
			{
				ConnectLoggingActionToLogger(message => writer.Write(message));

				var configuration = new ConfigurationBuilder()
					.AddJsonFile(GetAppSettingsPath("appsettings.json"))
					.Build();

				var loggerOptions = new LambdaLoggerOptions(configuration);
				var loggerfactory = new TestLoggerFactory()
					.AddLambdaLogger(loggerOptions);

				int countMessage = 0;
				int countEvent = 0;
				int countException = 0;

				var defaultLogger = loggerfactory.CreateLogger("Default");
				defaultLogger.LogTrace(SHOULD_NOT_APPEAR_EVENT, SHOULD_NOT_APPEAR_EXCEPTION, SHOULD_NOT_APPEAR);
				defaultLogger.LogDebug(SHOULD_NOT_APPEAR_EVENT, SHOULD_APPEAR + (countMessage++));
				defaultLogger.LogCritical(SHOULD_NOT_APPEAR_EVENT, SHOULD_APPEAR + (countMessage++));

				defaultLogger = loggerfactory.CreateLogger(null);
				defaultLogger.LogTrace(SHOULD_NOT_APPEAR_EVENT, SHOULD_NOT_APPEAR);
				defaultLogger.LogDebug(SHOULD_NOT_APPEAR_EVENT, SHOULD_APPEAR + (countMessage++));
				defaultLogger.LogCritical(SHOULD_NOT_APPEAR_EVENT, SHOULD_APPEAR + (countMessage++));

				// change settings
				loggerOptions.IncludeCategory = true;
				loggerOptions.IncludeLogLevel = true;
				loggerOptions.IncludeNewline = true;
				loggerOptions.IncludeException = true;
				loggerOptions.IncludeEventId = true;

				var msLogger = loggerfactory.CreateLogger("Microsoft");
				msLogger.LogTrace(SHOULD_NOT_APPEAR_EVENT, SHOULD_NOT_APPEAR_EXCEPTION, SHOULD_NOT_APPEAR);
				msLogger.LogInformation(GET_SHOULD_APPEAR_EVENT(countEvent++), GET_SHOULD_APPEAR_EXCEPTION(countException++), SHOULD_APPEAR + (countMessage++));
				msLogger.LogCritical(GET_SHOULD_APPEAR_EVENT(countEvent++), GET_SHOULD_APPEAR_EXCEPTION(countException++), SHOULD_APPEAR + (countMessage++));

				var sdkLogger = loggerfactory.CreateLogger("AWSSDK");
				sdkLogger.LogTrace(GET_SHOULD_APPEAR_EVENT(countEvent++), GET_SHOULD_APPEAR_EXCEPTION(countException++), SHOULD_APPEAR + (countMessage++));
				sdkLogger.LogInformation(GET_SHOULD_APPEAR_EVENT(countEvent++), GET_SHOULD_APPEAR_EXCEPTION(countException++), SHOULD_APPEAR + (countMessage++));
				sdkLogger.LogCritical(GET_SHOULD_APPEAR_EVENT(countEvent++), GET_SHOULD_APPEAR_EXCEPTION(countException++), SHOULD_APPEAR + (countMessage++));

				// get text and verify
				var text = writer.ToString();

				// check that there are no unexpected strings in the text
				Assert.DoesNotContain(SHOULD_NOT_APPEAR, text);
				Assert.DoesNotContain(SHOULD_NOT_APPEAR_EVENT.Id.ToString(), text);
				Assert.DoesNotContain(SHOULD_NOT_APPEAR_EVENT.Name, text);
				Assert.DoesNotContain(SHOULD_NOT_APPEAR_EXCEPTION.Message, text);

				// check that all expected strings are in the text
				for (int i = 0; i < countMessage; i++)
				{
					var expectedMessages = SHOULD_APPEAR + i;
					Assert.True(text.Contains(expectedMessages), $"Expected to find '{expectedMessages}' in '{text}'");
				}
				for (int i = 0; i < countException; i++)
				{
					var expectedMessages = SHOULD_APPEAR_EXCEPTION + i;
					Assert.True(text.Contains(expectedMessages), $"Expected to find '{expectedMessages}' in '{text}'");
				}
				for (int i = 0; i < countEvent; i++)
				{
					var expectedMessages = SHOULD_APPEAR_EVENT + i;
					Assert.True(text.Contains(expectedMessages), $"Expected to find '{expectedMessages}' in '{text}'");
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
			foreach (var substring in substrings)
			{
				total += CountOccurences(text, substring);
			}
			return total;
		}
	}
}
