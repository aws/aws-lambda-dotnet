// C# file-based Lambda functions can be deployed to Lambda using the .NET Tool Amazon.Lambda.Tools.
//
// Command to install Amazon.Lambda.Tools
//   dotnet tool install -g Amazon.Lambda.Tools
//
// Command to deploy function
//    dotnet lambda deploy-function <lambda-function-name> BlueprintBaseName.1.cs
//
// Command to package function
//    dotnet lambda package BlueprintBaseName.1.zip BlueprintBaseName.1.cs


#:package Amazon.Lambda.Core@2.8.0
#:package Amazon.Lambda.RuntimeSupport@1.14.1
#:package Amazon.Lambda.Serialization.SystemTextJson@2.4.4

// Explicitly setting TargetFramework here is done to avoid
// having to specify it when packaging the function with Amazon.Lambda.Tools
#:property TargetFramework=net10.0

// By default File-based C# apps publish as Native AOT. When packaging Lambda function
// unless the host machine is Amazon Linux a container build will be required. 
// Amazon.Lambda.Tools will automatically initate a container build if docker is installed.
// Native AOT also requires the code and dependencies be Native AOT compatible.
//
// To disable Native AOT uncomment the following line to add the .NET build directive 
// that disables Native AOT.
//#:property PublishAot=false

using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using System.Text.Json.Serialization;

// The function handler that will be called for each Lambda event
var handler = (string input, ILambdaContext context) =>
{
    return input.ToUpper();
};

// Build the Lambda runtime client passing in the handler to call for each
// event and the JSON serializer to use for translating Lambda JSON documents
// to .NET types.
await LambdaBootstrapBuilder.Create(handler, new SourceGeneratorLambdaJsonSerializer<LambdaSerializerContext>())
        .Build()
        .RunAsync();


// Since Native AOT is used by default with C# file-based Lambda functions the source generator
// based Lambda serializer is used. Ensure the input type and return type used by the function 
// handler are registered on the JsonSerializerContext using the JsonSerializable attribute.
[JsonSerializable(typeof(string))]
public partial class LambdaSerializerContext : JsonSerializerContext
{
}