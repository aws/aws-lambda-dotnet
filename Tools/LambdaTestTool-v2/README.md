# AWS Lambda Test Tool

## Overview
The AWS Lambda Test Tool provides local testing capabilities for .NET Lambda functions with support for both Lambda emulation and API Gateway emulation. This tool allows developers to test their Lambda functions locally in three different modes:

1. Lambda Emulator Mode
2. API Gateway Emulator Mode
3. Combined Mode (both emulators)

![img.png](img.png)

## Comparison with Previous Test Tool

The AWS Lambda Test Tool is an evolution of the previous AWS .NET Mock Lambda Test Tool, with several key improvements:

### New Features
- **API Gateway Emulation**: Direct support for testing API Gateway integrations locally
- New flow for loading Lambda functions that mimic's closer to the Lambda service. This solves many of the issues with the older tool when it came to loading dependencies.
- Ability to have multiple Lambda functions use the same instance of the test tool.
- UI refresh
- [Support for integration with .NET Aspire](https://github.com/aws/integrations-on-dotnet-aspire-for-aws/issues/17)

# AWS Lambda Test Tool

- [Overview](#overview)
- [Comparison with Previous Test Tool](#comparison-with-previous-test-tool)
    - [New Features](#new-features)
- [Getting help](#getting-help)
- [Installing](#installing)
- [Running the Test Tool](#running-the-test-tool)
    - [Lambda Emulator Mode](#lambda-emulator-mode)
    - [API Gateway Emulator Mode](#api-gateway-emulator-mode)
        - [Required Configuration](#required-configuration)
    - [Combined Mode](#combined-mode)
- [Command Line Options](#command-line-options)
- [API Gateway Configuration](#api-gateway-configuration)
    - [Single Route Configuration](#single-route-configuration)
    - [Multiple Routes Configuration](#multiple-routes-configuration)
- [Example Lambda Function Setup](#example-lambda-function-setup)
    - [1. Lambda Function Code](#1-lambda-function-code)
    - [2. Configuration Files](#2-configuration-files)
    - [3. AWS_LAMBDA_RUNTIME_API](#3-aws_lambda_runtime_api)
    - [4. API Gateway Configuration](#4-api-gateway-configuration)
    - [5. Testing the Function](#5-testing-the-function)


## Getting help

This tool is currently in preview and there are some known limitations. For questions and problems please open a GitHub issue in this repository.

## .NET Aspire integration
The easiest way to get started using the features of the new test tool is with .NET Aspire. The integration takes care of installing the tool and provides .NET Aspire extension methods for configuring your Lambda functions and API Gateway emulator in the .NET Aspire AppHost. It avoids all steps list below for installing the tooling and setting up environment variables.

Check out the following tracker issue for information on the .NET Aspire integration and steps for getting started. https://github.com/aws/integrations-on-dotnet-aspire-for-aws/issues/17

## Installing

The tool is distributed as .NET Global Tools via the NuGet packages. To install the tool execute the following command:

```
dotnet tool install -g amazon.lambda.testtool --prerelease
```

To update the tool run the following command:

```
dotnet tool update -g amazon.lambda.testtool --prerelease
```

## Running the Test Tool

### Lambda Emulator Mode
Use this mode when you want to test Lambda functions directly without API Gateway integration.

```
# Start Lambda emulator on port 5050
dotnet lambda-test-tool start --lambda-emulator-port 5050
```

### API Gateway Emulator Mode
Use this mode when you want to test Lambda functions through API Gateway endpoints. This mode requires additional configuration through environment variables.

```
# Start API Gateway emulator on port 5051 in REST mode
dotnet lambda-test-tool start \
    --api-gateway-emulator-port 5051 \
    --api-gateway-emulator-mode Rest

```
#### Required Configuration
When running via command line, you must set the environment variable for API Gateway route configuration:

Linux/macOS:
```bash
export APIGATEWAY_EMULATOR_ROUTE_CONFIG='{"LambdaResourceName":"AddLambdaFunction","HttpMethod":"Get","Path":"/add/{x}/{y}"}'
```

Windows (Command Prompt):

```
set APIGATEWAY_EMULATOR_ROUTE_CONFIG={"LambdaResourceName":"AddLambdaFunction","HttpMethod":"Get","Path":"/add/{x}/{y}"}
```

Windows (PowerShell):

```
$env:APIGATEWAY_EMULATOR_ROUTE_CONFIG='{"LambdaResourceName":"AddLambdaFunction","HttpMethod":"Get","Path":"/add/{x}/{y}"}'
```

### Combined Mode
Use this mode when you want to run both Lambda and API Gateway emulators simultaneously.

```
# Start both emulators
dotnet lambda-test-tool start \
    --lambda-emulator-port 5050 \
    --api-gateway-emulator-port 5051 \
    --api-gateway-emulator-mode Rest
```

## Command Line Options
| Option | Description | Required For |
|--------|-------------|--------------|
| `--lambda-emulator-port` | Port for Lambda emulator | Lambda Mode |
| `--lambda-emulator-host` | Host for Lambda emulator | Lambda Mode |
| `--api-gateway-emulator-port` | Port for API Gateway | API Gateway Mode |
| `--api-gateway-emulator-mode` | API Gateway mode (Rest/HttpV1/HttpV2) | API Gateway Mode |
| `--no-launch-window` | Disable auto-launching web interface | Optional |


## API Gateway Configuration
When using API Gateway mode, you need to configure the route mapping using the APIGATEWAY_EMULATOR_ROUTE_CONFIG environment variable. This can be a single route or an array of routes:

### Single Route Configuration
```
{
    "LambdaResourceName": "AddLambdaFunction",
    "HttpMethod": "Get",
    "Path": "/add/{x}/{y}"
}
```

### Multiple Routes Configuration

```
[
    {
        "LambdaResourceName": "AddLambdaFunction",
        "HttpMethod": "Get",
        "Path": "/add/{x}/{y}"
    },
    {
        "LambdaResourceName": "SubtractLambdaFunction",
        "HttpMethod": "Get",
        "Path": "/minus/{x}/{y}"
    }
]

```

## Example Lambda Function Setup

Here's a simple Lambda function that adds two numbers together.

### 1. Lambda Function Code
This can be implemented in two ways:

#### Option 1: Using Top-Level Statements


```csharp
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

var Add = (APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context) =>
{
    // Parse x and y from the path parameters
    var x = int.Parse(request.PathParameters["x"]);
    var y = int.Parse(request.PathParameters["y"]);
    return (x + y).ToString();
};

await LambdaBootstrapBuilder.Create(Add, new CamelCaseLambdaJsonSerializer())
    .Build()
    .RunAsync();

```

Configure the Lambda function to use the test tool:

**Properties/launchSettings.json**
```
{
  "profiles": {
    "AspireTestFunction": {
      "commandName": "Project",
      "environmentVariables": {
        "AWS_LAMBDA_RUNTIME_API": "localhost:5050/AddLambdaFunction"
      }
    }
  }
}
```

#### Option 2: Using Class Library
```
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;

namespace MyLambdaFunction;

public class Function
{
    public int Add(APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context)
    {
        var x = int.Parse(request.PathParameters["x"]);
        var y = int.Parse(request.PathParameters["y"]);
        return x + y;
    }
}
```

Configure the Lambda function to use the test tool:

**Properties/launchSettings.json**
```
{
  "profiles": {
    "LambdaRuntimeClient_FunctionHandler": {
      "workingDirectory": ".\\bin\\$(Configuration)\\net8.0",
      "commandName": "Executable",
      "commandLineArgs": "exec --depsfile ./MyLambdaFunction.deps.json  --runtimeconfig ./MyLambdaFunction.runtimeconfig.json %USERPROFILE%/.dotnet/tools/.store/amazon.lambda.testtool/0.0.2-preview/amazon.lambda.testtool/0.0.2-preview/content/Amazon.Lambda.RuntimeSupport/net8.0/Amazon.Lambda.RuntimeSupport.dll MyLambdaFunction::MyLambdaFunction.Function::Add",
      "executablePath": "dotnet",
      "environmentVariables": {
        "AWS_LAMBDA_RUNTIME_API": "localhost:5050/AddLambdaFunction"
      }
    }
  }
}
```

There are three variables you may need to replace:

1. The test tool version `0.0.2-preview` in the above path to the `Amazon.Lambda.RuntimeSupport.dll` should be updated to the current test tool version.
2. The .net version `net8.0` should be the same version that your lambda project is using.
3. The function hanadler `MyLambdaFunction::MyLambdaFunction.Function::Add` needs to be in the format of `<project_name>::<namespace>.<class>::<method_name>`

3. ### AWS_LAMBDA_RUNTIME_API

The `AWS_LAMBDA_RUNTIME_API` environment variable tells the Lambda function where to find the Lambda runtime API endpoint. It has the following format:

`host:port/functionName`


The host and port should match the port that the lambda emulator is running on.
In this example we will be running the lambda runtime api emulator on `localhost` on port `5050` and our function name will be `AddLambdaFunction`.

### 4. API Gateway Configuration
To expose this Lambda function through API Gateway, set the APIGATEWAY_EMULATOR_ROUTE_CONFIG:

```
{
    "LambdaResourceName": "AddLambdaFunction",
    "HttpMethod": "GET",
    "Path": "/add/{x}/{y}"
}
```

### 5. Testing the Function
1. Start the test tool with both Lambda and API Gateway emulators:
```
dotnet lambda-test-tool start \
    --lambda-emulator-port 5050 \
    --api-gateway-emulator-port 5051 \
    --api-gateway-emulator-mode HTTPV2

```
2. Send a test request:
```
curl -X GET "http://localhost:5051/add/5/3" -H "Content-Type: application/json" -d '"hello world"'
```

Expected response:
```
8
```
