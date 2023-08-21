using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Amazon.Lambda.Core;

namespace TestServerlessApp;

public class Program
{
    private static async Task Main(string[] args)
    {

        switch (Environment.GetEnvironmentVariable("HANDLER"))
        {
            case "SayHello":
                Func<Amazon.Lambda.APIGatewayEvents.APIGatewayProxyRequest, Amazon.Lambda.Core.ILambdaContext, Amazon.Lambda.APIGatewayEvents.APIGatewayProxyResponse> sayhello = Greeter_SayHello_Generated.SayHello;
                await Amazon.Lambda.RuntimeSupport.LambdaBootstrapBuilder.Create(sayhello, new Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer()).Build().RunAsync();
                break;
            case "SayHelloAsync":
                Func<Amazon.Lambda.APIGatewayEvents.APIGatewayProxyRequest, Amazon.Lambda.Core.ILambdaContext, System.Threading.Tasks.Task<Amazon.Lambda.APIGatewayEvents.APIGatewayProxyResponse>> sayhelloasync = Greeter_SayHelloAsync_Generated.SayHelloAsync;
                await Amazon.Lambda.RuntimeSupport.LambdaBootstrapBuilder.Create(sayhelloasync, new Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer()).Build().RunAsync();
                break;

        }
    }
}