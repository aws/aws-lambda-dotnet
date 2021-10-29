using System;
using System.Linq;
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
            var firstNames = request.QueryStringParameters["names"].Split(",").Select(q => (string)Convert.ChangeType(q, typeof(string))).ToList();
            greeter.SayHello(firstNames);

            return new APIGatewayProxyResponse
            {
                StatusCode = 200
            };
        }
    }
}