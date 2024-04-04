# AWS Lambda Custom Runtime Function Project

This starter project consists of:
* Function.cs - contains a class with a Main method that starts the bootstrap, and a single function handler method
* aws-lambda-tools-defaults.json - default argument settings for use with Visual Studio and command line deployment tools for AWS

You may also have a test project depending on the options selected.

The generated Main method is the entry point for the function's process.  The main method wraps the function handler in a wrapper that the bootstrap can work with.  Then it instantiates the bootstrap and sets it up to call the function handler each time the AWS Lambda function is invoked.  After the set up the bootstrap is started.

The generated function handler is a simple method accepting a string argument that returns the uppercase equivalent of the input string. Replace the body of this method, and parameters, to suit your needs. 

## Here are some steps to follow from Visual Studio:

To deploy your function to AWS Lambda, right click the project in Solution Explorer and select *Publish to AWS Lambda*.

To view your deployed function open its Function View window by double-clicking the function name shown beneath the AWS Lambda node in the AWS Explorer tree.

To perform testing against your deployed function use the Test Invoke tab in the opened Function View window.

To configure event sources for your deployed function, for example to have your function invoked when an object is created in an Amazon S3 bucket, use the Event Sources tab in the opened Function View window.

To update the runtime configuration of your deployed function use the Configuration tab in the opened Function View window.

To view execution logs of invocations of your function use the Logs tab in the opened Function View window.

## Here are some steps to follow to get started from the command line:

Once you have edited your template and code you can deploy your application using the [Amazon.Lambda.Tools Global Tool](https://github.com/aws/aws-extensions-for-dotnet-cli#aws-lambda-amazonlambdatools) from the command line.  Version 3.1.4
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

## Improve Cold Start

In the csproj file the PublishTrimmed and PublishReadyToRun properties have been enable to optimize the package bundle to improve cold start performance.

PublishTrimmed tells the compiler to remove code from the assemblies that is not being used to reduce the deployment bundle size. This requires additional
testing to make sure the .NET compiler does not remove any code that is actually used. For further information about trimming
check out the .NET documentation: https://docs.microsoft.com/en-us/dotnet/core/deploying/trimming/trimming-options

PublishReadyToRun tells the compiler to compile the .NET assemblies for a specific runtime environment. For Lambda's case that means Linux x64 or arm64.
This reduces the work the JIT compiler does at runtime to compile for the specific runtime environment and helps reduce cold start time.

