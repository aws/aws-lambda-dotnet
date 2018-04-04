# ASP.NET Core Web API Serverless Application

This project shows how to run an ASP.NET Core Web Application as a serverless application. The NuGet package [Amazon.Lambda.AspNetCoreServer](https://www.nuget.org/packages/Amazon.Lambda.AspNetCoreServer) contains a Lambda function that is used to translate requests from API Gateway into the ASP.NET Core framework and then the responses from ASP.NET Core back to API Gateway.

### Adding AWS SDK for .NET ###

To integrate the AWS SDK for .NET with the dependency injection system built into ASP.NET Core add the NuGet 
package [AWSSDK.Extensions.NETCore.Setup](https://www.nuget.org/packages/AWSSDK.Extensions.NETCore.Setup/). Then in 
the `ConfigureServices` method  in `Startup.cs` file register the AWS service with the `IServiceCollection`.

```csharp
public void ConfigureServices(IServiceCollection services)
{
    services.AddMvc();

    // Add S3 to the ASP.NET Core dependency injection framework.
    services.AddAWSService<Amazon.S3.IAmazonS3>();
}
```

### Project Files ###

* serverless.template - an AWS CloudFormation Serverless Application Model template file for declaring your Serverless functions and other AWS resources
* aws-lambda-tools-defaults.json - default argument settings for use with Visual Studio and command line deployment tools for AWS
* LambdaEntryPoint.cs - class that derives from **Amazon.Lambda.AspNetCoreServer.APIGatewayProxyFunction**. The code in this file bootstraps the ASP.NET Core hosting framework. The Lambda function is defined in the base class.
* LocalEntryPoint.cs - for local development this contains the executable Main function which bootstraps the ASP.NET Core hosting framework with Kestrel, as for typical ASP.NET Core applications.
* Startup.cs - usual ASP.NET Core Startup class used to configure the services ASP.NET Core will use.

The generated project contains a Serverless template which declares 2 Lambda functions. The first function is configured using an 
API Gateway proxy which acts like a wildcard passing all requests to the Lambda function except for requests to the root path which the 
proxy doesn't support. The second declared function handles request to the root path.

## Here are some steps to follow from Visual Studio:

To deploy your Serverless application, right click the project in Solution Explorer and select *Publish to AWS Lambda*.

To view your deployed application open the Stack View window by double-clicking the stack name shown beneath the AWS CloudFormation node in the AWS Explorer tree. The Stack View also displays the root URL to your published application.

## Here are some steps to follow to get started from the command line:

Once you have edited your template and code you can use the following command lines to deploy your application from the command line (these examples assume the project name is *BlueprintBaseName*):

Restore dependencies
```
    cd "BlueprintBaseName"
    dotnet restore
```

Execute unit tests
```
    cd "BlueprintBaseName/test/BlueprintBaseName.Tests"
    dotnet test
```

Deploy application
```
    cd "BlueprintBaseName/src/BlueprintBaseName"
    dotnet lambda deploy-serverless
```
