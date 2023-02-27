# AWS Lambda Function Project with Lambda Powertools

This starter project consists of:

* Function.cs - class file containing a class with a single function handler method
* aws-lambda-tools-defaults.json - default argument settings for use with Visual Studio and command line deployment tools for AWS

You may also have a test project depending on the options selected.

The generated function handler is a simple method accepting a string argument that returns the uppercase equivalent of the input string. Replace the body of this method, and parameters, to suit your needs.

## AWS Lambda Powertools for .NET

[AWS Lambda Powertools for .NET](https://awslabs.github.io/aws-lambda-powertools-dotnet/) is a developer toolkit to implement Serverless best practices and increase developer velocity.

This starter project comes with Powertools Loging, Metrics and Tracing configured through environment variables defined in the `aws-lambda-tools-defaults.json` file and annotations on methods in `Function.cs`

**Environment variables:**

* POWERTOOLS_SERVICE_NAME=PowertoolsFunction
* POWERTOOLS_LOG_LEVEL=Info
* POWERTOOLS_LOGGER_CASE=PascalCase
* POWERTOOLS_TRACER_CAPTURE_RESPONSE=true
* POWERTOOLS_TRACER_CAPTURE_ERROR=true
* POWERTOOLS_METRICS_NAMESPACE=powertools_function

References to the environment variables can be found [here]([https://](https://awslabs.github.io/aws-lambda-powertools-dotnet/references/))

**Included NuGet Packages:**

* [AWS.Lambda.Powertools.Logging](https://awslabs.github.io/aws-lambda-powertools-dotnet/core/logging/)
* [AWS.Lambda.Powertools.Metrics](https://awslabs.github.io/aws-lambda-powertools-dotnet/core/metrics/)
* [AWS.Lambda.Powertools.Tracing](https://awslabs.github.io/aws-lambda-powertools-dotnet/core/tracing/)

## Here are some steps to follow from Visual Studio

To deploy your function to AWS Lambda, right click the project in Solution Explorer and select *Publish to AWS Lambda*.

To view your deployed function open its Function View window by double-clicking the function name shown beneath the AWS Lambda node in the AWS Explorer tree.

To perform testing against your deployed function use the Test Invoke tab in the opened Function View window.

To configure event sources for your deployed function, for example to have your function invoked when an object is created in an Amazon S3 bucket, use the Event Sources tab in the opened Function View window.

To update the runtime configuration of your deployed function use the Configuration tab in the opened Function View window.

To view execution logs of invocations of your function use the Logs tab in the opened Function View window.

## Here are some steps to follow to get started from the command line

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

Deploy function to AWS Lambda

```
    cd "BlueprintBaseName.1/src/BlueprintBaseName.1"
    dotnet lambda deploy-function
```
