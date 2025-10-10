// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

namespace Amazon.Lambda.RuntimeSupport.Helpers
{
    /// <summary>
    /// This wrapper class is used to add a layer of protection around calling LambdaTraceProvider.SetCurrentTraceId
    /// from Amazon.Lambda.Core. If the provided Lambda function code does not include a version of Amazon.Lambda.Core
    /// that has Amazon.Lambda.Core.LambdaTraceProvider.SetCurrentTraceId a <see cref="System.TypeLoadException"/>
    /// will be thrown when parent calling method is called.
    ///
    /// For example if Amazon.Lambda.Core.LambdaTraceProvider.SetCurrentTraceId was called directly in the bootstrap's main
    /// invoke method the TypeLoadException would be thrown when the invoke method is called giving the invoke method
    /// no time to recover. By having this wrapper the invoke method can call this method if the Core version is out of date
    /// and the TypeLoadException will be thrown at the point of calling this method allowing the main invoke method to
    /// catch the exception and handle it appropriately.
    /// </summary>
    internal class TraceProviderIsolated
    {
        /// <summary>
        /// Set the trace id on the LambdaTraceProvider in Amazon.Lambda.Core.
        /// </summary>
        /// <param name="traceId"></param>
        /// <exception cref="System.TypeLoadException">If the version of Amazon.Lambda.Core used does not contain the SetCurrentTraceId method.</exception>
        internal static void SetCurrentTraceId(string traceId)
        {
#if !ANALYZER_UNIT_TESTS // This precompiler directive is used to avoid the unit tests from needing a dependency on Amazon.Lambda.Core.
            Amazon.Lambda.Core.LambdaTraceProvider.SetCurrentTraceId(traceId);
#endif
        }
    }
}
