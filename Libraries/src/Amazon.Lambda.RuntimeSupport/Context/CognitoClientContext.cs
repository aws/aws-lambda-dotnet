/*
 * Copyright 2019 Amazon.com, Inc. or its affiliates. All Rights Reserved.
 * 
 * Licensed under the Apache License, Version 2.0 (the "License").
 * You may not use this file except in compliance with the License.
 * A copy of the License is located at
 * 
 *  http://aws.amazon.com/apache2.0
 * 
 * or in the "license" file accompanying this file. This file is distributed
 * on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either
 * express or implied. See the License for the specific language governing
 * permissions and limitations under the License.
 */
using Amazon.Lambda.Core;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Amazon.Lambda.RuntimeSupport
{
    internal class CognitoClientContext : IClientContext
    {
        public IDictionary<string, string> Environment { get; internal set; }

        public IClientApplication Client { get; internal set; }

        public IDictionary<string, string> Custom { get; internal set; }

        internal static CognitoClientContext FromJson(string json)
        {
            var result = new CognitoClientContext();

            if (!string.IsNullOrWhiteSpace(json))
            {
                var jsonData = JsonDocument.Parse(json).RootElement;

                if (jsonData.TryGetProperty("client", out var clientElement))
                    result.Client = CognitoClientApplication.FromJsonData(clientElement);
                if (jsonData.TryGetProperty("custom", out var customElement))
                    result.Custom = GetDictionaryFromJsonData(customElement);
                if (jsonData.TryGetProperty("env", out var envElement))
                    result.Environment = GetDictionaryFromJsonData(envElement);

                return result;
            }

            return result;
        }

        private static IDictionary<string, string> GetDictionaryFromJsonData(JsonElement jsonData)
        {
            return jsonData.EnumerateObject().ToDictionary(properties => properties.Name, properties => properties.Value.ToString());
        }
    }
}
