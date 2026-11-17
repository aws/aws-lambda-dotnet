// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace Amazon.Lambda.RuntimeSupport
{
    /// <summary>
    /// Extracts the W3C trace-context fields (traceparent, tracestate, baggage)
    /// carried on <c>clientContext.w3c</c> at invoke time. Any other key on
    /// <c>w3c</c> is ignored, and any allowlisted key whose value is not a
    /// string is dropped.
    /// </summary>
    internal static class W3CTraceContext
    {
        internal static readonly string[] AllowedFields = { "traceparent", "tracestate", "baggage" };

        private static readonly IReadOnlyDictionary<string, string> Empty =
            new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());

        internal static IReadOnlyDictionary<string, string> FromClientContextJson(string clientContextJson)
        {
            if (string.IsNullOrWhiteSpace(clientContextJson))
            {
                return Empty;
            }

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(clientContextJson);
            }
            catch (JsonException)
            {
                return Empty;
            }

            using (doc)
            {
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    return Empty;
                }

                if (!root.TryGetProperty("w3c", out var w3c))
                {
                    return Empty;
                }

                if (w3c.ValueKind != JsonValueKind.Object)
                {
                    return Empty;
                }

                var fields = new Dictionary<string, string>(AllowedFields.Length);
                foreach (var key in AllowedFields)
                {
                    if (!w3c.TryGetProperty(key, out var value))
                    {
                        continue;
                    }
                    // Allowlisted keys whose value is not a JSON string are dropped.
                    if (value.ValueKind != JsonValueKind.String)
                    {
                        continue;
                    }
                    fields[key] = value.GetString();
                }

                return new ReadOnlyDictionary<string, string>(fields);
            }
        }
    }
}
