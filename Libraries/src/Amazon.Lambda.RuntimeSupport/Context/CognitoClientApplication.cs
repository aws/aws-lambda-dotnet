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
    internal class CognitoClientApplication : IClientApplication
    {
        public string AppPackageName { get; internal set; }

        public string AppTitle { get; internal set; }

        public string AppVersionCode { get; internal set; }

        public string AppVersionName { get; internal set; }

        public string InstallationId { get; internal set; }

        internal static CognitoClientApplication FromJsonData(JsonData jsonData)
        {
            var result = new CognitoClientApplication();

            if (jsonData != null)
            {
                if (jsonData["app_package_name"] != null)
                    result.AppPackageName = jsonData["app_package_name"].ToString();
                if (jsonData["app_title"] != null)
                    result.AppTitle = jsonData["app_title"].ToString();
                if (jsonData["app_version_code"] != null)
                    result.AppVersionCode = jsonData["app_version_code"].ToString();
                if (jsonData["app_version_name"] != null)
                    result.AppVersionName = jsonData["app_version_name"].ToString();
                if (jsonData["installation_id"] != null)
                    result.InstallationId = jsonData["installation_id"].ToString();
            }

            return result;
        }
    }
}
