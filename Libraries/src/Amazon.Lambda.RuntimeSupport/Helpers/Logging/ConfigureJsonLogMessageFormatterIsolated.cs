// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Text.Json;

namespace Amazon.Lambda.RuntimeSupport.Helpers.Logging
{
    internal class ConfigureJsonLogMessageFormatterIsolated
    {
        internal static void ConfigureCallbackInCore(Action<StructuredLoggingOptions> callback)
        {
            Amazon.Lambda.Core.LambdaLogger.SetConfigureStructuredLoggingAction((Amazon.Lambda.Core.StructuredLoggingOptions coreOptions) =>
            {
                if (coreOptions == null)
                {
                    callback(null);
                    return;
                }

                var isolatedOptions = new StructuredLoggingOptions();
                try
                {
                    isolatedOptions.OverrideSerializerOptions = coreOptions.OverrideSerializerOptions;
                }
                catch (Exception ex)
                {
                    InternalLogger.GetDefaultLogger().LogDebug("Failed to configure structured logging. This generally happens when the version of Amazon.Lambda.Core is out of date. Update to latest version of Amazon.Lambda.Core: " + ex.ToString());
                }

                callback(isolatedOptions);
            });
        }
    }
}
