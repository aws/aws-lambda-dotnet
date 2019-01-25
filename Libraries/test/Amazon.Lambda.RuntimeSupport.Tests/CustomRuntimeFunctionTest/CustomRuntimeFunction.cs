using Amazon.Lambda.RuntimeSupport;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace CustomRuntimeFunctionTest
{
    class CustomRuntimeFunction
    {
        static async Task Main(string[] args)
        {
            using (var bootstrap = new LambdaBootstrap(ToUpperAsync, InitializeAsync))
            {
                await bootstrap.RunAsync();
            }
        }

        static Task<bool> InitializeAsync()
        {
            Console.WriteLine("Initializing...");
            return Task.FromResult(true);
        }

        static async Task<Stream> ToUpperAsync(InvocationRequest invocation)
        {
            using (var sr = new StreamReader(invocation.InputStream))
            {
                Console.WriteLine("Invoking...");
                var result = (await sr.ReadToEndAsync()).ToUpper();
                Console.WriteLine($"returning \"{result}\"");
                return new MemoryStream(Encoding.UTF8.GetBytes(result));
            }
        }
    }
}
