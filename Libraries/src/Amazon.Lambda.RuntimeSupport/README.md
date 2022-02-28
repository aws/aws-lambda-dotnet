# Amazon.Lambda.RuntimeSupport

The Amazon.Lambda.RuntimeSupport package is a .NET Lambda Runtime Interface Client (RIC) for the [Lambda Runtime API](https://docs.aws.amazon.com/lambda/latest/dg/runtimes-api.html).
The Lambda Runtime Interface Client allows your runtime to receive requests from and send requests to the Lambda service.
It can be used for building .NET Lambda functions as either custom runtimes or container images. Starting with the .NET 6 this is also the Lambda rutime client used in managed runtimes.


## Container Image support

AWS provides base images containing all the required components to run your functions packaged as container images on AWS Lambda. Starting with the AWS Lambda .NET 5 base image
Amazon.Lambda.RuntimeSupport is used as the Lambda Runtime Client. The library targets .NET Standard 2.0 and can also be used in earlier versions of .NET Core that support 
.NET Standard like 3.1. 

In the AWS Lambda .NET base image this library is preinstalled into `/var/runtime` directory. .NET Lambda
functions using the base image do not directly interact with this package. Instead they pass in an image command or a Dockerfile `CMD` that indicates the
.NET code to run. The format of that parameter is `<assembly-name>::<full-type-name>::<function-name>`.

Custom base container images where Amazon.Lambda.RuntimeSupport is not preinstalled the library can be included in the .NET Lambda function code as a class library.
To learn how to build a .NET Lambda function using Amazon.Lambda.RuntimeSupport as a class library checkout the **Using Amazon.Lambda.RuntimeSupport as a class library**
section in this README.

The Dockefile below shows how to build a Lambda function using a custom base image. In this case the base image is Microsoft's .NET 6 runtime image. 
This Dockerfile copies the .NET Lambda function into the `/var/task` directory
and then uses the dotnet CLI to execute the .NET Lambda project which will initialize the Amazon.Lambda.RuntimeSupport library and start responding to Lambda events.

```Dockerfile
FROM mcr.microsoft.com/dotnet/runtime:6.0

WORKDIR /var/task

COPY "bin/Release/net6.0/linux-x64/publish"  .

ENTRYPOINT ["/usr/bin/dotnet", "exec", "/var/task/LambdaContainerCustomBase.dll"]
```


## Using Amazon.Lambda.RuntimeSupport as a class library

Amazon.Lambda.RuntimeSupport can be used as a class library to interact with the Lambda Runtime API. This is done by adding the NuGet dependency to Amazon.Lambda.RuntimeSupport and adding a `Main` function to 
Lambda .NET project to initialize Amazon.Lambda.RuntimeSupport library.
The [Amazon.Lambda.RuntimeSupport.LambdaBootstrap](./Bootstrap/LambdaBootstrap.cs) class handles initialization of the function and runs the loop that receives and handles invocations from the AWS Lambda service.
Take a look at the signature of the ToUpperAsync method in the example below.  This signature is the default for function handlers when using the Amazon.Lambda.RuntimeSupport.LambdaBootstrap class.

```csharp
private static MemoryStream ResponseStream = new MemoryStream();
private static JsonSerializer JsonSerializer = new JsonSerializer();

private static async Task Main(string[] args)
{
    using(var bootstrap = new LambdaBootstrap(ToUpperAsync))
    {
        await bootstrap.RunAsync();
    }
}

private static Task<InvocationResponse> ToUpperAsync(InvocationRequest invocation)
{
    var input = JsonSerializer.Deserialize<string>(invocation.InputStream);

    ResponseStream.SetLength(0);
    JsonSerializer.Serialize(input.ToUpper(), ResponseStream);
    ResponseStream.Position = 0;

    return Task.FromResult(new InvocationResponse(responseStream, false));
}
```

The [Amazon.Lambda.RuntimeSupport.HandlerWrapper](./Bootstrap/HandlerWrapper.cs) class allows you to use existing handlers with LambdaBootstrap.
The Amazon.Lambda.RuntimeSupport.HandlerWrapper class also takes care of deserialization and serialization for you.

```csharp
private static async Task Main(string[] args)
{
    using(var handlerWrapper = HandlerWrapper.GetHandlerWrapper((Func<string, ILambdaContext, string>)ToUpper, new JsonSerializer()))
    using(var bootstrap = new LambdaBootstrap(handlerWrapper))
    {
        await bootstrap.RunAsync();
    }
}

// existing handler doesn't conform to the new Amazon.Lambda.RuntimeSupport default signature
public static string ToUpper(string input, ILambdaContext context)
{
    return input?.ToUpper();
}
```

The [Amazon.Lambda.RuntimeSupport.RuntimeApiClient](./Client/RuntimeApiClient.cs) class handles interaction with the [AWS Lambda Runtime Interface](https://docs.aws.amazon.com/lambda/latest/dg/runtimes-api.html).
This class is meant for advanced use cases.  Under most circumstances you won't use it directly.

Below is an excerpt from the [Amazon.Lambda.RuntimeSupport.LambdaBootstrap](./Bootstrap/LambdaBootstrap.cs) class that demonstrates how Amazon.Lambda.RuntimeSupport.RuntimeApiClient is used.
Read the full source for Amazon.Lambda.RuntimeSupport.LambdaBootstrap to learn more.

```csharp
internal async Task InvokeOnceAsync()
{
    using (var invocation = await Client.GetNextInvocationAsync())
    {
        InvocationResponse response = null;
        bool invokeSucceeded = false;

        try
        {
            response = await _handler(invocation);
            invokeSucceeded = true;
        }
        catch (Exception exception)
        {
            await Client.ReportInvocationErrorAsync(invocation.LambdaContext.AwsRequestId, exception);
        }

        if (invokeSucceeded)
        {
            try
            {
                await Client.SendResponseAsync(invocation.LambdaContext.AwsRequestId, response?.OutputStream);
            }
            finally
            {
                if (response != null && response.DisposeOutputStream)
                {
                    response.OutputStream?.Dispose();
                }
            }
        }
    }
}
```


