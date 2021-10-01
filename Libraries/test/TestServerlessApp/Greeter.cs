using System;
using System.Threading.Tasks;
using Amazon.Lambda.Annotations;

namespace TestServerlessApp
{
    public class Greeter
    {
        [LambdaFunction]
        public void SayHello()
        {
            Console.WriteLine("Hello");
        }

        [LambdaFunction]
        public Task SayHelloAsync()
        {
            Console.WriteLine("Hello");
            return Task.CompletedTask;
        }
    }
}