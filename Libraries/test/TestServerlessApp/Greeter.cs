using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amazon.Lambda.Annotations;

namespace TestServerlessApp
{
    public class Greeter
    {
        [LambdaFunction(Name = "GreeterSayHello", MemorySize = 1024)]
        [HttpApi(HttpApiVersion.V1)]
        public void SayHello([FromQuery(Name = "names")]IEnumerable<string> firstNames)
        {
            if (firstNames == null)
            {
                return;
            }

            foreach (var firstName in firstNames)
            {
                Console.WriteLine($"Hello {firstName}");
            }
        }

        [LambdaFunction(Name = "GreeterSayHelloAsync", Timeout = 50)]
        [HttpApi(HttpApiVersion.V1)]
        public async Task SayHelloAsync()
        {
            Console.WriteLine("Hello");
            await Task.CompletedTask;
        }
    }
}