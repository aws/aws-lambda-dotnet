using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.Json;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace CustomRuntimeFunctionTest
{
    class CustomRuntimeFunction
    {
        private const string TestFilePath = "/var/task/SubFolder/TestFile.txt";

        private static MemoryStream ResponseStream = new MemoryStream();

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

        static async Task<InvocationResponse> ToUpperAsync(InvocationRequest invocation)
        {
            using (var sr = new StreamReader(invocation.InputStream))
            {
                Console.WriteLine("Invoking...");
                var result = (await sr.ReadToEndAsync()).ToUpper();
                Console.WriteLine($"returning \"{result}\"");

                // Truncate ResponseStream so it can be reused.
                ResponseStream.SetLength(0);
                // Write the result.
                await ResponseStream.WriteAsync(Encoding.UTF8.GetBytes(result));
                // Reset the position to the beginning.
                ResponseStream.Position = 0;

                // Return an InvocationResponse that indicates that the Stream shouldn't be disposed.
                return new InvocationResponse(ResponseStream, false);
            }
        }
    }
}
