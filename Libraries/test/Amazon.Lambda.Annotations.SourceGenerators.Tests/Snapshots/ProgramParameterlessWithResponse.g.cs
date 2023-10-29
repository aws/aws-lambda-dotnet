using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Amazon.Lambda.Core;

namespace TestServerlessApp;

public class GeneratedProgram
{
    private static async Task Main(string[] args)
    {

        switch (Environment.GetEnvironmentVariable("ANNOTATIONS_HANDLER"))
        {
            case "NoParameter":
                Func<Stream, string> noparameter_handler = new TestServerlessApp.ParameterlessMethodWithResponse_NoParameter_Generated().NoParameter;
                await Amazon.Lambda.RuntimeSupport.LambdaBootstrapBuilder.Create(noparameter_handler, new Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer()).Build().RunAsync();
                break;

        }
    }
}