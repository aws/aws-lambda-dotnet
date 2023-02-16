# AWS Serverless Application Project with Lambda Powertools

This starter project consists of:
* serverless.template - an AWS CloudFormation Serverless Application Model template file for declaring your Serverless functions and other AWS resources
* Function.cs - class file containing the C# method mapped to the single function declared in the template file
* aws-lambda-tools-defaults.json - default argument settings for use with Visual Studio and command line deployment tools for AWS

You may also have a test project depending on the options selected.

The generated project contains a Serverless template declaration for a single AWS Lambda function that will be exposed through Amazon API Gateway as a HTTP *Get* operation. Edit the template to customize the function or add more functions and other resources needed by your application, and edit the function code in Function.cs. You can then deploy your Serverless application.

## Lambda Powertools

[AWS Lambda Powertools for .NET](https://awslabs.github.io/aws-lambda-powertools-dotnet/) is a suite of utilities for AWS Lambda functions to ease adopting best practices such as tracing, structured logging, custom metrics, and more.

This starter project comes with Powertools Loging, Metrics and Tracing configured in Function.cs.

Included NuGet Packages:

* [AWS.Lambda.Powertools.Logging](https://awslabs.github.io/aws-lambda-powertools-dotnet/core/logging/)
* [AWS.Lambda.Powertools.Metrics](https://awslabs.github.io/aws-lambda-powertools-dotnet/core/metrics/)
* [AWS.Lambda.Powertools.Tracing](https://awslabs.github.io/aws-lambda-powertools-dotnet/core/tracing/)

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
## Arm64

Arm64 support is provided by the AWS Graviton2 processor. For many Lambda workloads Graviton2 provides the best price performance.

If you want to run your Lambda on a Graviton2 Arm64 processor, all you need to do is replace `x86_64` with `arm64` under `"Architectures": ` in the `serverless.template` file. Then deploy as described above. 