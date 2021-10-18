using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;

namespace TestServerlessApp
{
    public class Greeter_SayHelloAsync_Generated
    {
        private readonly Greeter greeter;

        public Greeter_SayHelloAsync_Generated()
        {
            greeter = new Greeter();
        }

        public async Task<APIGatewayProxyResponse> SayHelloAsync(APIGatewayProxyRequest request, ILambdaContext context)
        {
            await greeter.SayHelloAsync();

            return new APIGatewayProxyResponse
            {
                StatusCode = 200
            };
        }
    }
}