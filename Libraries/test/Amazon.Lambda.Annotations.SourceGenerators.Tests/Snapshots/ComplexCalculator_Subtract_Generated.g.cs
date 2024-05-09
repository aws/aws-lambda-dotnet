using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Amazon.Lambda.Core;
using System.Text.Json;

namespace TestServerlessApp
{
    public class ComplexCalculator_Subtract_Generated
    {
        private readonly ComplexCalculator complexCalculator;
        private readonly Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer serializer;

        public ComplexCalculator_Subtract_Generated()
        {
            SetExecutionEnvironment();
            complexCalculator = new ComplexCalculator();
            serializer = new Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer();
        }

        public System.IO.Stream Subtract(Amazon.Lambda.APIGatewayEvents.APIGatewayHttpApiV2ProxyRequest __request__, Amazon.Lambda.Core.ILambdaContext __context__)
        {
            var validationErrors = new List<string>();

            var complexNumbers = default(System.Collections.Generic.IList<System.Collections.Generic.IList<int>>);
            try
            {
                // convert string to stream
                var byteArray = Encoding.ASCII.GetBytes(__request__.Body);
                var stream = new MemoryStream(byteArray);
                complexNumbers = serializer.Deserialize<System.Collections.Generic.IList<System.Collections.Generic.IList<int>>>(stream);
            }
            catch (Exception e)
            {
                validationErrors.Add($"Value {__request__.Body} at 'body' failed to satisfy constraint: {e.Message}");
            }

            // return 400 Bad Request if there exists a validation error
            if (validationErrors.Any())
            {
                var errorResult = new Amazon.Lambda.APIGatewayEvents.APIGatewayHttpApiV2ProxyResponse
                {
                    Body = @$"{{""message"": ""{validationErrors.Count} validation error(s) detected: {string.Join(",", validationErrors)}""}}",
                    Headers = new Dictionary<string, string>
                    {
                        {"Content-Type", "application/json"},
                        {"x-amzn-ErrorType", "ValidationException"}
                    },
                    StatusCode = 400
                };

                var errorStream = new MemoryStream();
                JsonSerializer.Serialize(errorStream, errorResult, typeof(Amazon.Lambda.APIGatewayEvents.APIGatewayHttpApiV2ProxyResponse));
                errorStream.Position = 0;
                return errorStream;
            }
            var result = complexCalculator.Subtract(complexNumbers);
            var memoryStream = new MemoryStream();
            serializer.Serialize(result, memoryStream);
            memoryStream.Position = 0;

            // convert stream to string
            StreamReader reader = new StreamReader(memoryStream);
            var body = reader.ReadToEnd();
            var response = new Amazon.Lambda.APIGatewayEvents.APIGatewayHttpApiV2ProxyResponse
            {
                Body = body,
                StatusCode = 200,
                Headers = new Dictionary<string, string>
                {
                    {"Content-Type", "application/json"}
                }
            };

            var responseStream = new MemoryStream();
            JsonSerializer.Serialize(responseStream, response, typeof(Amazon.Lambda.APIGatewayEvents.APIGatewayHttpApiV2ProxyResponse));
            responseStream.Position = 0;
            return responseStream;
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

            envValue.Append("lib/amazon-lambda-annotations#1.3.1.0");

            Environment.SetEnvironmentVariable(envName, envValue.ToString());
        }
    }
}