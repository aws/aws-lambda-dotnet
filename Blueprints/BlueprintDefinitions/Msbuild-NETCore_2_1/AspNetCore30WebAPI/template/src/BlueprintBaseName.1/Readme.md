# ASP.NET Core 3.0 with Lambda Custom Runtime

This template packages the project as a self contained project including the .NET Core 3.0 runtime. The Amazon.Lambda.RuntimeSupport
package is used to interact with the Lambda custom runtime interface.

This starter project consists of:
* serverless.template - an AWS CloudFormation Serverless Application Model template file for declaring your Serverless functions and other AWS resources
* Program.cs - This class is the normal starting point for the ASP.NET Core application. If the AWS_LAMBDA_FUNCTION_NAME environment
variable is set then Program.cs will run in Lambda mode and configure the project to respond to Lambda events.
* LambdaEntryPoint.cs - class that derives from **Amazon.Lambda.AspNetCoreServer.APIGatewayProxyFunction**. The code in 
* aws-lambda-tools-defaults.json - default argument settings for use with Visual Studio and command line deployment tools for AWS

You may also have a test project depending on the options selected.

## Here are some steps to follow from Visual Studio:

To deploy your Serverless application, right click the project in Solution Explorer and select *Publish to AWS Lambda*.

To view your deployed application open the Stack View window by double-clicking the stack name shown beneath the AWS CloudFormation node in the AWS Explorer tree. The Stack View also displays the root URL to your published application.

## Here are some steps to follow to get started from the command line:

Once you have edited your template and code you can deploy your application using the [Amazon.Lambda.Tools Global Tool](https://github.com/aws/aws-extensions-for-dotnet-cli#aws-lambda-amazonlambdatools) from the command line.

Install Amazon.Lambda.Tools Global Tools if not already installed.
```
    dotnet tool install -g Amazon.Lambda.Tools
```

If already installed check if new version is available.
```
    dotnet tool update -g Amazon.Lambda.Tools
```

Execute unit tests
```
    cd "BlueprintBaseName.1/test/BlueprintBaseName.1.Tests"
    dotnet test
```

Deploy application
```
    cd "BlueprintBaseName.1/src/BlueprintBaseName.1"
    dotnet lambda deploy-serverless
```

## Improve Cold Start

.NET Core 3.0 has a new feature called ReadyToRun. When you compile your .NET Core 3.0 application you can enable ReadyToRun 
to prejit the .NET assemblies. This saves the .NET Core runtime from doing a lot of work during startup converting the 
assemblies to a native format. ReadyToRun must be used on the same platform as the platform that will run the .NET application. In Lambda's case
that means you have to build the Lambda package bundle in a Linux environment. To enable ReadyToRun edit the aws-lambda-tools-defaults.json
file to add /p:PublishReadyToRun=true to the msbuild-parameters parameter.

