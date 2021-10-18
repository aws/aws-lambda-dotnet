using System;
using System.Collections.Generic;
using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;

namespace TestServerlessApp
{
    public class Greeter_SayHello_Generated
    {
        private readonly Greeter greeter;

        public Greeter_SayHello_Generated()
        {
            greeter = new Greeter();
        }

        public APIGatewayProxyResponse SayHello(APIGatewayProxyRequest request, ILambdaContext context)
        {
            greeter.SayHello();

            return new APIGatewayProxyResponse
            {
                Headers = new Dictionary<string, string>
                {
                    {"Content-Type", "text/plain"}
                },
                StatusCode = 200
            };
        }
    }
}