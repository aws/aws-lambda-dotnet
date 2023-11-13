using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Amazon.Lambda.Core;

namespace TestServerlessApp
{
    public class IntrinsicExample_HasIntrinsic_Generated
    {
        private readonly IntrinsicExample intrinsicExample;
        private readonly Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer serializer;

        public IntrinsicExample_HasIntrinsic_Generated()
        {
            SetExecutionEnvironment();
            intrinsicExample = new IntrinsicExample();
            serializer = new Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer();
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

            envValue.Append("amazon-lambda-annotations_1.1.0.0");

            Environment.SetEnvironmentVariable(envName, envValue.ToString());
        }
    }
}