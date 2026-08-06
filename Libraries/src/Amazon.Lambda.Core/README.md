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

## Response Streaming

This package includes types under the `Amazon.Lambda.Core.ResponseStreaming` namespace that let a handler stream its response back incrementally instead of buffering the entire payload. This raises the maximum response size beyond the standard 6 MB buffered limit and lets callers receive data as soon as it is produced.

Use `LambdaResponseStreamFactory` to create a write-only `LambdaResponseStream` (a `System.IO.Stream`) and write to it with any standard stream consumer, such as `StreamWriter`. Once a handler creates a response stream, all output must be written to the stream and the handler's return value is ignored.

```csharp
using Amazon.Lambda.Core.ResponseStreaming;

public async Task StreamHandler(string input, ILambdaContext context)
{
    await using var responseStream = LambdaResponseStreamFactory.CreateStream();
    using var writer = new StreamWriter(responseStream);

    for (var i = 0; i < 5; i++)
    {
        await writer.WriteLineAsync($"Chunk {i}");
        await writer.FlushAsync();
    }
}
```

When the function is invoked through a Lambda Function URL or API Gateway, use `CreateHttpStream(HttpResponseStreamPrelude)` instead. The prelude sets the HTTP status code, headers, and cookies and is sent as the first chunk before the response body.

```csharp
var prelude = new HttpResponseStreamPrelude
{
    StatusCode = HttpStatusCode.OK,
    Headers = { ["Content-Type"] = "text/plain" }
};
await using var responseStream = LambdaResponseStreamFactory.CreateHttpStream(prelude);
```

Response streaming also requires a current version of the `Amazon.Lambda.RuntimeSupport` package. For more details and end-to-end examples, see [Announcing response streaming for .NET on AWS Lambda](https://aws.amazon.com/blogs/developer/announcing-response-streaming-for-net-on-aws-lambda/).

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
