// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using Amazon.Lambda.Core;

namespace Amazon.Lambda.RuntimeSupport.Helpers
{
    /// <summary>
    /// Wrapper around the call that sets <see cref="LambdaContext.Serializer"/>.
    /// The user's Lambda function may reference an older <see cref="Amazon.Lambda.Core"/>
    /// that does not declare <c>ILambdaContext.Serializer</c>. By isolating the assignment
    /// in its own static method, the JIT only attempts to resolve the property when this
    /// method is called — letting the invoke loop wrap it in <c>try/catch</c> for
    /// <see cref="TypeLoadException"/> / <see cref="MissingMethodException"/> and
    /// continue the invocation rather than crashing the process.
    ///
    /// Mirrors the pattern used by <see cref="TraceProviderIsolated"/>,
    /// <see cref="SnapstartHelperCopySnapshotCallbacksIsolated"/>, etc.
    /// </summary>
    internal static class LambdaContextSerializerIsolated
    {
        /// <summary>
        /// Stores <paramref name="serializer"/> on <paramref name="context"/> so
        /// <see cref="ILambdaContext.Serializer"/> returns it on this invocation.
        /// </summary>
        /// <exception cref="TypeLoadException">Thrown when the user's Amazon.Lambda.Core does
        /// not contain <see cref="ILambdaContext.Serializer"/>. Callers must catch.</exception>
        /// <exception cref="MissingMethodException">Thrown by some runtimes when the
        /// property accessor is missing on the loaded <see cref="ILambdaContext"/>.
        /// Callers must catch.</exception>
        internal static void TrySetSerializer(LambdaContext context, ILambdaSerializer serializer)
        {
            if (context == null) return;
            context.Serializer = serializer;
        }
    }
}
