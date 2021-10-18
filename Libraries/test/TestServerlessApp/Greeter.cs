using System;
using System.Threading.Tasks;
using Amazon.Lambda.Annotations;

namespace TestServerlessApp
{
    public class Greeter
    {
        [LambdaFunction]
        [HttpApi(HttpApiVersion.V1)]
        public void SayHello()
        {
            Console.WriteLine("Hello");
        }

        [LambdaFunction]
        [HttpApi(HttpApiVersion.V1)]
        public async Task SayHelloAsync()
        {
            Console.WriteLine("Hello");
            await Task.CompletedTask;
        }
    }
}