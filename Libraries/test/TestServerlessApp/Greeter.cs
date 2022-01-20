using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Amazon.Lambda.Annotations;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;

namespace TestServerlessApp
{
    public class Greeter
    {
        [LambdaFunction(Name = "GreeterSayHello", MemorySize = 1024, PackageType = LambdaPackageType.Image)]
        [HttpApi(LambdaHttpMethod.Get, "/Greeter/SayHello", Version = HttpApiVersion.V1)]
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

        [LambdaFunction(Name = "GreeterSayHelloAsync", Timeout = 50, PackageType = LambdaPackageType.Image)]
        [HttpApi(LambdaHttpMethod.Get, "/Greeter/SayHelloAsync", Version = HttpApiVersion.V1)]
        public async Task SayHelloAsync([FromHeader(Name = "names")]IEnumerable<string> firstNames)
        {
            if (firstNames == null)
            {
                return;
            }

            foreach (var firstName in firstNames)
            {
                Console.WriteLine($"Hello {firstName}");
            }
            await Task.CompletedTask;
        }
    }
}