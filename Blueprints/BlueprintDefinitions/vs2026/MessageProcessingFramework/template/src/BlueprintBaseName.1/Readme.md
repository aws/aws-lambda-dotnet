# AWS Message Processing Framework for .NET Template

This starter project consists of:
* Functions.cs - class file containing a class with two Lambda functions, one for sending messages and one for processing them
* Startup.cs - default argument settings for use with Visual Studio and command line deployment tools for AWS
* GreetingMessage.cs - Represents a single message
* GreetingMessageHandler.cs - Business logic for handling messages
* serverless.template - A CloudFormation template to deploy both functions. It also creates a new SQS queue that the functions will send to and receive messages from. 

You may also have a test project depending on the options selected.

## About the Framework

The AWS Message Processing Framework for .NET is an AWS-native framework that simplifies the development of .NET message processing applications that use AWS services, such as Amazon Simple Queue Service (SQS), Amazon Simple Notification Service (SNS), and Amazon EventBridge. 
The framework reduces the amount of boiler-plate code developers need to write, allowing you to focus on your business logic when publishing and consuming messages.

* [Readme](https://github.com/awslabs/aws-dotnet-messaging/blob/main/README.md)
* [Developer Guide](https://docs.aws.amazon.com/sdk-for-net/v3/developer-guide/msg-proc-fw.html)
* [API Reference](https://awslabs.github.io/aws-dotnet-messaging/api/AWS.Messaging.html)

The framework supports Open Telemetry via the [AWS.Messaging.Telemetry.OpenTelemetry](https://www.nuget.org/packages/AWS.Messaging.Telemetry.OpenTelemetry/) package. Refer to its [README](https://github.com/awslabs/aws-dotnet-messaging/blob/main/src/AWS.Messaging.Telemetry.OpenTelemetry/README.md) to enable instrumentation.

## Local Testing Guide

### Prerequisites
The functions can be tested with the [Lambda Test Tool](https://github.com/aws/aws-lambda-dotnet/tree/master/Tools/LambdaTestTool-v2).

1. Install the Lambda Test Tool:
```bash
dotnet tool install -g amazon.lambda.testtool
```

2. Get the Lambda Test Tool version:

```
dotnet lambda-test-tool info
```

### Setup Steps


1. Build the project

```
dotnet build
```

2. Start the Lambda Test Tool:

```
dotnet lambda-test-tool start --lambda-emulator-port 5050
```

3. Configure the project:
* Update Properties/launchSettings.json with the Lambda Test Tool version and function handler name.

###$ Example launchSettings.json

```json
{
    "profiles": {
        "Default": {
            "workingDirectory": ".\\bin\\$(Configuration)\\net8.0",
            "commandName": "Executable",
            "commandLineArgs": "exec --depsfile ./BlueprintBaseName.1.deps.json  --runtimeconfig ./BlueprintBaseName.1.runtimeconfig.json %USERPROFILE%/.dotnet/tools/.store/amazon.lambda.testtool/${VERSION}/amazon.lambda.testtool/${VERSION}/content/Amazon.Lambda.RuntimeSupport/net8.0/Amazon.Lambda.RuntimeSupport.dll BlueprintBaseName.1::BlueprintBaseName._1.Functions_Handler_Generated::Handler",
            "executablePath": "dotnet",
            "environmentVariables": {
                "AWS_LAMBDA_RUNTIME_API": "localhost:5050/MyFunction",
                "QUEUE_URL": "QUEUE_URL"
            }
        }
    }
}

```


### Running the project

### Option 1: Using Visual Studio
1. Update launchSettings.json with the correct Lambda Test Tool version
2. Run the project from Visual Studio


### Option 2: Using Command Line


```
cd bin\Debug\net8.0
$env:AWS_LAMBDA_RUNTIME_API = "localhost:5050/MyFunction"
$env:VERSION = "0.9.1" // Use the version returned from dotnet lambda-test-tool info

dotnet exec --depsfile ./BlueprintBaseName.1.deps.json --runtimeconfig ./BlueprintBaseName.1.runtimeconfig.json "$env:USERPROFILE\.dotnet\tools\.store\amazon.lambda.testtool\$env:VERSION\amazon.lambda.testtool\$env:VERSION\content\Amazon.Lambda.RuntimeSupport\net8.0\Amazon.Lambda.RuntimeSupport.dll" BlueprintBaseName.1::BlueprintBaseName._1.Functions_Handler_Generated::Handler


```

### Testing

The project includes sample payloads in 

```plaintext
.lambda-test-tool\SavedRequests
```

:

1. "Sender Sample Request" - can be used to invoke the Sender function. Note that this will send an SQS message to the queue configured in `launchSettings.json` 
    
2. "Handler Sample Request" - can be used to invoke the Handler function. This mocks the SQS message, and does not require an actual queue.


## Deploying and Testing from Visual Studio

To deploy your functions to AWS Lambda, right click the project in Solution Explorer and select *Publish to AWS Lambda* and then follow the wizard.

To view your deployed functions, open the Function View window by double-clicking the function names shown beneath the AWS Lambda node in the AWS Explorer tree.

To perform testing against your deployed functions use the Test Invoke tab in the opened Function View window.

To configure event sources for your deployed functions use the Event Sources tab in the opened Function View window.

To update the runtime configuration of your deployed functions use the Configuration tab in the opened Function View window.

To view execution logs of invocations of your functions use the Logs tab in the opened Function View window.

## Deploying and Testing from the CLI

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

Deploy the functions to AWS Lambda
```
    cd "BlueprintBaseName.1/src/BlueprintBaseName.1"
    dotnet lambda deploy-serverless
```
