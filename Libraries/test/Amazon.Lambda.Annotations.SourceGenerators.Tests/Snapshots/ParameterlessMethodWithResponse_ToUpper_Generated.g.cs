using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Amazon.Lambda.Core;

namespace TestServerlessApp
{
    public class ParameterlessMethodWithResponse_NoParameterWithResponse_Generated
    {
        private readonly ParameterlessMethodWithResponse parameterlessMethodWithResponse;
        private readonly Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer serializer;

        public ParameterlessMethodWithResponse_NoParameterWithResponse_Generated()
        {
            SetExecutionEnvironment();
            parameterlessMethodWithResponse = new ParameterlessMethodWithResponse();
            serializer = new Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer();
        }

        public string NoParameterWithResponse(Stream stream)
        {
            return parameterlessMethodWithResponse.NoParameterWithResponse();
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