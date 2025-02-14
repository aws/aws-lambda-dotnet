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

        internal static LambdaBootstrapConfiguration GetDefaultConfiguration()
        {
            bool isCallPreJit = UserCodeInit.IsCallPreJit();
#if NET8_0_OR_GREATER
            bool isInitTypeSnapstart = 
                string.Equals(
                    Environment.GetEnvironmentVariable(Constants.ENVIRONMENT_VARIABLE_AWS_LAMBDA_INITIALIZATION_TYPE),
                    Constants.AWS_LAMBDA_INITIALIZATION_TYPE_SNAP_START);

            return new LambdaBootstrapConfiguration(isCallPreJit, isInitTypeSnapstart);
#endif
            return new LambdaBootstrapConfiguration(isCallPreJit, false);
        }
    }
}