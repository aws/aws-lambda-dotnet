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
using ThirdParty.Json.LitJson;

namespace Amazon.Lambda.RuntimeSupport
{
    internal class CognitoIdentity : ICognitoIdentity
    {
        public string IdentityId { get; internal set; }

        public string IdentityPoolId { get; internal set; }

        internal static CognitoIdentity FromJson(string json)
        {
            var result = new CognitoIdentity();

            if (!string.IsNullOrWhiteSpace(json))
            {
                var jsonData = JsonMapper.ToObject(json);
                if (jsonData["identityId"] != null)
                    result.IdentityId = jsonData["identityId"].ToString();
                if (jsonData["identityPoolId"] != null)
                    result.IdentityPoolId = jsonData["identityPoolId"].ToString();
            }

            return result;
        }
    }
}
