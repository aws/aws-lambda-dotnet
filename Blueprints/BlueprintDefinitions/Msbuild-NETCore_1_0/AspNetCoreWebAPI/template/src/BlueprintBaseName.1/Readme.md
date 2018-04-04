# ASP.NET Core Web API Serverless Application (Preview)

This project shows how to run an ASP.NET Core Web API project as an AWS Lambda exposed through Amazon API Gateway. The NuGet package [Amazon.Lambda.AspNetCoreServer](https://www.nuget.org/packages/Amazon.Lambda.AspNetCoreServer) contains a Lambda function that is used to translate requests from API Gateway into the ASP.NET Core framework and then the responses from ASP.NET Core back to API Gateway.

The project starts with two Web API controllers. The first is the example ValuesController that is created by default for new ASP.NET Core Web API projects. The second is S3ProxyController which uses the AWS SDK for .NET to proxy requests for an Amazon S3 bucket.

### Logging ###

The NuGet package [Amazon.Lambda.Logging.AspNetCore](https://www.nuget.org/packages/Amazon.Lambda.Logging.AspNetCore/) is added to the project to integrate the ASP.NET Core ILogger logging framework into the Amazon CloudWatch Logs built into Lambda functions. By adding this package and the following code in the Startup.cs file controllers that write to the ILogger will have those log messages be written to CloudWatch Logs

```csharp
public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
{
    loggerFactory.AddLambdaLogger(Configuration.GetLambdaLoggerOptions());
    ...
}
```

You can also control what log messages get written to CloudWatch Logs with your configuration. For example this appsettings.json file configures log messages of level informational and above coming from classes under the Microsoft namespace to be sent to CloudWatch Logs and for all other log messages, those at debug level and above are written.
```json
{
  "Lambda.Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft": "Information"
    }
  }
}
```
Check the [GitHub](https://github.com/aws/aws-lambda-dotnet/tree/master/Libraries/src/Amazon.Lambda.Logging.AspNetCore) repository for more information on this package.

### Configuring AWS SDK for .NET ###

To integrate the AWS SDK for .NET with the dependency injection system built into ASP.NET Core the NuGet package [AWSSDK.Extensions.NETCore.Setup](https://www.nuget.org/packages/AWSSDK.Extensions.NETCore.Setup/) is referenced. In the Startup.cs file the Amazon S3 client is added to the dependency injection framework. The S3ProxyController will get its S3 service client from there.

```csharp
public void ConfigureServices(IServiceCollection services)
{
    services.AddMvc();

    // Pull in any SDK configuration from Configuration object
    services.AddDefaultAWSOptions(Configuration.GetAWSOptions());

    // Add S3 to the ASP.NET Core dependency injection framework.
    services.AddAWSService<Amazon.S3.IAmazonS3>();
}
```

### Project Files ###

* serverless.template - an AWS CloudFormation Serverless Application Model template file for declaring your Serverless functions and other AWS resources
* aws-lambda-tools-defaults.json - default argument settings for use with Visual Studio and command line deployment tools for AWS
* LambdaEntryPoint.cs - class that derives from **Amazon.Lambda.Logging.AspNetCore.APIGatewayProxyFunction**. The code in this file bootstraps the ASP.NET Core hosting framework. The Lambda function is defined in the base class.
* LocalEntryPoint.cs - for local development this contains the executable Main function which bootstraps the ASP.NET Core hosting framework with Kestrel, as for typical ASP.NET Core applications.
* Startup.cs - usual ASP.NET Core Startup class used to configure the services ASP.NET Core will use.
* web.config - used for local development.
* Controllers\S3ProxyController - Web API controller for proxying an S3 bucket
* Controllers\ValuesController - example Web API controller

You may also have a test project depending on the options selected.

The generated project contains a Serverless template declaration for a single AWS Lambda function that will be exposed through Amazon API Gateway as a HTTP *Get* operation. Edit the template to customize the function or add more functions and other resources needed by your application, and edit the function code in Function.cs. You can then deploy your Serverless application.

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
