using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using Amazon.Lambda.Core;

namespace TestServerlessApp
{
    public class ComplexCalculator_Add_Generated
    {
        private readonly ComplexCalculator complexCalculator;

        public ComplexCalculator_Add_Generated()
        {
            SetExecutionEnvironment();
            complexCalculator = new ComplexCalculator();
        }

        public Amazon.Lambda.APIGatewayEvents.APIGatewayHttpApiV2ProxyResponse Add(Amazon.Lambda.APIGatewayEvents.APIGatewayHttpApiV2ProxyRequest __request__, Amazon.Lambda.Core.ILambdaContext __context__)
        {
            var complexNumbers = __request__.Body;

            var response = complexCalculator.Add(complexNumbers, __context__, __request__);

            var body = System.Text.Json.JsonSerializer.Serialize(response);

            return new Amazon.Lambda.APIGatewayEvents.APIGatewayHttpApiV2ProxyResponse
            {
                Body = body,
                Headers = new Dictionary<string, string>
                {
                    {"Content-Type", "application/json"}
                },
                StatusCode = 200
            };
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

            envValue.Append("amazon-lambda-annotations_0.13.2.0");

            Environment.SetEnvironmentVariable(envName, envValue.ToString());
        }
    }
}