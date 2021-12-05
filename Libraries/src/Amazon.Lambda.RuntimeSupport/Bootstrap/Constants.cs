using System.Reflection;

namespace Amazon.Lambda.RuntimeSupport.Bootstrap
{
    internal class Constants
    {
        internal const string ENVIRONMENT_VARIABLE_AWS_LAMBDA_DOTNET_PREJIT = "AWS_LAMBDA_DOTNET_PREJIT";
        internal const string ENVIRONMENT_VARIABLE_AWS_LAMBDA_INITIALIZATION_TYPE = "AWS_LAMBDA_INITIALIZATION_TYPE";
        internal const string ENVIRONMENT_VARIABLE_LANG = "LANG";
        internal const string AWS_LAMBDA_INITIALIZATION_TYPE_PC = "provisioned-concurrency";
        internal const string AWS_LAMBDA_INITIALIZATION_TYPE_ON_DEMAND = "on-demand";

        internal enum AwsLambdaDotNetPreJit
        {
            Never,
            Always,
            ProvisionedConcurrency
        }

        internal const BindingFlags DefaultFlags = BindingFlags.DeclaredOnly | BindingFlags.NonPublic | BindingFlags.Public
                                                   | BindingFlags.Instance | BindingFlags.Static;
    }
}