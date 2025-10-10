using System;
using Amazon.Lambda.RuntimeSupport.Bootstrap;
using Amazon.Lambda.RuntimeSupport.Helpers;

namespace Amazon.Lambda.RuntimeSupport
{
    internal class LambdaBootstrapConfiguration
    {
        internal bool IsCallPreJit { get; set; }
        internal bool IsInitTypeSnapstart { get; set; }

        internal LambdaBootstrapConfiguration(bool isCallPreJit, bool isInitTypeSnapstart)
        {
            if (IsInitTypeSnapstart)
                InternalLogger.GetDefaultLogger().LogInformation("Setting Init type to SnapStart");

            IsCallPreJit = isCallPreJit;
            IsInitTypeSnapstart = isInitTypeSnapstart;
        }

        internal static LambdaBootstrapConfiguration GetDefaultConfiguration(IEnvironmentVariables environmentVariables)
        {
            bool isCallPreJit = UserCodeInit.IsCallPreJit(environmentVariables);
#if NET8_0_OR_GREATER
            bool isInitTypeSnapstart = 
                string.Equals(
                    environmentVariables.GetEnvironmentVariable(Constants.ENVIRONMENT_VARIABLE_AWS_LAMBDA_INITIALIZATION_TYPE),
                    Constants.AWS_LAMBDA_INITIALIZATION_TYPE_SNAP_START);

            return new LambdaBootstrapConfiguration(isCallPreJit, isInitTypeSnapstart);
#else
            return new LambdaBootstrapConfiguration(isCallPreJit, false);
#endif
        }
    }
}
