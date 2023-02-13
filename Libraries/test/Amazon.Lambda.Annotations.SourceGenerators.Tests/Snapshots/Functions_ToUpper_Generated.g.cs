using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using Amazon.Lambda.Core;

namespace TestServerlessApp.Sub1
{
    public class Functions_ToUpper_Generated
    {
        private readonly Functions functions;

        public Functions_ToUpper_Generated()
        {
            SetExecutionEnvironment();
            functions = new Functions();
        }

        public string ToUpper(string text)
        {
            return functions.ToUpper(text);
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

            envValue.Append("amazon-lambda-annotations_0.12.0.0");

            Environment.SetEnvironmentVariable(envName, envValue.ToString());
        }
    }
}