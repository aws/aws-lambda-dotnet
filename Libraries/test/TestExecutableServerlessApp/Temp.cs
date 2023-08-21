namespace TestServerlessApp;

using System;
using System.Threading.Tasks;

using Amazon.Lambda.Core;

public class Temp
{
    public static async Task Handle()
    {
        Action<string, ILambdaContext> voidreturn = VoidExample_VoidReturn_Generated.VoidReturn;
        await Amazon.Lambda.RuntimeSupport.LambdaBootstrapBuilder.Create(voidreturn, new Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer()).Build().RunAsync();
    }

    public static void Handler(string message)
    {
        
    }
}