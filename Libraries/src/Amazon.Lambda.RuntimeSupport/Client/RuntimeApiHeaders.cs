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
using System.Collections.Generic;
using System.Linq;

namespace Amazon.Lambda.RuntimeSupport
{
    internal class RuntimeApiHeaders
    {
        internal const string HeaderAwsRequestId = "Lambda-Runtime-Aws-Request-Id";
        internal const string HeaderTraceId = "Lambda-Runtime-Trace-Id";
        internal const string HeaderClientContext = "Lambda-Runtime-Client-Context";
        internal const string HeaderCognitoIdentity = "Lambda-Runtime-Cognito-Identity";
        internal const string HeaderDeadlineMs = "Lambda-Runtime-Deadline-Ms";
        internal const string HeaderInvokedFunctionArn = "Lambda-Runtime-Invoked-Function-Arn";

        public RuntimeApiHeaders(Dictionary<string, IEnumerable<string>> headers)
        {
            DeadlineMs = GetHeaderValueOrNull(headers, HeaderDeadlineMs);
            AwsRequestId = GetHeaderValueRequired(headers, HeaderAwsRequestId);
            ClientContextJson = GetHeaderValueOrNull(headers, HeaderClientContext);
            CognitoIdentityJson = GetHeaderValueOrNull(headers, HeaderCognitoIdentity);
            InvokedFunctionArn = GetHeaderValueOrNull(headers, HeaderInvokedFunctionArn);
            TraceId = GetHeaderValueOrNull(headers, HeaderTraceId);
        }

        public string AwsRequestId { get; private set; }
        public string InvokedFunctionArn { get; private set; }
        public string TraceId { get; private set; }
        public string ClientContextJson { get; private set; }
        public string CognitoIdentityJson { get; private set; }
        public string DeadlineMs { get; private set; }

        private string GetHeaderValueRequired(Dictionary<string, IEnumerable<string>> headers, string header)
        {
            return headers[header].FirstOrDefault();
        }

        private string GetHeaderValueOrNull(Dictionary<string, IEnumerable<string>> headers, string header)
        {
            if (headers.ContainsKey(header))
            {
                return headers[header].FirstOrDefault();
            }

            return null;
        }
    }

}
