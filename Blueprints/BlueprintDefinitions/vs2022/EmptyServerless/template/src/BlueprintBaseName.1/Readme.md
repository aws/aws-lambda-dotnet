# Empty AWS Serverless Application Project

This starter project consists of:
* serverless.template - An AWS CloudFormation Serverless Application Model template file for declaring your Serverless functions and other AWS resources
* Function.cs - Class file containing the C# method mapped to the single function declared in the template file
* Startup.cs - Class file that can be used to configure services that can be injected for either the Lambda container lifetime or a single function invocation
* aws-lambda-tools-defaults.json - Default argument settings for use with Visual Studio and command line deployment tools for AWS

You may also have a test project depending on the options selected.

The generated project contains a Serverless template declaration for a single AWS Lambda function that will be exposed through Amazon API Gateway as a HTTP *Get* operation. Edit the template to customize the function or add more functions and other resources needed by your application, and edit the function code in Function.cs. You can then deploy your Serverless application.

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

## Lambda Annotations
This template uses [Lambda Annotations](https://github.com/aws/aws-lambda-dotnet/blob/master/Libraries/src/Amazon.Lambda.Annotations/README.md) to bridge the gap between the Lambda programming model and a more idiomatic .NET model.

This automatically handles reading parameters from an APIGatewayProxyRequest and returning an APIGatewayProxyResponse. 

It also generates the function resources in a JSON or YAML CloudFormation template based on your function definitions, and keeps them updated.

### Using Annotations without API Gateway
You can still use Lambda Annotations without integrating with API Gateway. For example, this Lambda function processes messages from an Amazon Simple Queue Service (Amazon SQS) queue:
```
[LambdaFunction(Policies = "AWSLambdaSQSQueueExecutionRole", MemorySize = 256, Timeout = 30)]
public async Task FunctionHandler(SQSEvent evnt, ILambdaContext context) 
{ 
    foreach(var message in evnt.Records) 
    { 
      await ProcessMessageAsync(message, context);
    }
}
```

### Reverting to not using Annotations
If you wish to use the former style of function instead of annotations, replace the Lambda function with:
```
public APIGatewayProxyResponse Get(APIGatewayProxyRequest request, ILambdaContext context)
{
    context.Logger.LogInformation("Handling the 'Get' Request");

    var response = new APIGatewayProxyResponse
    {
        StatusCode = (int)HttpStatusCode.OK,
        Body = "Hello AWS Serverless",
        Headers = new Dictionary<string, string> { { "Content-Type", "text/plain" } }
    };

    return response;
}
```

You must also replace the function resource in `serverless.template` with:
```
    "Get": {
      "Type": "AWS::Serverless::Function",
      "Properties": {
        "Handler": "<ASSEMBLY>::<TYPE>.Functions::Get",
        "Runtime": "dotnet6",
        "CodeUri": "",
        "MemorySize": 256,
        "Timeout": 30,
        "Role": null,
        "Policies": [
          "AWSLambdaBasicExecutionRole"
        ],
        "Events": {
          "RootGet": {
            "Type": "Api",
            "Properties": {
              "Path": "/",
              "Method": "GET"
            }
          }
        }
      }
    }
  }
```

You may also want to:
1. Update the generated test code to match the new `Get` Signature.
2. Remove the package reference and `using` statements related to `Amazon.Lambda.Annotations`.