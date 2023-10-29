using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Amazon.Lambda.Core;

namespace TestServerlessApp.Sub1;

public class GeneratedProgram
{
    private static async Task Main(string[] args)
    {

        switch (Environment.GetEnvironmentVariable("ANNOTATIONS_HANDLER"))
        {
            case "ToLower":
                Func<string, string> tolower_handler = new TestServerlessApp.Sub1.FunctionsZipOutput_ToLower_Generated().ToLower;
                await Amazon.Lambda.RuntimeSupport.LambdaBootstrapBuilder.Create(tolower_handler, new Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer()).Build().RunAsync();
                break;

        }
    }
}