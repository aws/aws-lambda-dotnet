/*
 * Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
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

using Amazon.Lambda.RuntimeSupport.Helpers;
using System;
using System.IO;
using System.Runtime.Loader;
using System.Threading.Tasks;

namespace Amazon.Lambda.RuntimeSupport
{
    class Program
    {
#if NET8_0_OR_GREATER
        [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode(
            "The Main entry point is used in the managed runtime which loads Lambda functions as a class library. " + 
            "The class library mode does not support Native AOT and trimming.")]
#endif
        private static async Task Main(string[] args)
        {
#if NET8_0_OR_GREATER
            AssemblyLoadContext.Default.Resolving += ResolveSnapshotRestoreAssembly;
            if (args.Length == 0)
            {
                throw new ArgumentException("The function handler was not provided via command line arguments.", nameof(args));
            }
#endif
            var handler = args[0];

            RuntimeSupportInitializer runtimeSupportInitializer = new RuntimeSupportInitializer(handler);
            await runtimeSupportInitializer.RunLambdaBootstrap();
        }

#if NET8_0_OR_GREATER
        [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("This code is only exercised in the class library programming model. Native AOT will not use this code path.")]
        private static System.Reflection.Assembly ResolveSnapshotRestoreAssembly(AssemblyLoadContext assemblyContext, System.Reflection.AssemblyName assemblyName)
        {
            const string assemblyPath = "/var/runtime/SnapshotRestore.Registry.dll";
            InternalLogger.GetDefaultLogger().LogInformation("Resolving assembly: " + assemblyName.Name);
            if (string.Equals(assemblyName.Name, "SnapshotRestore.Registry", StringComparison.InvariantCultureIgnoreCase) && File.Exists(assemblyPath))
            {
                return assemblyContext.LoadFromAssemblyPath(assemblyPath);
            }

            return null;
        }
#endif
    }
}
