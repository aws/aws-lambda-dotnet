using System.IO;

using Amazon.Lambda.AspNetCoreServer;
using Microsoft.AspNetCore.Hosting;

namespace TestWebApp
{
    public class LambdaFunction : APIGatewayProxyFunction
    {
        public const string BinaryContentType = "application/octet-stream";

        protected override void Init(IWebHostBuilder builder)
        {
            builder
                .UseApiGateway()
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseStartup<Startup>();
        }
    }
}
