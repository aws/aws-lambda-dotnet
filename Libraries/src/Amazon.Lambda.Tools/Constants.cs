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
