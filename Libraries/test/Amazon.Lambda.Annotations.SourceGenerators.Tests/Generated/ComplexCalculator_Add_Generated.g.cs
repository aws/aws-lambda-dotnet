using System;
using System.Collections.Generic;
using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;

namespace TestServerlessApp
{
    public class ComplexCalculator_Add_Generated
    {
        private readonly ComplexCalculator complexCalculator;

        public ComplexCalculator_Add_Generated()
        {
            complexCalculator = new ComplexCalculator();
        }

        public APIGatewayProxyResponse Add(APIGatewayProxyRequest request, ILambdaContext context)
        {
            var response = complexCalculator.Add();

            var body = System.Text.Json.JsonSerializer.Serialize(response);

            return new APIGatewayProxyResponse
            {
                Body = body,
                Headers = new Dictionary<string, string>
                {
                    {"Content-Type", "text/plain"}
                },
                StatusCode = 200
            };
        }
    }
}