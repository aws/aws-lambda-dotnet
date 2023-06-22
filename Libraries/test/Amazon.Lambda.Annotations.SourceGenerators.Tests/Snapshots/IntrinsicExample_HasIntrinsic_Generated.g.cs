using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using Amazon.Lambda.Core;

namespace TestServerlessApp
{
    public class IntrinsicExample_HasIntrinsic_Generated
    {
        private readonly IntrinsicExample intrinsicExample;

        public IntrinsicExample_HasIntrinsic_Generated()
        {
            SetExecutionEnvironment();
            intrinsicExample = new IntrinsicExample();
        }

        public void HasIntrinsic(string text, Amazon.Lambda.Core.ILambdaContext __context__)
        {
            intrinsicExample.HasIntrinsic(text, __context__);
        }

        private static void SetExecutionEnvironment()
        {
            const string envName = "AWS_EXECUTION_ENV";

            var envValue = new StringBuilder();

            // If there is an existing execution environment variable add the annotations package as a suffix.
            if(!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(envName)))
            {
                envValue.Append($"{Environment.GetEnvironmentVariable(envName)}_");
            }

            envValue.Append("amazon-lambda-annotations_0.13.4.0");

            Environment.SetEnvironmentVariable(envName, envValue.ToString());
        }
    }
}