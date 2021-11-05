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

        public Amazon.Lambda.APIGatewayEvents.APIGatewayProxyResponse SayHello(Amazon.Lambda.APIGatewayEvents.APIGatewayProxyRequest request, Amazon.Lambda.Core.ILambdaContext context)
        {
            var firstNames = default(System.Collections.Generic.IEnumerable<string>);
            if (request.MultiValueQueryStringParameters?.ContainsKey("names") == true)
            {
                firstNames = request.MultiValueQueryStringParameters["names"].Select(q => (string)Convert.ChangeType(q, typeof(string))).ToList();
            }

            greeter.SayHello(firstNames, request, context);

            return new APIGatewayProxyResponse
            {
                StatusCode = 200
            };
        }
    }
}