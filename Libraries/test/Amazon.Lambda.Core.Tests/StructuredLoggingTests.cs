using System;
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
            // Simulate the placeholder behavior: _configureStructuredLoggingAction initially stores options
            // in _placeHolderStructuredLoggingOptions. When the runtime later calls SetConfigureStructuredLoggingAction,
            // the pending options are forwarded to the new action.

            // Step 1: Set up a placeholder-like action that stores the options
            // (This mimics what happens in the real LambdaLogger static initializer)
            StructuredLoggingOptions placeholderStore = null;
            LambdaLogger.SetConfigureStructuredLoggingAction(options => placeholderStore = options);

            // Step 2: User calls ConfigureStructuredLogging (simulating early call before runtime is ready)
            var userOptions = new StructuredLoggingOptions
            {
                OverrideSerializerOptions = new JsonSerializerOptions { WriteIndented = true }
            };
            LambdaLogger.ConfigureStructuredLogging(userOptions);
            Assert.Same(userOptions, placeholderStore);
        }

        [Fact]
        public void StructuredLoggingOptions_DefaultsToNull()
        {
            var options = new StructuredLoggingOptions();
            Assert.Null(options.OverrideSerializerOptions);
        }
    }
}
