// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Amazon.Lambda.RuntimeSupport.Helpers;
using Amazon.Lambda.RuntimeSupport.Helpers.Logging;
using Xunit;

namespace Amazon.Lambda.RuntimeSupport.UnitTests
{
    /// <summary>
    /// Tests the reflection based wiring of the structured logging configuration callback used by the class library
    /// programming model on the managed runtime. In that model Amazon.Lambda.RuntimeSupport is compiled against its own
    /// bundled Amazon.Lambda.Core, while the customer's Amazon.Lambda.Core is loaded separately. The compile-time
    /// wiring in JsonLogMessageFormatter's constructor therefore targets the wrong assembly and the customer's call to
    /// LambdaLogger.ConfigureStructuredLogging never reaches the formatter.
    ///
    /// See https://github.com/aws/aws-lambda-dotnet/issues/2350.
    /// </summary>
    public class StructuredLoggingCustomerCoreTests
    {
        public class Product
        {
            public string Name { get; set; }
            public int Inventory { get; set; }
            public override string ToString() => $"{Name} {Inventory}";
        }

        /// <summary>
        /// Simulates the managed runtime wiring: after the customer's Amazon.Lambda.Core assembly is discovered, the
        /// UserCodeLoader wires the structured logging callback into it via reflection. This test verifies the
        /// reflection path (ConfigureCallbackInCore(Assembly, ...) reached through
        /// WireStructuredLoggingCallbacksToCustomerCore) actually delivers the customer's JsonSerializerOptions to the
        /// formatter, which is the behavior that was missing before the fix for issue #2350.
        /// </summary>
        [Fact]
        public void ReflectionWiring_DeliversOverrideSerializerOptions_ToFormatter()
        {
            var formatter = new JsonLogMessageFormatter();

            // The assembly that actually defines the Amazon.Lambda.Core.LambdaLogger type the test references. In the
            // real managed runtime this is the customer's copy, distinct from the RuntimeSupport bundled copy.
            var customerCoreAssembly = typeof(Amazon.Lambda.Core.LambdaLogger).Assembly;

            // Wire using the reflection based path used by the class library model.
            JsonLogMessageFormatter.WireStructuredLoggingCallbacksToCustomerCore(customerCoreAssembly);

            var customOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };

            // Customer calls the public API on their Amazon.Lambda.Core. Because the reflection wiring registered the
            // callback against this assembly's LambdaLogger, it must flow through to the formatter.
            Amazon.Lambda.Core.LambdaLogger.ConfigureStructuredLogging(new Amazon.Lambda.Core.StructuredLoggingOptions
            {
                OverrideSerializerOptions = customOptions
            });

            var state = new MessageState
            {
                AwsRequestId = "1234",
                Level = LogLevelLoggerWriter.LogLevel.Information,
                MessageTemplate = "Product is {@product}",
                MessageArguments = new object[] { new Product { Name = "Widget", Inventory = 100 } },
                TimeStamp = DateTime.UtcNow
            };

            var json = formatter.FormatMessage(state);
            var doc = JsonDocument.Parse(json);

            // camelCase naming policy from the customer supplied options must be applied by the formatter.
            Assert.Equal(JsonValueKind.Object, doc.RootElement.GetProperty("product").ValueKind);
            Assert.Equal("Widget", doc.RootElement.GetProperty("product").GetProperty("name").GetString());
            Assert.Equal(100, doc.RootElement.GetProperty("product").GetProperty("inventory").GetInt32());
        }

        /// <summary>
        /// The reflection wiring must be resilient: passing an assembly that does not contain the structured logging
        /// types (simulating an older Amazon.Lambda.Core in the deployment bundle) must not throw.
        /// </summary>
        [Fact]
        public void ReflectionWiring_AssemblyWithoutStructuredLoggingTypes_DoesNotThrow()
        {
            var formatter = new JsonLogMessageFormatter();

            // System.Private.CoreLib clearly has no Amazon.Lambda.Core.LambdaLogger type.
            var unrelatedAssembly = typeof(object).Assembly;

            var ex = Record.Exception(() =>
                ConfigureJsonLogMessageFormatterIsolated.ConfigureCallbackInCore(unrelatedAssembly, options => { }));

            Assert.Null(ex);
        }

        [Fact]
        public void ReflectionWiring_NullAssembly_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                ConfigureJsonLogMessageFormatterIsolated.ConfigureCallbackInCore((Assembly)null, options => { }));
        }
    }
}
