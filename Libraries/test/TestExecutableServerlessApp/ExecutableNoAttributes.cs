using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;

namespace TestServerlessApp
{
    public class ExecutableNoAttributes
    {
        public void SayHello([FromQuery(Name = "names")]IEnumerable<string> firstNames, APIGatewayProxyRequest request, ILambdaContext context)
        {
            context.Logger.LogLine($"Request {JsonSerializer.Serialize(request)}");

            if (firstNames == null)
            {
                return;
            }

            foreach (var firstName in firstNames)
            {
                Console.WriteLine($"Hello {firstName}");
            }
        }
    }
}