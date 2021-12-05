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
using System.Text.Json;

namespace Amazon.Lambda.RuntimeSupport
{
    internal class CognitoClientApplication : IClientApplication
    {
        public string AppPackageName { get; internal set; }

        public string AppTitle { get; internal set; }

        public string AppVersionCode { get; internal set; }

        public string AppVersionName { get; internal set; }

        public string InstallationId { get; internal set; }

        internal static CognitoClientApplication FromJsonData(JsonElement jsonData)
        {
            var result = new CognitoClientApplication();

            if (jsonData.TryGetProperty("app_package_name", out var nameElement))
                result.AppPackageName = nameElement.GetString();
            if (jsonData.TryGetProperty("app_title", out var tileElement))
                result.AppTitle = tileElement.GetString();
            if (jsonData.TryGetProperty("app_version_code", out var codeElement))
                result.AppVersionCode = codeElement.GetString();
            if (jsonData.TryGetProperty("app_version_name", out var versionNameElement))
                result.AppVersionName = versionNameElement.GetString();
            if (jsonData.TryGetProperty("installation_id", out var installElement))
                result.InstallationId = installElement.GetString();

            return result;
        }
    }
}
