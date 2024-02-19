# AWS Lambda Native AOT Serverless Project

This starter project consists of:
* serverless.template - an AWS CloudFormation Serverless Application Model template file for declaring your Serverless functions and other AWS resources.
* Function.cs - contains a class with a `Main` method that starts the bootstrap and a single function handler method.
* Startup.cs - When using dependency injection the services can be registered in the ConfigureServices of this type.
* aws-lambda-tools-defaults.json - default argument settings for use with Visual Studio and command line deployment tools for AWS.

You may also have a test project depending on the options selected.

This project uses the [Amazon.Lambda.Annotations](https://www.nuget.org/packages/Amazon.Lambda.Annotations) library
that uses .NET attributes to generate the boiler plate code needed for Lambda functions. It will also synchronize the CloudFormation template
with the .NET methods marked with the LambdaAttribute.

For Native AOT Lambda functions the project must be deployed as an executable and bootstrap the Lambda runtime client. Lambda Annotations
will automatically generate the `Main` by using the `GenerateMain` property of `LambdaGlobalProperties` attribute to true. The generated
`Main` method uses the environment variable `ANNOTATIONS_HANDLER` to switch to the correct function handler for the executable. Lambda
Annotations will automatically set the `ANNOTATIONS_HANDLER` environment variable when synchronizing the CloudFormation template. If deploying
the project with a tool besides CloudFormation then the `ANNOTATIONS_HANDLER` environment variable must be manually set to the .NET method name 
that should be invoked.

For more information about Amazon.Lambda.Annotations library visit the docs in GitHub for the library. https://github.com/aws/aws-lambda-dotnet/tree/master/Libraries/src/Amazon.Lambda.Annotations

## Native AOT

Native AOT is a feature that compiles .NET assemblies into a single native executable. By using the native executable the .NET runtime 
is not required to be installed on the target platform. Native AOT can significantly improve Lambda cold starts for .NET Lambda functions. 
This project enables Native AOT by setting the .NET `PublishAot` property in the .NET project file to `true`. The `StripSymbols` property is also
set to `true` to strip debugging symbols from the deployed executable to reduce the executable's size.

### Building Native AOT

When publishing with Native AOT the build OS and Architecture must match the target platform that the application will run. For AWS Lambda that target
platform is Amazon Linux 2023. The AWS tooling for Lambda like the AWS Toolkit for Visual Studio, .NET Global Tool Amazon.Lambda.Tools and SAM CLI will 
perform a container build using a .NET 8 Amazon Linux 2023 build image when `PublishAot` is set to `true`. This means **docker is a requirement**
when packaging .NET Native AOT Lambda functions on non-Amazon Linux 2023 build environments. To install docker go to https://www.docker.com/.

### Trimming

As part of the Native AOT compilation, .NET assemblies will be trimmed removing types and methods that the compiler does not find a reference to. This is important
to keep the native executable size small. When types are used through reflection this can go undetected by the compiler causing necessary types and methods to
be removed. When testing Native AOT Lambda functions in Lambda if a runtime error occurs about missing types or methods the most likely solution will
be to remove references to trim-unsafe code or configure [trimming options](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/trimming-options?pivots=dotnet-7-0).
This sample defaults to partial TrimMode because currently the AWS SDK for .NET does not support trimming. This will result in a larger executable size, and still does not
guarantee runtime trimming errors won't be hit.

For information about trimming see the documentation: <https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/trim-self-contained>

## Docker requirement

Docker is required to be installed and running when building .NET Native AOT Lambda functions on any platform besides Amazon Linux 2023. Information on how acquire Docker can be found here: https://docs.docker.com/get-docker/

## Here are some steps to follow from Visual Studio:

To deploy your function to AWS Lambda, right click the project in Solution Explorer and select *Publish to AWS Lambda*.

To view your deployed function open its Function View window by double-clicking the function name shown beneath the AWS Lambda node in the AWS Explorer tree.

To perform testing against your deployed function use the Test Invoke tab in the opened Function View window.

To configure event sources for your deployed function, for example to have your function invoked when an object is created in an Amazon S3 bucket, use the Event Sources tab in the opened Function View window.

To update the runtime configuration of your deployed function use the Configuration tab in the opened Function View window.

To view execution logs of invocations of your function use the Logs tab in the opened Function View window.

## Here are some steps to follow to get started from the command line:

Once you have edited your template and code you can deploy your application using the [Amazon.Lambda.Tools Global Tool](https://github.com/aws/aws-extensions-for-dotnet-cli#aws-lambda-amazonlambdatools) from the command line.  Version 5.6.0
or later is required to deploy this project.

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

Deploy function to AWS Lambda
```
    cd "BlueprintBaseName.1/src/BlueprintBaseName.1"
    dotnet lambda deploy-function
```