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

using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Amazon.Lambda.RuntimeSupport.Helpers
{
    /// <summary>
    /// RuntimeSupportDebugAttacher class responsible for waiting for a debugger to attach.
    /// </summary>
    public class RuntimeSupportDebugAttacher
    {
        private readonly InternalLogger _internalLogger;
        private const string DebuggingEnvironmentVariable = "_AWS_LAMBDA_DOTNET_DEBUGGING";

        /// <summary>
        /// RuntimeSupportDebugAttacher constructor.
        /// </summary>
        public RuntimeSupportDebugAttacher()
        {
            _internalLogger = InternalLogger.ConsoleLogger;
        }

        /// <summary>
        /// The function tries to wait for a debugger to attach.
        /// </summary>
        public async Task TryAttachDebugger()
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(DebuggingEnvironmentVariable)))
            {
                return;
            }

            try
            {
                _internalLogger.LogInformation("Waiting for the debugger to attach...");

                var timeout = DateTimeOffset.Now.Add(TimeSpan.FromMinutes(10));

                while (!Debugger.IsAttached)
                {
                    if (DateTimeOffset.Now > timeout)
                    {
                        _internalLogger.LogInformation("Timeout. Proceeding without debugger.");
                        return;
                    }

                    await Task.Delay(TimeSpan.FromMilliseconds(50));
                }
            }
            catch (Exception ex)
            {
                _internalLogger.LogInformation($"An exception occurred while waiting for a debugger to attach. The exception details are as follows:\n{ex}");
            }
        }
    }
}
