using Amazon.Lambda.Logging.AspNetCore.Tests;
using Amazon.Lambda.RuntimeSupport.Helpers;
using Amazon.Lambda.RuntimeSupport.Helpers.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Amazon.Lambda.Tests
{

	public class LoggingTests
	{
		private const string SHOULD_APPEAR = "TextThatShouldAppear";
		private const string SHOULD_NOT_APPEAR = "TextThatShouldNotAppear";
		private const string SHOULD_APPEAR_EVENT = "EventThatShouldAppear";
		private const string SHOULD_APPEAR_EXCEPTION = "ExceptionThatShouldAppear";
		private static readonly string APPSETTINGS_DIR = Directory.GetCurrentDirectory();
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
				sdkLogger.LogTrace(SHOULD_NOT_APPEAR);
				sdkLogger.LogInformation(SHOULD_APPEAR + (count++));
				sdkLogger.LogCritical(SHOULD_APPEAR + (count++));

				// get text and verify
				var text = writer.ToString();

				// check that there are no unexpected strings in the text
				Assert.DoesNotContain(SHOULD_NOT_APPEAR, text);

                // Confirm log level was written to log
                Assert.Contains("Critical:", text);

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

				var loggerFactory = new TestLoggerFactory()
					.AddLambdaLogger(loggerOptions);

				int count = 0;

				// Should match:
				//   "Foo.*": "Information"
				var foobarLogger = loggerFactory.CreateLogger("Foo.Bar");
				foobarLogger.LogTrace(SHOULD_NOT_APPEAR);
				foobarLogger.LogDebug(SHOULD_NOT_APPEAR);
				foobarLogger.LogInformation(SHOULD_APPEAR + (count++));
				foobarLogger.LogWarning(SHOULD_APPEAR + (count++));
				foobarLogger.LogError(SHOULD_APPEAR + (count++));
				foobarLogger.LogCritical(SHOULD_APPEAR + (count++));

				// Should match:
				//   "Foo.Bar.Baz": "Critical"
				var foobarbazLogger = loggerFactory.CreateLogger("Foo.Bar.Baz");
				foobarbazLogger.LogTrace(SHOULD_NOT_APPEAR);
				foobarbazLogger.LogDebug(SHOULD_NOT_APPEAR);
				foobarbazLogger.LogInformation(SHOULD_NOT_APPEAR);
				foobarbazLogger.LogWarning(SHOULD_NOT_APPEAR);
				foobarbazLogger.LogError(SHOULD_NOT_APPEAR);
				foobarbazLogger.LogCritical(SHOULD_APPEAR + (count++));

				// Should match:
				//   "Foo.Bar.*": "Warning"
				var foobarbuzzLogger = loggerFactory.CreateLogger("Foo.Bar.Buzz");
				foobarbuzzLogger.LogTrace(SHOULD_NOT_APPEAR);
				foobarbuzzLogger.LogDebug(SHOULD_NOT_APPEAR);
				foobarbuzzLogger.LogInformation(SHOULD_NOT_APPEAR);
				foobarbuzzLogger.LogWarning(SHOULD_APPEAR + (count++));
				foobarbuzzLogger.LogError(SHOULD_APPEAR + (count++));
				foobarbuzzLogger.LogCritical(SHOULD_APPEAR + (count++));


				// Should match:
				//   "*": "Error"
				var somethingLogger = loggerFactory.CreateLogger("something");
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
			Assert.False(loggerOptions.IncludeScopes);
		}

		[Fact]
		public void TestConfigurationReadingForScopes()
		{
			// Arrange
			var configuration = new ConfigurationBuilder()
					.AddJsonFile(GetAppSettingsPath("appsettings.scopes.json"))
					.Build();

			// Act
			var loggerOptions = new LambdaLoggerOptions(configuration);

			// Assert
			Assert.False(loggerOptions.IncludeCategory);
			Assert.False(loggerOptions.IncludeLogLevel);
			Assert.False(loggerOptions.IncludeNewline);
			Assert.True(loggerOptions.IncludeEventId);
			Assert.True(loggerOptions.IncludeException);
			Assert.True(loggerOptions.IncludeScopes);
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
				var loggerFactory = new TestLoggerFactory()
					.AddLambdaLogger(loggerOptions);

				int countMessage = 0;
				int countEvent = 0;
				int countException = 0;

				var defaultLogger = loggerFactory.CreateLogger("Default");
				defaultLogger.LogTrace(SHOULD_NOT_APPEAR_EVENT, SHOULD_NOT_APPEAR_EXCEPTION, SHOULD_NOT_APPEAR);
				defaultLogger.LogDebug(SHOULD_NOT_APPEAR_EVENT, SHOULD_APPEAR + (countMessage++));
				defaultLogger.LogCritical(SHOULD_NOT_APPEAR_EVENT, SHOULD_APPEAR + (countMessage++));

				defaultLogger = loggerFactory.CreateLogger(null);
				defaultLogger.LogTrace(SHOULD_NOT_APPEAR_EVENT, SHOULD_NOT_APPEAR);
				defaultLogger.LogDebug(SHOULD_NOT_APPEAR_EVENT, SHOULD_APPEAR + (countMessage++));
				defaultLogger.LogCritical(SHOULD_NOT_APPEAR_EVENT, SHOULD_APPEAR + (countMessage++));

				// change settings
				loggerOptions.IncludeCategory = true;
				loggerOptions.IncludeLogLevel = true;
				loggerOptions.IncludeNewline = true;
				loggerOptions.IncludeException = true;
				loggerOptions.IncludeEventId = true;

				var msLogger = loggerFactory.CreateLogger("Microsoft");
				msLogger.LogTrace(SHOULD_NOT_APPEAR_EVENT, SHOULD_NOT_APPEAR_EXCEPTION, SHOULD_NOT_APPEAR);
				msLogger.LogInformation(GET_SHOULD_APPEAR_EVENT(countEvent++), GET_SHOULD_APPEAR_EXCEPTION(countException++), SHOULD_APPEAR + (countMessage++));
				msLogger.LogCritical(GET_SHOULD_APPEAR_EVENT(countEvent++), GET_SHOULD_APPEAR_EXCEPTION(countException++), SHOULD_APPEAR + (countMessage++));

				var sdkLogger = loggerFactory.CreateLogger("AWSSDK");
				sdkLogger.LogTrace(SHOULD_NOT_APPEAR_EVENT, SHOULD_NOT_APPEAR_EXCEPTION, SHOULD_NOT_APPEAR);
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

        [Fact]
        public void TestLoggingScopesEvents()
        {
            // Arrange
            using (var writer = new StringWriter())
            {
                ConnectLoggingActionToLogger(message => writer.Write(message));

                var loggerOptions = new LambdaLoggerOptions{ IncludeScopes = true };
                var loggerFactory = new TestLoggerFactory()
                    .AddLambdaLogger(loggerOptions);

                var defaultLogger = loggerFactory.CreateLogger("Default");

                // Act
                using(defaultLogger.BeginScope("First {0}", "scope123"))
                {
                    defaultLogger.LogInformation("Hello");

                    using(defaultLogger.BeginScope("Second {0}", "scope456"))
                    {
                        defaultLogger.LogError("In 2nd scope");
                        defaultLogger.LogInformation("that's enough");
                    }
                }

                // Assert
                // get text and verify
                var text = writer.ToString();
                Assert.Contains("[Information] First scope123 => Default: Hello ", text);
                Assert.Contains("[Error] First scope123 Second scope456 => Default: In 2nd scope ", text);
                Assert.Contains("[Information] First scope123 Second scope456 => Default: that's enough ", text);
            }
        }

		[Fact]
		public void TestLoggingScopesEvents_When_ScopesDisabled()
		{
			// Arrange
			using (var writer = new StringWriter())
			{
				ConnectLoggingActionToLogger(message => writer.Write(message));

				var loggerOptions = new LambdaLoggerOptions { IncludeScopes = false };
				var loggerFactory = new TestLoggerFactory()
					.AddLambdaLogger(loggerOptions);

				var defaultLogger = loggerFactory.CreateLogger("Default");

				// Act
				using (defaultLogger.BeginScope("First {0}", "scope123"))
				{
					defaultLogger.LogInformation("Hello");

					using (defaultLogger.BeginScope("Second {0}", "scope456"))
					{
						defaultLogger.LogError("In 2nd scope");
						defaultLogger.LogInformation("that's enough");
					}
				}

				// Assert
				// get text and verify
				var text = writer.ToString();
				Assert.Contains("[Information] Default: Hello ", text);
				Assert.Contains("[Error] Default: In 2nd scope ", text);
				Assert.Contains("[Information] Default: that's enough ", text);
			}
		}

        [Fact]
        public void TestLoggingWithTypeCategories()
        {
            using (var writer = new StringWriter())
            {
                ConnectLoggingActionToLogger(message => writer.Write(message));

                // arrange
                var configuration = new ConfigurationBuilder()
                    .AddJsonFile(GetAppSettingsPath("appsettings.nsprefix.json"))
                    .Build();

                var loggerOptions = new LambdaLoggerOptions(configuration);
                var loggerFactory = new TestLoggerFactory()
                    .AddLambdaLogger(loggerOptions);

                // act
                var httpClientLogger = loggerFactory.CreateLogger<System.Net.HttpListener>();
                var arrayLogger = loggerFactory.CreateLogger<System.Array>();

                httpClientLogger.LogTrace(SHOULD_NOT_APPEAR);
                httpClientLogger.LogDebug(SHOULD_APPEAR);
                httpClientLogger.LogInformation(SHOULD_APPEAR);
                httpClientLogger.LogWarning(SHOULD_APPEAR);
                httpClientLogger.LogError(SHOULD_APPEAR);
                httpClientLogger.LogCritical(SHOULD_APPEAR);

                arrayLogger.LogTrace(SHOULD_NOT_APPEAR);
                arrayLogger.LogDebug(SHOULD_NOT_APPEAR);
                arrayLogger.LogInformation(SHOULD_NOT_APPEAR);
                arrayLogger.LogWarning(SHOULD_APPEAR);
                arrayLogger.LogError(SHOULD_APPEAR);
                arrayLogger.LogCritical(SHOULD_APPEAR);

                // assert
                var text = writer.ToString();
                Assert.DoesNotContain(SHOULD_NOT_APPEAR, text);
            }
        }

        [Fact]
        public void TestDefaultLogLevel()
        {
            using (var writer = new StringWriter())
            {
                ConnectLoggingActionToLogger(message => writer.Write(message));

                var configuration = new ConfigurationBuilder()
                    .AddJsonFile(GetAppSettingsPath("appsettings.json"))
                    .Build();

                var loggerOptions = new LambdaLoggerOptions(configuration);
                var loggerFactory = new TestLoggerFactory()
                    .AddLambdaLogger(loggerOptions);

                // act
                // creating named logger, `Default` category is set to "Debug"
                // (Default category has special treatment - it's not actually stored, named logger just falls to default)
                var defaultLogger = loggerFactory.CreateLogger("Default");
                defaultLogger.LogTrace(SHOULD_NOT_APPEAR);
                defaultLogger.LogDebug(SHOULD_APPEAR);
                defaultLogger.LogInformation(SHOULD_APPEAR);

                // `Dummy` category is not specified, we should use `Default` category instead
                var dummyLogger = loggerFactory.CreateLogger("Dummy");
                dummyLogger.LogTrace(SHOULD_NOT_APPEAR);
                dummyLogger.LogDebug(SHOULD_APPEAR);
                dummyLogger.LogInformation(SHOULD_APPEAR);

                // `Microsoft` category is specified, log accordingly
                var msLogger = loggerFactory.CreateLogger("Microsoft");
                msLogger.LogTrace(SHOULD_NOT_APPEAR);
                msLogger.LogDebug(SHOULD_NOT_APPEAR);
                msLogger.LogInformation(SHOULD_APPEAR);

                // assert
                var text = writer.ToString();
                Assert.DoesNotContain(SHOULD_NOT_APPEAR, text);
            }
        }

        [Fact]
        public void TestDefaultLogLevelIfNotConfigured()
        {
            // arrange
            using (var writer = new StringWriter())
            {
                ConnectLoggingActionToLogger(message => writer.Write(message));

                var configuration = new ConfigurationBuilder()
                    .AddJsonFile(GetAppSettingsPath("appsettings.without_default.json"))
                    .Build();

                var loggerOptions = new LambdaLoggerOptions(configuration);
                var loggerFactory = new TestLoggerFactory()
                    .AddLambdaLogger(loggerOptions);

                // act
                // `Dummy` category is not specified, we should stick with default: min level = INFO
                var dummyLogger = loggerFactory.CreateLogger("Dummy");
                dummyLogger.LogTrace(SHOULD_NOT_APPEAR);
                dummyLogger.LogDebug(SHOULD_NOT_APPEAR);
                dummyLogger.LogInformation(SHOULD_APPEAR);

                // `Microsoft` category is specified, log accordingly
                var msLogger = loggerFactory.CreateLogger("Microsoft");
                msLogger.LogTrace(SHOULD_NOT_APPEAR);
                msLogger.LogDebug(SHOULD_NOT_APPEAR);
                msLogger.LogInformation(SHOULD_NOT_APPEAR);

                // assert
                var text = writer.ToString();
                Assert.DoesNotContain(SHOULD_NOT_APPEAR, text);
            }
        }

        /// <summary>
        /// For this test we just need to make sure the _loggingWithLevelAndExceptionAction is called with parameters and exception.
        /// We can't confirm the JSON formatting is done because RuntimeSupport is not involved. That is okay because we have
        /// other tests that confirm RuntimeSupport formats the log as JSON. We jsut need to confirm the right callback is called
        /// with the parameters from the log message.
        /// </summary>
        [Fact]
        public void TestJSONParameterLogging()
        {
            Environment.SetEnvironmentVariable("AWS_LAMBDA_LOG_FORMAT", "JSON");
            try
            {
                using (var writer = new StringWriter())
                {
                    ConnectLoggingActionToLogger(message => writer.Write(message));

                    var configuration = new ConfigurationBuilder()
                        .AddJsonFile(GetAppSettingsPath("appsettings.json"))
                        .Build();

                    var loggerOptions = new LambdaLoggerOptions(configuration);
                    var loggerFactory = new TestLoggerFactory()
                        .AddLambdaLogger(loggerOptions);

                    var logger = loggerFactory.CreateLogger("JSONLogging");

                    logger.LogError(new Exception("Too Cheap"), "User {name} fail to by {product} for {price}", "Gilmour", "Guitar", 55.55);

                    var text = writer.ToString();
                    Assert.Contains("parameter count: 3", text);
                    Assert.Contains("Too Cheap", text);
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable("AWS_LAMBDA_LOG_FORMAT", null);
            }

        }

        [Fact]
        public void TestJSONParameterLoggingIncludesCategoryWhenEnabled()
        {
            Environment.SetEnvironmentVariable("AWS_LAMBDA_LOG_FORMAT", "JSON");
            try
            {
                using (var writer = new StringWriter())
                {
                    ConnectLoggingActionToLogger(message => writer.Write(message));

                    var configuration = new ConfigurationBuilder()
                        .AddJsonFile(GetAppSettingsPath("appsettings.json"))
                        .Build();

                    var loggerOptions = new LambdaLoggerOptions(configuration);
                    loggerOptions.IncludeCategory = true;
                    var loggerFactory = new TestLoggerFactory()
                        .AddLambdaLogger(loggerOptions);

                    var logger = loggerFactory.CreateLogger("JSONLogging");

                    logger.LogError(new Exception("Too Cheap"), "User {name} fail to by {product} for {price}", "Gilmour", "Guitar", 55.55);

                    var text = writer.ToString();
                    // Category is prepended as literal text, not a template placeholder,
                    // so the caller's own {name}/{product}/{price} arguments are untouched.
                    Assert.Contains("[JSONLogging]", text);
                    Assert.DoesNotContain("{Category}", text);
                    Assert.Contains("parameter count: 3", text);
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable("AWS_LAMBDA_LOG_FORMAT", null);
            }
        }

        [Fact]
        public void TestJSONParameterLoggingOmitsCategoryWhenDisabled()
        {
            Environment.SetEnvironmentVariable("AWS_LAMBDA_LOG_FORMAT", "JSON");
            try
            {
                using (var writer = new StringWriter())
                {
                    ConnectLoggingActionToLogger(message => writer.Write(message));

                    var configuration = new ConfigurationBuilder()
                        .AddJsonFile(GetAppSettingsPath("appsettings.json"))
                        .Build();

                    var loggerOptions = new LambdaLoggerOptions(configuration);
                    loggerOptions.IncludeCategory = false;
                    var loggerFactory = new TestLoggerFactory()
                        .AddLambdaLogger(loggerOptions);

                    var logger = loggerFactory.CreateLogger("JSONLogging");

                    logger.LogError(new Exception("Too Cheap"), "User {name} fail to by {product} for {price}", "Gilmour", "Guitar", 55.55);

                    var text = writer.ToString();
                    Assert.DoesNotContain("[JSONLogging]", text);
                    Assert.DoesNotContain("{Category}", text);
                    Assert.Contains("parameter count: 3", text);
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable("AWS_LAMBDA_LOG_FORMAT", null);
            }
        }

        [Fact]
        public void TestJSONParameterLoggingWithCategoryLeavesPositionalTemplateIntact()
        {
            // Prepending category as a "{Category}" template placeholder (the earlier approach)
            // would mix a named property into an otherwise all-numeric template, which the JSON
            // formatter's positional-argument detection relies on being all-numeric. Prepending
            // it as literal text instead means the caller's own "{0}"/"{1}" placeholders, and the
            // argument array they line up with, are passed through completely untouched.
            Environment.SetEnvironmentVariable("AWS_LAMBDA_LOG_FORMAT", "JSON");
            try
            {
                using (var writer = new StringWriter())
                {
                    ConnectLoggingActionToLogger(message => writer.Write(message));

                    var configuration = new ConfigurationBuilder()
                        .AddJsonFile(GetAppSettingsPath("appsettings.json"))
                        .Build();

                    var loggerOptions = new LambdaLoggerOptions(configuration);
                    loggerOptions.IncludeCategory = true;
                    var loggerFactory = new TestLoggerFactory()
                        .AddLambdaLogger(loggerOptions);

                    var logger = loggerFactory.CreateLogger("JSONLogging");

                    logger.LogInformation("{0} scored {1}", "Alice", 42);

                    var text = writer.ToString();
                    Assert.Contains("[JSONLogging]", text);
                    Assert.Contains("{0} scored {1}", text);
                    Assert.DoesNotContain("{Category}", text);
                    Assert.Contains("parameter count: 2", text);
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable("AWS_LAMBDA_LOG_FORMAT", null);
            }
        }

        [Fact]
        public void JsonLoggingWithNoOriginalFormat()
        {
            Environment.SetEnvironmentVariable("AWS_LAMBDA_LOG_FORMAT", "JSON");
            try
            {
                using (var writer = new StringWriter())
                {
                    ConnectLoggingActionToLogger(message => writer.Write(message));

                    var configuration = new ConfigurationBuilder()
                        .AddJsonFile(GetAppSettingsPath("appsettings.json"))
                        .Build();

                    var loggerOptions = new LambdaLoggerOptions(configuration);
                    var loggerFactory = new TestLoggerFactory()
                        .AddLambdaLogger(loggerOptions);

                    var logger = loggerFactory.CreateLogger("JSONLogging");

                    logger.Log(LogLevel.Error, new EventId(1), new Dictionary<string, object>() { { "Param1", "Value1" } }, null, (state, e) =>
                    {
                        var sb = new StringBuilder();
                        foreach(var kvp in state)
                        {
                            sb.AppendFormat("{0}:{1}\n", kvp.Key, kvp.Value);
                        }
                        return sb.ToString();
                    });

                    var text = writer.ToString();
                    Assert.Contains("Param1:Value1", text);
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable("AWS_LAMBDA_LOG_FORMAT", null);
            }
        }

        [Fact]
        public void JsonLogging_SingleStructuredScope_IncludedInParameters()
        {
            Environment.SetEnvironmentVariable("AWS_LAMBDA_LOG_FORMAT", "JSON");
            try
            {
                using (var writer = new StringWriter())
                {
                    ConnectLoggingActionToLogger(message => writer.Write(message));

                    var loggerOptions = new LambdaLoggerOptions { IncludeScopes = true };
                    var loggerFactory = new TestLoggerFactory().AddLambdaLogger(loggerOptions);
                    var logger = loggerFactory.CreateLogger("JsonScopeTest");

                    var scopeProps = new Dictionary<string, object> { { "RequestId", "abc-123" } };
                    using (logger.BeginScope(scopeProps))
                    {
                        logger.LogInformation("User {Name} logged in", "Alice");
                    }

                    var text = writer.ToString();
                    // scope param + 1 message param = 2
                    Assert.Contains("parameter count: 2", text);
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable("AWS_LAMBDA_LOG_FORMAT", null);
            }
        }

        [Fact]
        public void JsonLogging_NestedStructuredScopes_AllIncludedInParameters()
        {
            Environment.SetEnvironmentVariable("AWS_LAMBDA_LOG_FORMAT", "JSON");
            try
            {
                using (var writer = new StringWriter())
                {
                    ConnectLoggingActionToLogger(message => writer.Write(message));

                    var loggerOptions = new LambdaLoggerOptions { IncludeScopes = true };
                    var loggerFactory = new TestLoggerFactory().AddLambdaLogger(loggerOptions);
                    var logger = loggerFactory.CreateLogger("JsonScopeTest");

                    var outerScope = new Dictionary<string, object> { { "TraceId", "trace-1" } };
                    var innerScope = new Dictionary<string, object> { { "UserId", "user-99" } };
                    using (logger.BeginScope(outerScope))
                    {
                        using (logger.BeginScope(innerScope))
                        {
                            logger.LogInformation("Processed {Item}", "order");
                        }
                    }

                    var text = writer.ToString();
                    // outer (1) + inner (1) + message param (1) = 3
                    Assert.Contains("parameter count: 3", text);
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable("AWS_LAMBDA_LOG_FORMAT", null);
            }
        }

        [Fact]
        public void JsonLogging_ScopesDisabled_ScopePropertiesNotIncluded()
        {
            Environment.SetEnvironmentVariable("AWS_LAMBDA_LOG_FORMAT", "JSON");
            try
            {
                using (var writer = new StringWriter())
                {
                    ConnectLoggingActionToLogger(message => writer.Write(message));

                    var loggerOptions = new LambdaLoggerOptions { IncludeScopes = false };
                    var loggerFactory = new TestLoggerFactory().AddLambdaLogger(loggerOptions);
                    var logger = loggerFactory.CreateLogger("JsonScopeTest");

                    var scopeProps = new Dictionary<string, object> { { "RequestId", "abc-123" } };
                    using (logger.BeginScope(scopeProps))
                    {
                        logger.LogInformation("User {Name} logged in", "Alice");
                    }

                    var text = writer.ToString();
                    // only 1 message param, scope excluded
                    Assert.Contains("parameter count: 1", text);
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable("AWS_LAMBDA_LOG_FORMAT", null);
            }
        }

        [Fact]
        public void JsonLogging_NoScopes_MessageTemplatePropertiesPreserved()
        {
            Environment.SetEnvironmentVariable("AWS_LAMBDA_LOG_FORMAT", "JSON");
            try
            {
                using (var writer = new StringWriter())
                {
                    ConnectLoggingActionToLogger(message => writer.Write(message));

                    var loggerOptions = new LambdaLoggerOptions { IncludeScopes = true };
                    var loggerFactory = new TestLoggerFactory().AddLambdaLogger(loggerOptions);
                    var logger = loggerFactory.CreateLogger("JsonScopeTest");

                    logger.LogInformation("Order {OrderId} placed for {Customer}", 42, "Bob");

                    var text = writer.ToString();
                    Assert.Contains("parameter count: 2", text);
                    Assert.Contains("Order {OrderId} placed for {Customer}", text);
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable("AWS_LAMBDA_LOG_FORMAT", null);
            }
        }

        [Fact]
        public void JsonLogging_ScopeWithNullValue_DoesNotCrash()
        {
            Environment.SetEnvironmentVariable("AWS_LAMBDA_LOG_FORMAT", "JSON");
            try
            {
                using (var writer = new StringWriter())
                {
                    ConnectLoggingActionToLogger(message => writer.Write(message));

                    var loggerOptions = new LambdaLoggerOptions { IncludeScopes = true };
                    var loggerFactory = new TestLoggerFactory().AddLambdaLogger(loggerOptions);
                    var logger = loggerFactory.CreateLogger("JsonScopeTest");

                    var scopeProps = new Dictionary<string, object> { { "NullProp", null } };
                    using (logger.BeginScope(scopeProps))
                    {
                        logger.LogInformation("Null scope value test");
                    }

                    var text = writer.ToString();
                    // 1 scope param (null) + 0 message params = 1
                    Assert.Contains("parameter count: 1", text);
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable("AWS_LAMBDA_LOG_FORMAT", null);
            }
        }

        [Fact]
        public void JsonLogging_NonStructuredScope_DoesNotCrash()
        {
            Environment.SetEnvironmentVariable("AWS_LAMBDA_LOG_FORMAT", "JSON");
            try
            {
                using (var writer = new StringWriter())
                {
                    ConnectLoggingActionToLogger(message => writer.Write(message));

                    var loggerOptions = new LambdaLoggerOptions { IncludeScopes = true };
                    var loggerFactory = new TestLoggerFactory().AddLambdaLogger(loggerOptions);
                    var logger = loggerFactory.CreateLogger("JsonScopeTest");

                    using (logger.BeginScope("plain string scope"))
                    {
                        logger.LogInformation("Message {Param}", "value");
                    }

                    var text = writer.ToString();
                    // non-structured scope ignored; only 1 message param
                    Assert.Contains("parameter count: 1", text);
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable("AWS_LAMBDA_LOG_FORMAT", null);
            }
        }

        /// <summary>
        /// Hooks the actual Lambda RuntimeSupport JSON log formatter (Amazon.Lambda.RuntimeSupport.Helpers.Logging.JsonLogMessageFormatter)
        /// up to Amazon.Lambda.Core.LambdaLogger, the same way RuntimeSupport does at runtime, so tests can assert on real, parsed JSON
        /// output rather than a fake sink. Returns the list that will be populated with the raw JSON produced for each log call.
        /// </summary>
        private static List<string> ConnectJsonFormatterToLogger()
        {
            var jsonMessages = new List<string>();
            var formatter = new JsonLogMessageFormatter();

            void Capture(string level, Exception exception, string message, object[] args)
            {
                var state = new MessageState
                {
                    TimeStamp = DateTime.UtcNow,
                    Level = Enum.TryParse<LogLevelLoggerWriter.LogLevel>(level, true, out var parsedLevel)
                        ? parsedLevel
                        : (LogLevelLoggerWriter.LogLevel?)null,
                    MessageTemplate = message,
                    MessageArguments = args ?? Array.Empty<object>(),
                    Exception = exception,
                };

                jsonMessages.Add(formatter.FormatMessage(state));
            }

            Action<string, Exception, string, object[]> loggingWithExceptionLevelAction =
                (level, exception, message, args) => Capture(level, exception, message, args);

            var lambdaLoggerType = typeof(Amazon.Lambda.Core.LambdaLogger);
            var loggingWithExceptionLevelActionField = lambdaLoggerType
                .GetTypeInfo()
                .GetField("_loggingWithLevelAndExceptionAction", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(loggingWithExceptionLevelActionField);

            loggingWithExceptionLevelActionField.SetValue(null, loggingWithExceptionLevelAction);

            return jsonMessages;
        }

        /// <summary>
        /// Runs <paramref name="testBody"/> with AWS_LAMBDA_LOG_FORMAT=JSON and the real RuntimeSupport JSON formatter
        /// connected to Amazon.Lambda.Core.LambdaLogger, restoring both the environment variable and the original
        /// static logging delegate afterwards so this test does not leak state into other tests.
        /// </summary>
        private static void RunWithJsonFormatterCapture(Action<List<string>> testBody)
        {
            var lambdaLoggerType = typeof(Amazon.Lambda.Core.LambdaLogger);
            var loggingWithExceptionLevelActionField = lambdaLoggerType
                .GetTypeInfo()
                .GetField("_loggingWithLevelAndExceptionAction", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(loggingWithExceptionLevelActionField);
            var originalAction = loggingWithExceptionLevelActionField.GetValue(null);

            Environment.SetEnvironmentVariable("AWS_LAMBDA_LOG_FORMAT", "JSON");
            try
            {
                var jsonMessages = ConnectJsonFormatterToLogger();
                testBody(jsonMessages);
            }
            finally
            {
                Environment.SetEnvironmentVariable("AWS_LAMBDA_LOG_FORMAT", null);
                loggingWithExceptionLevelActionField.SetValue(null, originalAction);
            }
        }

        [Fact]
        public void EndToEndJson_MessageProperties_WrittenWithCorrectNamesAndValues()
        {
            RunWithJsonFormatterCapture(jsonMessages =>
            {
                var loggerFactory = new TestLoggerFactory().AddLambdaLogger(new LambdaLoggerOptions());
                var logger = loggerFactory.CreateLogger("EndToEndJson");

                logger.LogInformation("User {Name} bought {Count} items for {Price}", "Alice", 3, 19.99);

                var json = JsonDocument.Parse(Assert.Single(jsonMessages)).RootElement;
                Assert.Equal("Alice", json.GetProperty("Name").GetString());
                Assert.Equal(3, json.GetProperty("Count").GetInt32());
                Assert.Equal(19.99, json.GetProperty("Price").GetDouble());
                Assert.Equal("Information", json.GetProperty("level").GetString());
            });
        }

        [Fact]
        public void EndToEndJson_ScopeProperties_AddedAsJsonPropertiesWithCorrectTypes()
        {
            RunWithJsonFormatterCapture(jsonMessages =>
            {
                var loggerFactory = new TestLoggerFactory().AddLambdaLogger(new LambdaLoggerOptions { IncludeScopes = true });
                var logger = loggerFactory.CreateLogger("EndToEndJson");

                var scopeProps = new Dictionary<string, object>
                {
                    { "RequestId", "abc-123" },
                    { "RetryCount", 2 },
                };
                using (logger.BeginScope(scopeProps))
                {
                    logger.LogInformation("User {Name} logged in", "Bob");
                }

                var json = JsonDocument.Parse(Assert.Single(jsonMessages)).RootElement;
                Assert.Equal("Bob", json.GetProperty("Name").GetString());
                Assert.Equal("abc-123", json.GetProperty("RequestId").GetString());
                Assert.Equal(2, json.GetProperty("RetryCount").GetInt32());
            });
        }

        [Fact]
        public void EndToEndJson_NestedScopesDuplicateKey_InnerValueWins()
        {
            RunWithJsonFormatterCapture(jsonMessages =>
            {
                var loggerFactory = new TestLoggerFactory().AddLambdaLogger(new LambdaLoggerOptions { IncludeScopes = true });
                var logger = loggerFactory.CreateLogger("EndToEndJson");

                var outerScope = new Dictionary<string, object> { { "UserId", "outer-user" } };
                var innerScope = new Dictionary<string, object> { { "UserId", "inner-user" } };
                using (logger.BeginScope(outerScope))
                using (logger.BeginScope(innerScope))
                {
                    logger.LogInformation("Processing");
                }

                var json = JsonDocument.Parse(Assert.Single(jsonMessages)).RootElement;
                Assert.Equal("inner-user", json.GetProperty("UserId").GetString());
                // Ensure the property was written exactly once by round-tripping through JsonDocument (which would
                // otherwise expose duplicate properties on enumeration).
                Assert.Equal(1, json.EnumerateObject().Count(p => p.Name == "UserId"));
            });
        }

        [Fact]
        public void EndToEndJson_DuplicateKeysWithinSingleScope_LastValueWins()
        {
            RunWithJsonFormatterCapture(jsonMessages =>
            {
                var loggerFactory = new TestLoggerFactory().AddLambdaLogger(new LambdaLoggerOptions { IncludeScopes = true });
                var logger = loggerFactory.CreateLogger("EndToEndJson");

                var scopeWithDuplicates = new List<KeyValuePair<string, object>>
                {
                    new KeyValuePair<string, object>("Key", "first"),
                    new KeyValuePair<string, object>("Key", "second"),
                };
                using (logger.BeginScope(scopeWithDuplicates))
                {
                    logger.LogInformation("Message");
                }

                var json = JsonDocument.Parse(Assert.Single(jsonMessages)).RootElement;
                Assert.Equal("second", json.GetProperty("Key").GetString());
                Assert.Equal(1, json.EnumerateObject().Count(p => p.Name == "Key"));
            });
        }

        [Fact]
        public void EndToEndJson_ScopeKeyCollidesWithMessageProperty_MessagePropertyPreserved()
        {
            RunWithJsonFormatterCapture(jsonMessages =>
            {
                var loggerFactory = new TestLoggerFactory().AddLambdaLogger(new LambdaLoggerOptions { IncludeScopes = true });
                var logger = loggerFactory.CreateLogger("EndToEndJson");

                var scopeProps = new Dictionary<string, object> { { "Name", "FromScope" } };
                using (logger.BeginScope(scopeProps))
                {
                    logger.LogInformation("User {Name} logged in", "FromMessage");
                }

                var json = JsonDocument.Parse(Assert.Single(jsonMessages)).RootElement;
                Assert.Equal("FromMessage", json.GetProperty("Name").GetString());
                Assert.Equal(1, json.EnumerateObject().Count(p => p.Name == "Name"));
            });
        }

        [Theory]
        [InlineData("Invalid:Key")]
        [InlineData("Invalid{Key")]
        [InlineData("Invalid}Key")]
        [InlineData("Invalid Key")]
        public void EndToEndJson_InvalidScopeKey_SkippedAndSubsequentValidKeyStillBinds(string invalidKey)
        {
            RunWithJsonFormatterCapture(jsonMessages =>
            {
                var loggerFactory = new TestLoggerFactory().AddLambdaLogger(new LambdaLoggerOptions { IncludeScopes = true });
                var logger = loggerFactory.CreateLogger("EndToEndJson");

                var scopeProps = new List<KeyValuePair<string, object>>
                {
                    new KeyValuePair<string, object>(invalidKey, "should-not-appear"),
                    new KeyValuePair<string, object>("ValidKey", "valid-value"),
                };
                using (logger.BeginScope(scopeProps))
                {
                    logger.LogInformation("Message");
                }

                var jsonString = Assert.Single(jsonMessages);
                var json = JsonDocument.Parse(jsonString).RootElement;
                Assert.Equal("valid-value", json.GetProperty("ValidKey").GetString());
                Assert.DoesNotContain("should-not-appear", jsonString);
            });
        }

        [Theory]
        [InlineData("timestamp")]
        [InlineData("level")]
        [InlineData("message")]
        public void EndToEndJson_ScopeKeyCollidesWithReservedField_ReservedFieldNotOverwritten(string reservedKey)
        {
            RunWithJsonFormatterCapture(jsonMessages =>
            {
                var loggerFactory = new TestLoggerFactory().AddLambdaLogger(new LambdaLoggerOptions { IncludeScopes = true });
                var logger = loggerFactory.CreateLogger("EndToEndJson");

                var scopeProps = new Dictionary<string, object> { { reservedKey, "hijacked-value" } };
                using (logger.BeginScope(scopeProps))
                {
                    logger.LogInformation("Message");
                }

                var jsonString = Assert.Single(jsonMessages);
                var json = JsonDocument.Parse(jsonString).RootElement;
                // The reserved value (a real timestamp/level/message string) must never be replaced by the
                // scope's "hijacked-value", and the reserved field must appear exactly once.
                Assert.DoesNotContain("hijacked-value", jsonString);
                Assert.NotEqual("hijacked-value", json.GetProperty(reservedKey).GetString());
                Assert.Equal(1, json.EnumerateObject().Count(p => p.Name == reservedKey));
            });
        }

        [Fact]
        public void EndToEndJson_ScopeKeyCollidesWithConditionalReservedField_NotAddedAsMessageProperty()
        {
            RunWithJsonFormatterCapture(jsonMessages =>
            {
                var loggerFactory = new TestLoggerFactory().AddLambdaLogger(new LambdaLoggerOptions { IncludeScopes = true });
                var logger = loggerFactory.CreateLogger("EndToEndJson");

                // "requestId", "tenantId" and "traceId" are only written by the formatter when the corresponding
                // MessageState value is populated. Since MessageState.AwsRequestId is null in this test, the
                // "requestId" JSON property is not emitted at all - it must not be added as a message property either.
                var scopeProps = new Dictionary<string, object> { { "requestId", "hijacked-value" } };
                using (logger.BeginScope(scopeProps))
                {
                    logger.LogInformation("Message");
                }

                var jsonString = Assert.Single(jsonMessages);
                var json = JsonDocument.Parse(jsonString).RootElement;
                Assert.DoesNotContain("hijacked-value", jsonString);
                Assert.False(json.TryGetProperty("requestId", out _));
            });
        }

        [Fact]
        public void EndToEndJson_ScopesDisabled_ScopePropertiesAbsentFromJson()
        {
            RunWithJsonFormatterCapture(jsonMessages =>
            {
                var loggerFactory = new TestLoggerFactory().AddLambdaLogger(new LambdaLoggerOptions { IncludeScopes = false });
                var logger = loggerFactory.CreateLogger("EndToEndJson");

                var scopeProps = new Dictionary<string, object> { { "RequestId", "abc-123" } };
                using (logger.BeginScope(scopeProps))
                {
                    logger.LogInformation("User {Name} logged in", "Carol");
                }

                var jsonString = Assert.Single(jsonMessages);
                var json = JsonDocument.Parse(jsonString).RootElement;
                Assert.Equal("Carol", json.GetProperty("Name").GetString());
                Assert.False(json.TryGetProperty("RequestId", out _));
            });
        }

        [Fact]
        public void EndToEndJson_ScopeWithNullValue_WritesJsonNull()
        {
            RunWithJsonFormatterCapture(jsonMessages =>
            {
                var loggerFactory = new TestLoggerFactory().AddLambdaLogger(new LambdaLoggerOptions { IncludeScopes = true });
                var logger = loggerFactory.CreateLogger("EndToEndJson");

                var scopeProps = new Dictionary<string, object> { { "NullProp", null } };
                using (logger.BeginScope(scopeProps))
                {
                    logger.LogInformation("Message");
                }

                var jsonString = Assert.Single(jsonMessages);
                var json = JsonDocument.Parse(jsonString).RootElement;
                // The RuntimeSupport JSON formatter omits null-valued message properties entirely rather than
                // writing a JSON null (see JsonLogMessageFormatter.WriteMessageAttributes).
                Assert.False(json.TryGetProperty("NullProp", out _));
            });
        }

        [Fact]
        public void EndToEndJson_NoScopeProvider_NonJsonBehaviorUnaffected()
        {
            using (var writer = new StringWriter())
            {
                ConnectLoggingActionToLogger(message => writer.Write(message));

                var loggerOptions = new LambdaLoggerOptions { IncludeScopes = true };
                var loggerFactory = new TestLoggerFactory().AddLambdaLogger(loggerOptions);
                var logger = loggerFactory.CreateLogger("Default");

                using (logger.BeginScope("First {0}", "scope123"))
                {
                    logger.LogInformation("Hello");
                }

                var text = writer.ToString();
                Assert.Contains("[Information] First scope123 => Default: Hello ", text);
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

            Action<string, string, object[]> loggingWithLevelAction = (level, message, parameters) =>  {
                var formattedMessage = $"{level}: {message}: parameter count: {parameters?.Length}";
                loggingAction(formattedMessage);
            };

            var loggingWithLevelActionField = lambdaLoggerType
                .GetTypeInfo()
                .GetField("_loggingWithLevelAction", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(loggingActionField);

            loggingWithLevelActionField.SetValue(null, loggingWithLevelAction);

            Action<string, Exception, string, object[]> loggingWithExceptionLevelAction = (level, exception, message, parameters) => {
                var formattedMessage = $"{level}: {message}: parameter count: {parameters?.Length}\n{exception?.Message}";
                loggingAction(formattedMessage);
            };

            var loggingWithExceptionLevelActionField = lambdaLoggerType
                .GetTypeInfo()
                .GetField("_loggingWithLevelAndExceptionAction", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(loggingActionField);

            loggingWithExceptionLevelActionField.SetValue(null, loggingWithExceptionLevelAction);
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
