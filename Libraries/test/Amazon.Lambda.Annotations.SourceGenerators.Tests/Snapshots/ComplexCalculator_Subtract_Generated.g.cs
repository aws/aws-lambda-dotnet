using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;

namespace TestServerlessApp
{
    public class ComplexCalculator_Subtract_Generated
    {
        private readonly ComplexCalculator complexCalculator;

        public ComplexCalculator_Subtract_Generated()
        {
            SetExecutionEnvironment();
            complexCalculator = new ComplexCalculator();
        }

        public Amazon.Lambda.APIGatewayEvents.APIGatewayHttpApiV2ProxyResponse Subtract(Amazon.Lambda.APIGatewayEvents.APIGatewayHttpApiV2ProxyRequest request, Amazon.Lambda.Core.ILambdaContext context)
        {
            var complexNumbers = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.IList<System.Collections.Generic.IList<int>>>(request.Body);

            var response = complexCalculator.Subtract(complexNumbers);

            var body = System.Text.Json.JsonSerializer.Serialize(response);

            return new APIGatewayHttpApiV2ProxyResponse
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

            envValue.Append("amazon-lambda-annotations_0.1.0.0");

            Environment.SetEnvironmentVariable(envName, envValue.ToString());
        }
    }
}