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
            case "ToUpper":
                Func<string, string> toupper_handler = new TestServerlessApp.Sub1.Functions_ToUpper_Generated().ToUpper;
                await Amazon.Lambda.RuntimeSupport.LambdaBootstrapBuilder.Create(toupper_handler, new Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer()).Build().RunAsync();
                break;

        }
    }
}