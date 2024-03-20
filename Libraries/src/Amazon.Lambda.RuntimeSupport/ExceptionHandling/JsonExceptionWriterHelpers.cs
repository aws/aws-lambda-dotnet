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

using LitJson;

namespace Amazon.Lambda.RuntimeSupport
{
    internal class JsonExceptionWriterHelpers
    {
        /// <summary>
        /// This method escapes a string for use as a JSON string value.
        /// It was adapted from the PutString method in the LitJson.JsonWriter class.
        /// </summary>
        /// <param name="str"></param>
        public static string EscapeStringForJson(string str)
        {
            if (str == null)
                return null;

            // Create a JsonData object to hold the string
            JsonData jsonData = new JsonData(str);

            // Serialize the JsonData object to a JSON string
            string litjsonString = JsonMapper.ToJson(jsonData);
            return litjsonString.Trim('"').Trim();
        }
    }
}
