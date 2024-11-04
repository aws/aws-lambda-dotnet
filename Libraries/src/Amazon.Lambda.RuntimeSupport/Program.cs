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

using System;
using System.Diagnostics;
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
            string handler = null;
            bool startTestTool = false;
            int port = 0;

            for (var i = 0; i < args.Length; i++)
            {
                if (handler == null && !args[i].StartsWith("--"))
                {
                    handler = args[i];
                }
                else if (string.Equals(args[i], "--port"))
                {
                    if (i + 1 < args.Length)
                    {
                        if (!int.TryParse(args[i + 1], out port))
                        {
                            throw new ArgumentException("The value for --port switch is not a valid port number.", "--port");
                        }
                    }
                }
                else if (string.Equals(args[i], "--start-testtool"))
                {
                    startTestTool = true;
                }
            }

            if (handler == null)
            {
                throw new ArgumentException("The function handler was not provided via command line arguments.", nameof(args));
            }

            if (port != 0)
            {
                Environment.SetEnvironmentVariable("AWS_LAMBDA_RUNTIME_API", $"localhost:{port}");
            }

            if (startTestTool)
            {
                StartTestTool(port);
            }

            RuntimeSupportInitializer runtimeSupportInitializer = new RuntimeSupportInitializer(handler);
            await runtimeSupportInitializer.RunLambdaBootstrap();
        }

        private static void StartTestTool(int port)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                Arguments = $"--port {port}",
                FileName = $"{Environment.GetEnvironmentVariable("USERPROFILE")}\\.dotnet\\tools\\dotnet-lambda-test-tool-8.0.exe"
            };

            Process.Start(startInfo);
        }
    }
}
