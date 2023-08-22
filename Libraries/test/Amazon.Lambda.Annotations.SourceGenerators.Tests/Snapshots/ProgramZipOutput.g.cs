using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Amazon.Lambda.Core;

namespace TestServerlessApp.Sub1;

public class GeneratedProgram
{
    private static async Task Main(string[] args)
    {

        switch (Environment.GetEnvironmentVariable("ANNOTATIONS_HANDLER"))
        {
            case "ToLower":
                Func<string, string> tolower = FunctionsZipOutput_ToLower_Generated.ToLower;
                await Amazon.Lambda.RuntimeSupport.LambdaBootstrapBuilder.Create(tolower, new Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer()).Build().RunAsync();
                break;

        }
    }
}