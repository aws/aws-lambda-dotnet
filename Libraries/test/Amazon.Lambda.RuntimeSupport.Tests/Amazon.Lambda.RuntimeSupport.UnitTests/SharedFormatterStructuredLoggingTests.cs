using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Amazon.Lambda.RuntimeSupport.Helpers;
using Xunit;

namespace Amazon.Lambda.RuntimeSupport.UnitTests
{
    /// <summary>
    /// Regression tests for https://github.com/aws/aws-lambda-dotnet/issues/2350.
    ///
    /// LogLevelLoggerWriter creates two WrapperTextWriter instances (one for stdout, one for stderr). Before the
    /// fix each writer constructed its OWN JsonLogMessageFormatter, and each formatter registered the customer's
    /// LambdaLogger.ConfigureStructuredLogging callback through a single static setter on
    /// Amazon.Lambda.Core.LambdaLogger, so the second construction overwrote the first. A customer calling
    /// ConfigureStructuredLogging then only affected one formatter, while log messages written to stdout went
    /// through the OTHER formatter that never received the custom JsonSerializerOptions. The fix makes both writers
    /// share a single formatter instance.
    ///
    /// The test injects an IEnvironmentVariables so it never mutates the process-wide AWS_LAMBDA_LOG_FORMAT (which
    /// would race with other tests). It is placed in the serial "StructuredLogging" collection because
    /// ConfigureStructuredLogging mutates a process-wide static on LambdaLogger.
    /// </summary>
    [Collection("StructuredLogging")]
    public class SharedFormatterStructuredLoggingTests
    {
        /// <summary>A value type that serializes to "{}" under default options (mirrors NodaTime.Instant).</summary>
        private readonly struct Instant
        {
            private readonly DateTime _utc;
            public Instant(DateTime utc) => _utc = utc;
            public DateTime ToUtc() => _utc;
        }

        /// <summary>Only this converter (registered via OverrideSerializerOptions) makes an Instant serialize to a value.</summary>
        private sealed class InstantConverter : JsonConverter<Instant>
        {
            public override Instant Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
                => new Instant(reader.GetDateTime());

            public override void Write(Utf8JsonWriter writer, Instant value, JsonSerializerOptions options)
                => writer.WriteStringValue(value.ToUtc().ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture));
        }

        [Fact]
        public void ConfigureStructuredLogging_CustomOptions_ReachFormatterUsedForStdout()
        {
            try
            {
                // JSON log format is supplied through an injected environment, so we do not touch the process-wide
                // environment variable that other (parallel) tests read.
                var environmentVariables = new TestEnvironmentVariables(new Dictionary<string, string>
                {
                    { "AWS_LAMBDA_LOG_FORMAT", "JSON" }
                });

                var stdout = new StringWriter();
                var stderr = new StringWriter();

                // Constructing the writer creates the (now shared) JsonLogMessageFormatter and registers the
                // ConfigureStructuredLogging callback. This is the same code path Lambda uses at startup.
                var writer = new LogLevelLoggerWriter(environmentVariables, stdout, stderr);

                // Customer configures structured logging with a converter that only lives in the options collection.
                var customOptions = new JsonSerializerOptions();
                customOptions.Converters.Add(new InstantConverter());
                Amazon.Lambda.Core.LambdaLogger.ConfigureStructuredLogging(new Amazon.Lambda.Core.StructuredLoggingOptions
                {
                    OverrideSerializerOptions = customOptions
                });

                // Write a structured log with a destructured value type to STDOUT (the path customer logging uses).
                var scheduled = new Instant(new DateTime(2026, 9, 1, 15, 40, 0, DateTimeKind.Utc));
                writer.FormattedWriteLine("Information", "scheduled {@scheduledDate}", scheduled);

                var output = stdout.ToString();

                // With the fix the converter is applied and the ISO string appears. Without the fix the stdout
                // formatter never received the options and the value serialized to "{}".
                Assert.Contains("2026-09-01T15:40:00Z", output);
                Assert.DoesNotContain("\"scheduledDate\":{}", output);
            }
            finally
            {
                // Reset the process-wide callback so this test does not affect others.
                Amazon.Lambda.Core.LambdaLogger.ConfigureStructuredLogging(null);
            }
        }
    }
}
