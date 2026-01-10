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
using System.Collections.Generic;
using System.IO;
using System.Runtime.Loader;
using System.Threading.Tasks;
using Amazon.Lambda.RuntimeSupport.Bootstrap;

namespace Amazon.Lambda.RuntimeSupport
{
    class Program
    {
        // .NET 10 considers adding RequiresUnreferencedCode on the entry point a warning. Our situation is different then the normal use case in that the only time
        // the Main exists in the Lambda class library mode which will never be used for Native AOT.
#pragma warning disable IL2123
#if NET8_0_OR_GREATER
        [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode(
            "The Main entry point is used in the managed runtime which loads Lambda functions as a class library. " +
            "The class library mode does not support Native AOT and trimming.")]
#endif
        private static async Task Main(string[] args)
        {
#if NET8_0_OR_GREATER
            AssemblyLoadContext.Default.Resolving += ResolveSnapshotRestoreAssembly;
#endif
            if (args.Length == 0)
            {
                throw new ArgumentException("The function handler was not provided via command line arguments.", nameof(args));
            }
            var handler = args[0];

            var lambdaBootstrapOptions = ParseCommandLineArguments(args);

            RuntimeSupportInitializer runtimeSupportInitializer = new RuntimeSupportInitializer(handler, lambdaBootstrapOptions);
            await runtimeSupportInitializer.RunLambdaBootstrap();
        }
#pragma warning restore IL2123

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

        /// <summary>
        /// Parse the command line args to create a <see cref="LambdaBootstrapOptions"/> object
        /// which contains configuration that overrides the use of Environment Variables.
        /// This is only for testing purposes and should not be used in production environments.
        /// </summary>
        /// <param name="args">Command line arguments</param>
        /// <returns><see cref="LambdaBootstrapOptions"/> object which contains configuration
        /// that overrides the use of Environment Variables</returns>
        /// <exception cref="ArgumentException">Thrown if a command line argument is invalid</exception>
        private static LambdaBootstrapOptions ParseCommandLineArguments(string[] args)
        {
            var option = new LambdaBootstrapOptions();
            if (args.Length <= 1)
                return option;

            var arguments = new Dictionary<string, string>();
            for (int i = 1; i < args.Length; i++)
            {
                if (string.IsNullOrEmpty(args[i]))
                    throw new ArgumentException("The command line argument cannot be null or empty.", nameof(args));

                var key = args[i];
                var valueIndex = i + 1;

                if (!key.StartsWith("-"))
                    throw new ArgumentException($"The command line argument '{key}' is invalid.", nameof(args));

                string value = null;
                if (valueIndex < args.Length  &&
                    !args[valueIndex].StartsWith("-"))
                {
                    value = args[valueIndex];
                    i++;
                }

                arguments[key] = value;
            }

            if (arguments.TryGetValue(Constants.CMDLINE_ARG_RUNTIME_API_CLIENT, out var argument))
                option.RuntimeApiEndpoint = argument;

            return option;
        }
    }
}
