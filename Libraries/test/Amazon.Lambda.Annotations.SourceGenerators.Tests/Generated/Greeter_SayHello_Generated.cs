using System;
using System.Collections.Generic;

using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;

namespace TestServerlessApp
{
    public class Greeter_SayHello_Generated
    {
        private readonly TestServerlessApp.Greeter greeter;

        public Greeter_SayHello_Generated()
        {
            greeter = new TestServerlessApp.Greeter();
        }

        public APIGatewayProxyResponse SayHello(APIGatewayProxyRequest request, ILambdaContext _context)
        {
            greeter.SayHello();
            return new APIGatewayProxyResponse
            {
                StatusCode = 200,
            };
        }
    }
}