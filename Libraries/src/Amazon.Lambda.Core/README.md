# Amazon.Lambda.Core

This package contains interfaces and classes that can be helpful when running your .NET code on the AWS Lambda platform.

## ILambdaContext

The [Amazon.Lambda.Core.ILambdaContext](./ILambdaContext.cs) interface can be used in your handler function to access information about the current execution, such as the name of the current function, the memory limit, execution time remaining, and logging.

Here is an example of how this interface can be used in your handler function.
The function performs a simple ToUpper content transformation, while writing some context data to Console.

```csharp
public string ToUpper(string input, ILambdaContext context)
{
    Console.WriteLine("Function name: " + context.FunctionName);
    Console.WriteLine("Max mem allocated: " + context.MemoryLimitInMB);
    Console.WriteLine("Time remaining: " + context.RemainingTime);
    Console.WriteLine("CloudWatch log stream name: " + context.LogStreamName);
    Console.WriteLine("CloudWatch log group name: " + context.LogGroupName);

    return input?.ToUpper();
}
```

An instance of this interface is attached to any `ControllerBase.Request.HttpContext` instances via the `Items` property using the key "[LAMBDA_CONTEXT / LambdaContext](../Amazon.Lambda.AspNetCoreServer/APIGatewayProxyFunction.cs)"

Here is an example of how you can use this interface in a controller method.

```csharp
[ApiController]
public class TestController : ControllerBase
{
    [HttpGet("/[controller]")]
    public IActionResult Get()
    {
        Response.Headers.Add("Access-Control-Allow-Origin", "*"); // NOTE: Should be configured via app.UseCors in Startup.cs

        var context = (ILambdaContext)Request.HttpContext.Items[APIGatewayProxyFunction.LAMBDA_CONTEXT];
        var tmp = new
        {
            context.AwsRequestId,
            context.FunctionName,
            context.MemoryLimitInMB,
            context.LogStreamName,
            context.LogGroupName
        };
        return new OkObjectResult(tmp);
    }
}
```

The following sections describe various other interfaces which are accessible through the `ILambdaContext`.

### IClientContext

The `Amazon.Lambda.Core.IClientContext` interface provides information about the client application and device when the Lambda function is invoked through the AWS Mobile SDK. This includes environment information such as make and model of the device, information about the application, as well as use-defined name-value pairs that describe this installation of the application.
This interface can be found under `ILambdaContext.ClientContext`.

### IClientApplication

The `Amazon.Lambda.Core.IClientApplication` interface provides information about the client application when the Lambda function is invoked through the AWS Mobile SDK. This includes the application title, its version, etc.
This interface can be found under `ILambdaContext.ClientContext.Client`.

### ICognitoIdentity

The `Amazon.Lambda.Core.ICognitoIdentity` interface provides Information about the Amazon Cognito identity provider when invoked through the AWS Mobile SDK. This includes the Amazon Cognito IdentityId and IdentityPoolId.
This interface can be found under `ILambdaContext.Identity`.

### ILambdaLogger

The `Amazon.Lambda.Core.ILambdaLogger` interface allows your function to log data to CloudWatch. This interface defines methods `Log` and `LogLine`. Both take a string and result in a CloudWatch Logs event, with or without a line terminator, provided that the event size is within the allowed limits.

Here is an example of how this interface can be used in your handler function.
The function performs a simple ToUpper content transformation, while logging the context data.

```csharp
public string ToUpper(string input, ILambdaContext context)
{
    context.Logger.Log("Function name: " + context.FunctionName);
    context.Logger.Log("Max mem allocated: " + context.MemoryLimitInMB);
    context.Logger.Log("Time remaining: " + context.RemainingTime);

    return input?.ToUpper();
}
```

## ILambdaSerializer

The `Amazon.Lambda.Core.ILambdaSerializer` interface allows you to implement a custom serializer to convert between arbitrary types and Lambda streams.

By default, Lambda functions can only use Stream types as inputs or outputs. To use other types, you can either write your own serializer that implements ILambdaSerializer, or use the `Amazon.Lambda.Serialization.Json` package to serialize and deserialize JSON data.

See `Amazon.Lambda.Serialization.Json.JsonSerializer` class for a sample implementation of `ILambdaSerializer`.

## LambdaSerializerAttribute

The `Amazon.Lambda.Core.LambdaSerializerAttribute` is an attribute that can is used to instruct the Lambda container what serializer to use when converting .NET types to Lambda-supported types.
This attribute can be present on the assembly or on the handler method. If you specify both, the method attribute takes priority.

Here is an example of setting this attribute on the assembly.

```csharp
[assembly: Amazon.Lambda.Core.LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]
```

And this is how the method can be applied to the handler method.

```csharp
[LambdaSerializer(typeof(XmlSerializer))]
public Response CustomSerializerMethod(Request input)
{
    ...
}
```
