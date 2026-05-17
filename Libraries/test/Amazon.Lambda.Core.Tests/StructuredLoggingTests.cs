using System;
using System.Reflection;
using System.Text.Json;
using Xunit;

using Amazon.Lambda.Core;

namespace Amazon.Lambda.Tests
{
    public class StructuredLoggingTests
    {
        [Fact]
        public void SetConfigureStructuredLoggingAction_CallbackReceivesOptions()
        {
            StructuredLoggingOptions receivedOptions = null;

            LambdaLogger.SetConfigureStructuredLoggingAction(options =>
            {
                receivedOptions = options;
            });

            var expectedOptions = new StructuredLoggingOptions
            {
                OverrideSerializerOptions = new JsonSerializerOptions { WriteIndented = true }
            };

            LambdaLogger.ConfigureStructuredLogging(expectedOptions);

            Assert.NotNull(receivedOptions);
            Assert.Same(expectedOptions, receivedOptions);
            Assert.True(receivedOptions.OverrideSerializerOptions.WriteIndented);
        }

        [Fact]
        public void ConfigureStructuredLogging_BeforeRuntimeSetsAction_OptionsFlowThroughCurrentAction()
        {
            StructuredLoggingOptions capturedOptions = null;
            LambdaLogger.SetConfigureStructuredLoggingAction(options =>
            {
                capturedOptions = options;
            });

            var userOptions = new StructuredLoggingOptions
            {
                OverrideSerializerOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }
            };

            LambdaLogger.ConfigureStructuredLogging(userOptions);
            Assert.Same(userOptions, capturedOptions);
        }

        [Fact]
        public void SetConfigureStructuredLoggingAction_PendingOptionsForwardedToNewAction()
        {
            // Reset _placeHolderStructuredLoggingOptions and _configureStructuredLoggingAction to initial state
            // so we can test the forwarding path.
            var placeholderField = typeof(LambdaLogger).GetField("_placeHolderStructuredLoggingOptions", BindingFlags.NonPublic | BindingFlags.Static);
            var actionField = typeof(LambdaLogger).GetField("_configureStructuredLoggingAction", BindingFlags.NonPublic | BindingFlags.Static);

            // Reset to initial state: action stores to placeholder, placeholder is null
            placeholderField.SetValue(null, null);
            actionField.SetValue(null, new Action<StructuredLoggingOptions>(options =>
            {
                placeholderField.SetValue(null, options);
            }));

            // Step 1: User calls ConfigureStructuredLogging before runtime is ready.
            // This stores the options in _placeHolderStructuredLoggingOptions.
            var userOptions = new StructuredLoggingOptions
            {
                OverrideSerializerOptions = new JsonSerializerOptions { WriteIndented = true }
            };
            LambdaLogger.ConfigureStructuredLogging(userOptions);

            Assert.Same(userOptions, placeholderField.GetValue(null));

            // Step 2: Runtime calls SetConfigureStructuredLoggingAction.
            // The pending options should be forwarded to the new action.
            StructuredLoggingOptions forwardedOptions = null;
            LambdaLogger.SetConfigureStructuredLoggingAction(options =>
            {
                forwardedOptions = options;
            });

            Assert.NotNull(forwardedOptions);
            Assert.Same(userOptions, forwardedOptions);
        }

        [Fact]
        public void StructuredLoggingOptions_DefaultsToNull()
        {
            var options = new StructuredLoggingOptions();
            Assert.Null(options.OverrideSerializerOptions);
        }
    }
}
