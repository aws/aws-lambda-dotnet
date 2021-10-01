using System;
using System.Collections.Generic;

using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;

namespace TestServerlessApp
{
    public class ComplexCalculator_Add_Generated
    {
        private readonly TestServerlessApp.ComplexCalculator complexCalculator;

        public ComplexCalculator_Add_Generated()
        {
            complexCalculator = new TestServerlessApp.ComplexCalculator();
        }

        public APIGatewayProxyResponse Add(APIGatewayProxyRequest request, ILambdaContext _context)
        {
            var response = complexCalculator.Add();
            var body = System.Text.Json.JsonSerializer.Serialize(response);

            return new APIGatewayProxyResponse
            {
                StatusCode = 200,
                Body = body,
                Headers = new Dictionary<string, string>
                {
                    { "Content-Type", "text/plain" }
                }
            };
        }
    }
}