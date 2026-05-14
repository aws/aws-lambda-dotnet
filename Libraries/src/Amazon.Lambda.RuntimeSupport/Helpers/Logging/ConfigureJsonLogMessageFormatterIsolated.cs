// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Text.Json;

namespace Amazon.Lambda.RuntimeSupport.Helpers.Logging
{
    internal class ConfigureJsonLogMessageFormatterIsolated
    {
        private readonly JsonLogMessageFormatter _formatter;
        public ConfigureJsonLogMessageFormatterIsolated(JsonLogMessageFormatter formatter)
        {
            _formatter = formatter;
        }

        internal void ConfigureCallbackInCore()
        {
            Amazon.Lambda.Core.LambdaLogger.SetConfigureStructuredLoggingAction(ConfigureStructuredLogging);
        }

        private void ConfigureStructuredLogging(Amazon.Lambda.Core.StructuredLoggingOptions options)
        {
            try
            {
                var isolatedOptions = new StructuredLoggingOptions
                {
                    OverrideSerializerOptions = options.OverrideSerializerOptions
                };
            }
            catch(Exception ex)
            {
                InternalLogger.GetDefaultLogger().LogDebug("Failed to configure structured logging. This generally happens when the version of Amazon.Lambda.Core is out of date. Update to latest version of Amazon.Lambda.Core: " + ex.ToString());
            }
        }


        /// <summary>
        /// Mirror the version of StructuredLoggingOptions in Amazon.Lambda.Core because we can't guarantee that the version of Amazon.Lambda.Core used by the customer
        /// will be the same as the version of Amazon.Lambda.RuntimeSupport.
        /// </summary>
        internal class StructuredLoggingOptions
        {
            public JsonSerializerOptions OverrideSerializerOptions { get; set; }
        }
    }
}
