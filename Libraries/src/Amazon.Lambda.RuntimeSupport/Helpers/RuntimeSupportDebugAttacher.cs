using Amazon.Lambda.Core;
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
                _internalLogger.LogInformation($"An exception occured while waiting for a debugger to attach. The exception details are as follows:\n{ex}");
            }
        }
    }
}
