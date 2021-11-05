using System;
using System.Linq;
using System.Collections.Generic;
using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;

namespace TestServerlessApp
{
    public class ComplexCalculator_Subtract_Generated
    {
        private readonly ComplexCalculator complexCalculator;

        public ComplexCalculator_Subtract_Generated()
        {
            complexCalculator = new ComplexCalculator();
        }

        public APIGatewayHttpApiV2ProxyResponse Subtract(Amazon.Lambda.APIGatewayEvents.APIGatewayHttpApiV2ProxyRequest request, Amazon.Lambda.Core.ILambdaContext context)
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
    }
}