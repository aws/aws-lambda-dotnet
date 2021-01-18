# AWS Lambda Custom Runtime Function Project

This starter project consists of:
* Function.fs - contains a main function that starts the bootstrap, and a single function handler
* aws-lambda-tools-defaults.json - default argument settings for use with Visual Studio and command line deployment tools for AWS

You may also have a test project depending on the options selected.

The generated main function is the entry point for the function's process.  The main function wraps the function handler in a wrapper that the bootstrap can work with.  Then it instantiates the bootstrap and sets it up to call the function handler each time the AWS Lambda function is invoked.  After the set up the bootstrap is started.

The generated function handler is a simple function accepting a string argument that returns the uppercase equivalent of the input string. Replace the body of this function, and parameters, to suit your needs. 

## Here are some steps to follow from Visual Studio:

(Deploying and invoking custom runtime functions is not yet available in Visual Studio)

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
    cd "BlueprintBaseName/test/BlueprintBaseName.Tests"
    dotnet test
```

Deploy function to AWS Lambda
```
    cd "BlueprintBaseName/src/BlueprintBaseName"
    dotnet lambda deploy-function
```

## Using AWS .NET Mock Lambda Test Tool

The AWS .NET Mock Lambda Test Tool can be used with .NET Lambda custom runtimes. When the test tool is used for custom runtime the project
is executed similar to a Lambda managed runtime and the main method is not called. The test tool uses the `function-handler` field in
the `aws-lambda-tools-defaults.json` file to figure out what code to call when executing a function in it.

To configure the test tool for custom runtimes follow these steps:

* Ensure the `function-handler` is set in the `aws-lambda-tools-defaults.json` for the method to call.
* There is a JSON serializer registered for test tool to using the `LambdaSerializer` assembly attribute.
  * `[<assembly: LambdaSerializer(typeof<Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer>)>]`
* Ensure the test tool is installed from NuGet. Below is an example for installing the .NET 5.0 version.
  * `dotnet tool install -g Amazon.Lambda.TestTool-5.0` or to update `dotnet tool update -g Amazon.Lambda.TestTool-5.0`
* For Visual Studio edit or add the `Properties\launchSettings.json` to register the test tool as a debug target.
```json
{
  "profiles": {
    "Mock Lambda Test Tool": {
      "commandName": "Executable",
      "commandLineArgs": "--port 5050",
      "workingDirectory": ".\\bin\\$(Configuration)\\net5.0",
      "executablePath": "%USERPROFILE%\\.dotnet\\tools\\dotnet-lambda-test-tool-5.0.exe"
    }
  }
}
```
