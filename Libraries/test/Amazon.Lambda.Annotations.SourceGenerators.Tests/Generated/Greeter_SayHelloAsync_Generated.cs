using System;
using System.Collections.Generic;

using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;

namespace TestServerlessApp
{
    public class Greeter_SayHelloAsync_Generated
    {
        private readonly TestServerlessApp.Greeter greeter;

        public Greeter_SayHelloAsync_Generated()
        {
            greeter = new TestServerlessApp.Greeter();
        }

        public async System.Threading.Tasks.Task<APIGatewayProxyResponse> SayHelloAsync(APIGatewayProxyRequest request, ILambdaContext _context)
        {
            await greeter.SayHelloAsync();
            return new APIGatewayProxyResponse
            {
                StatusCode = 200,
            };
        }
    }
}