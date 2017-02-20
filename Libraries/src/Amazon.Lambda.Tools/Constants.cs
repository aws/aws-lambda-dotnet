using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Amazon.Lambda.Tools
{
    public static class Constants
    {
        public const string IAM_ARN_PREFIX = "arn:aws:iam::";
        public const string AWS_MANAGED_POLICY_ARN_PREFIX = "arn:aws:iam::aws:policy";

        public const string SERVERLESS_TAG_NAME = "AWSServerlessAppNETCore";

        public const int MAX_TEMPLATE_BODY_IN_REQUEST_SIZE = 50000;

        // The .NET Core 1.0 version of the runtime hierarchies for .NET Core taken from the corefx repository
        // https://github.com/dotnet/corefx/blob/release/1.0.0/pkg/Microsoft.NETCore.Platforms/runtime.json
#if NETCORE
        internal const string RUNTIME_HIERARCHY = "Amazon.Lambda.Tools.Resources.netcore.runtime.hierarchy.json";
#else
        internal const string RUNTIME_HIERARCHY = "Amazon.AWSToolkit.Lambda.LambdaTools.Resources.netcore.runtime.hierarchy.json";
#endif

        // The closest match to Amazon Linux
        internal const string RUNTIME_HIERARCHY_STARTING_POINT = "rhel.7.2-x64";

        /// <summary>
        /// The assume role policy that gives Lambda permission to assume the role. This is used
        /// when the deploy tool creates a new role for a function.
        /// </summary>
        public static readonly string LAMBDA_ASSUME_ROLE_POLICY =
@"
{
  ""Version"": ""2012-10-17"",
  ""Statement"": [
    {
      ""Sid"": """",
      ""Effect"": ""Allow"",
      ""Principal"": {
        ""Service"": ""lambda.amazonaws.com""
      },
      ""Action"": ""sts:AssumeRole""
    }
  ]
}
".Trim();

    }
}
