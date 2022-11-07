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
using System.Threading.Tasks;

namespace Amazon.Lambda.RuntimeSupport
{
    class Program
    {
        private static async Task Main(string[] args)
        {
            if (args.Length == 0)
            {
                throw new ArgumentException("The function handler was not provided via command line arguments.", nameof(args));
            }

            var handler = args[0];

            RuntimeSupportInitializer runtimeSupportInitializer = new RuntimeSupportInitializer(handler);
            await runtimeSupportInitializer.RunLambdaBootstrap();
        }
    }
}
