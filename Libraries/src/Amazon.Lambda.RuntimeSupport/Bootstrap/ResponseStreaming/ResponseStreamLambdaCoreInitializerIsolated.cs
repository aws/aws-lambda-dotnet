// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Lambda.RuntimeSupport.Client.ResponseStreaming;
#pragma warning disable CA2252
namespace Amazon.Lambda.RuntimeSupport
{
    /// <summary>
    /// This class connects the <see cref="ResponseStream"/> created by <see cref="ResponseStreamFactory"/>
    /// to Amazon.Lambda.Core's public response streaming interfaces.
    /// <para>
    /// The deployed Lambda function might reference an older Amazon.Lambda.Core that does not have
    /// the response streaming interfaces. To prevent ReflectionTypeLoadException when customer code
    /// calls GetTypes() on this assembly, NO type in this class (or this assembly) directly implements
    /// ILambdaResponseStream. Instead, we use DispatchProxy to generate the implementation dynamically
    /// at runtime, only when the interface type is confirmed to exist in the loaded Amazon.Lambda.Core.
    /// See: https://github.com/aws/aws-lambda-dotnet/issues/2430
    /// </para>
    /// </summary>
    internal class ResponseStreamLambdaCoreInitializerIsolated
    {
        /// <summary>
        /// Initialize Amazon.Lambda.Core with a factory method for creating response streams.
        /// All type references to ILambdaResponseStream are made via reflection to avoid embedding
        /// the type dependency in this assembly's metadata.
        /// </summary>
        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Response streaming types are preserved by the runtime and only loaded when available")]
        [UnconditionalSuppressMessage("Trimming", "IL2055", Justification = "Response streaming types are preserved by the runtime")]
        [UnconditionalSuppressMessage("Trimming", "IL2060", Justification = "Response streaming types are preserved by the runtime")]
        [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "Response streaming types are preserved by the runtime")]
        [UnconditionalSuppressMessage("Trimming", "IL2080", Justification = "Response streaming types are preserved by the runtime")]
        internal static void InitializeCore()
        {
#if !ANALYZER_UNIT_TESTS
            var coreAssembly = typeof(Amazon.Lambda.Core.ILambdaContext).Assembly;

            // Check if the loaded Amazon.Lambda.Core has the response streaming types.
            // If not (older version loaded from /var/task), bail out gracefully — no exception.
            var interfaceType = coreAssembly.GetType("Amazon.Lambda.Core.ResponseStreaming.ILambdaResponseStream");
            if (interfaceType == null) return;

            var factoryType = coreAssembly.GetType("Amazon.Lambda.Core.ResponseStreaming.LambdaResponseStreamFactory");
            if (factoryType == null) return;

            var setMethod = factoryType.GetMethod("SetLambdaResponseStream", BindingFlags.NonPublic | BindingFlags.Static);
            if (setMethod == null) return;

            // Create a Func<byte[], ILambdaResponseStream> using DispatchProxy.
            // ResponseStreamProxy<T> generates a runtime type implementing T (ILambdaResponseStream)
            // that forwards calls to a ResponseStream instance.
            var proxyFactoryMethod = typeof(ResponseStreamLambdaCoreInitializerIsolated)
                .GetMethod(nameof(BuildFactory), BindingFlags.NonPublic | BindingFlags.Static)
                .MakeGenericMethod(interfaceType);

            var factory = proxyFactoryMethod.Invoke(null, null);
            setMethod.Invoke(null, new object[] { factory });
#endif
        }

        private static Func<byte[], T> BuildFactory<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>() where T : class
        {
            return (byte[] prelude) =>
            {
                var stream = ResponseStreamFactory.CreateStream(prelude);
                var proxy = DispatchProxy.Create<T, ResponseStreamProxy>();
                ((ResponseStreamProxy)(object)proxy).SetInner(stream);
                return proxy;
            };
        }
    }

    /// <summary>
    /// A DispatchProxy that forwards ILambdaResponseStream calls to a ResponseStream instance.
    /// This class does NOT implement ILambdaResponseStream at compile time — DispatchProxy generates
    /// the interface implementation dynamically at runtime, avoiding the ReflectionTypeLoadException.
    /// </summary>
    internal class ResponseStreamProxy : DispatchProxy
    {
        private ResponseStream _inner;

        internal void SetInner(ResponseStream inner)
        {
            _inner = inner;
        }

        protected override object Invoke(MethodInfo targetMethod, object[] args)
        {
            return targetMethod.Name switch
            {
                "WriteAsync" => _inner.WriteAsync((byte[])args[0], (int)args[1], (int)args[2],
                    args.Length > 3 ? (CancellationToken)args[3] : default),
                "Dispose" => DoDispose(),
                "get_BytesWritten" => _inner.BytesWritten,
                "get_HasError" => _inner.HasError,
                _ => null
            };
        }

        private object DoDispose()
        {
            _inner.Dispose();
            return null;
        }
    }
}
