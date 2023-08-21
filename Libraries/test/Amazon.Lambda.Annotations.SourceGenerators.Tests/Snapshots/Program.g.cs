using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Amazon.Lambda.Core;

namespace TestServerlessApp.Sub1;

public class Program
{
    private static async Task Main(string[] args)
    {
        Func<string, string> toupper = Functions_ToUpper_Generated.ToUpper;
        await Amazon.Lambda.RuntimeSupport.LambdaBootstrapBuilder.Create(toupper, new Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer()).Build().RunAsync();
    }
}