using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using Amazon.Lambda.Core;

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
            var validationErrors = new List<string>();

            var complexNumbers = default(System.Collections.Generic.IList<System.Collections.Generic.IList<int>>);
            try
            {
                complexNumbers = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.IList<System.Collections.Generic.IList<int>>>(request.Body);
            }
            catch (Exception e)
            {
                validationErrors.Add($"Value {request.Body} at 'body' failed to satisfy constraint: {e.Message}");
            }

            // return 400 Bad Request if there exists a validation error
            if (validationErrors.Any())
            {
                return new Amazon.Lambda.APIGatewayEvents.APIGatewayHttpApiV2ProxyResponse
                {
                    Body = @$"{{""message"": ""{validationErrors.Count} validation error(s) detected: {string.Join(",", validationErrors)}""}}",
                    Headers = new Dictionary<string, string>
                    {
                        {"Content-Type", "application/json"},
                        {"x-amzn-ErrorType", "ValidationException"}
                    },
                    StatusCode = 400
                };
            }

            var response = complexCalculator.Subtract(complexNumbers);

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

            envValue.Append("amazon-lambda-annotations_0.5.0.0");

            Environment.SetEnvironmentVariable(envName, envValue.ToString());
        }
    }
}