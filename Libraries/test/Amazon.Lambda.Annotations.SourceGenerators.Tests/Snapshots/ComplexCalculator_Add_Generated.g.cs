using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Amazon.Lambda.Core;

namespace TestServerlessApp
{
    public class ComplexCalculator_Add_Generated
    {
        private readonly ComplexCalculator complexCalculator;
        private readonly Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer serializer;

        public ComplexCalculator_Add_Generated()
        {
            SetExecutionEnvironment();
            complexCalculator = new ComplexCalculator();
            serializer = new Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer();
        }

        public Amazon.Lambda.APIGatewayEvents.APIGatewayHttpApiV2ProxyResponse Add(Amazon.Lambda.APIGatewayEvents.APIGatewayHttpApiV2ProxyRequest __request__, Amazon.Lambda.Core.ILambdaContext __context__)
        {
            var complexNumbers = __request__.Body;

            var response = complexCalculator.Add(complexNumbers, __context__, __request__);
            var memoryStream = new MemoryStream();
            serializer.Serialize(response, memoryStream);
            memoryStream.Position = 0;

            // convert stream to string
            StreamReader reader = new StreamReader( memoryStream );
            var body = reader.ReadToEnd();

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

            envValue.Append("lib/amazon-lambda-annotations#1.3.1.0");

            Environment.SetEnvironmentVariable(envName, envValue.ToString());
        }
    }
}