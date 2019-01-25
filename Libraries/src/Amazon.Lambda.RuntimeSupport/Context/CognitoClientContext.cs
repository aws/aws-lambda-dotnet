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
using System;
using System.Collections.Generic;
using ThirdParty.Json.LitJson;

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
                var jsonData = JsonMapper.ToObject(json);

                if (jsonData["client"] != null)
                    result.Client = CognitoClientApplication.FromJsonData(jsonData["client"]);
                if (jsonData["custom"] != null)
                    result.Custom = GetDictionaryFromJsonData(jsonData["custom"]);
                if (jsonData["env"] != null)
                    result.Environment = GetDictionaryFromJsonData(jsonData["env"]);
            }
            return result;
        }

        private static IDictionary<string, string> GetDictionaryFromJsonData(JsonData jsonData)
        {
            var result = new Dictionary<string, string>();

            foreach (var key in jsonData.PropertyNames)
            {
                result.Add(key, jsonData[key].ToString());
            }

            return result;
        }
    }
}
