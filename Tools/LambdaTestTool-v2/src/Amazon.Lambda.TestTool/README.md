# AWS Lambda Test Tool

## Overview
The AWS Lambda Test Tool provides local testing capabilities for .NET Lambda functions with support for both Lambda emulation and API Gateway emulation. This tool allows developers to test their Lambda functions locally in three different modes:

1. Lambda Emulator Mode
2. API Gateway Emulator Mode
3. Combined Mode (both emulators)

## Comparison with Previous Test Tool

The AWS Lambda Test Tool is an evolution of the previous AWS .NET Mock Lambda Test Tool, with several key improvements:

### New Features
- **API Gateway Emulation**: Direct support for testing API Gateway integrations locally

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
- [Running the Test Tool Project via an IDE](#running-the-test-tool-project-via-an-ide)
- [Example Lambda Function Setup](#example-lambda-function-setup)
  - [1. Lambda Function Code](#1-lambda-function-code)
  - [2. Configuration Files](#2-configuration-files)
  - [3. API Gateway Configuration](#3-api-gateway-configuration)
  - [4. Testing the Function](#4-testing-the-function)


## Getting help

This tool is currently in preview and there are some known limitations. For questions and problems please open a GitHub issue in this repository.

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

## Running the Test Tool Project via an IDE

Download this repository and create/modify the launch settings `Properties/launchSettings.json` to startup the test tool

```json
{
    "profiles": {
        "Lambda Test Tool": {
          "commandName": "Project",
            "commandLineArgs": "start --lambda-emulator-port 5050 --api-gateway-emulator-port 5051 --api-gateway-emulator-mode Rest",
            "environmentVariables": {
                "APIGATEWAY_EMULATOR_ROUTE_CONFIG": {
                    "LambdaResourceName": "AddLambdaFunction",
                    "HttpMethod": "Get",
                    "Path": "/add/{x}/{y}"
                }
            }
        }
    }
}

```

## Example Lambda Function Setup

Here's a simple Lambda function that adds two numbers together. This can be implemented in two ways:

### 1. Lambda Function Code
Here's a simple Lambda function that adds two numbers together:

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

### 2. Configuration Files
**Properties/launchSettings.json**
Configure the Lambda function to use the test tool:

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

**aws-lambda-tools-defaults.json**

For top-level statements, your `aws-lambda-tools-defaults.json` should be:
```
{
    "profile": "default",
    "region": "us-west-2",
    "configuration": "Release",
    "function-runtime": "dotnet8",
    "function-memory-size": 512,
    "function-timeout": 30,
    "function-handler": "AddLambdaFunction"
}
```

For the class library approach, your `aws-lambda-tools-defaults.json` should be:

```
{
    "profile": "default",
    "region": "us-west-2",
    "configuration": "Release",
    "function-runtime": "dotnet8",
    "function-memory-size": 512,
    "function-timeout": 30,
    "function-handler": "MyLambdaFunction::MyLambdaFunction.Function::Add"
}

```


### 3. API Gateway Configuration
To expose this Lambda function through API Gateway, set the APIGATEWAY_EMULATOR_ROUTE_CONFIG:

```
{
    "LambdaResourceName": "AddLambdaFunction",
    "HttpMethod": "GET",
    "Path": "/add/{x}/{y}"
}
```

### 4. Testing the Function
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
