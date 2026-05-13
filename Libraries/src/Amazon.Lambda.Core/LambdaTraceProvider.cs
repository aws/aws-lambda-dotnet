// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Threading;

namespace Amazon.Lambda.Core
{
    /// <summary>
    /// Provides global access to the current trace id for the current Lambda event invocation.
    /// </summary>
    public class LambdaTraceProvider
    {
        // Use separate backing fields based on if multi-concurrency is being used or not.
        // This is done because accessing from the AsyncLocal is slower then a backing string field
        // so only use AsyncLocal when we have to in a multi-concurrency scenario.
        private static string _traceIdField;
        private readonly static AsyncLocal<string> _traceIdStorage = new AsyncLocal<string>();

        internal static void SetCurrentTraceId(string traceId)
        {
            if (Utils.IsUsingMultiConcurrency)
                _traceIdStorage.Value = traceId;
            else
                _traceIdField = traceId;
        }

        /// <summary>
        /// The current trace id for the current Lambda event invocation.
        /// </summary>
        public static string CurrentTraceId
        {
            get
            {
                if (Utils.IsUsingMultiConcurrency)
                    return _traceIdStorage.Value;
                else if (_traceIdField != null)
                    return _traceIdField;

                // Fallback to the environment variable if the backing field is not set.
                // This would happen if the version of Amazon.Lambda.RuntimeSupport being used is out of date
                // and doesn't call SetCurrentTraceId.
                return Environment.GetEnvironmentVariable(Constants.ENV_VAR_AWS_LAMBDA_TRACE_ID);
            }
        }
    }
}
