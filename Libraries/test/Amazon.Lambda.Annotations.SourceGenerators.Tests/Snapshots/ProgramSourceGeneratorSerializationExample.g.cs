using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Amazon.Lambda.Core;

namespace TestExecutableServerlessApp;

public class GeneratedProgram
{
    public static async Task Main(string[] args)
    {

        switch (Environment.GetEnvironmentVariable("ANNOTATIONS_HANDLER"))
        {
            case "GetPerson":
                Func<Amazon.Lambda.APIGatewayEvents.APIGatewayProxyRequest, Amazon.Lambda.Core.ILambdaContext, System.IO.Stream> getperson_handler = new TestExecutableServerlessApp.SourceGenerationSerializationExample_GetPerson_Generated().GetPerson;
                await Amazon.Lambda.RuntimeSupport.LambdaBootstrapBuilder.Create(getperson_handler, new Amazon.Lambda.Serialization.SystemTextJson.SourceGeneratorLambdaJsonSerializer<TestExecutableServerlessApp.HttpApiJsonSerializerContext>()).Build().RunAsync();
                break;

        }
    }
}