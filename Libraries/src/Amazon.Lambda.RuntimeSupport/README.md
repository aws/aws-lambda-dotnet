# Amazon.Lambda.RuntimeSupport

This package contains classes that can be used to create .NET Core custom runtimes in AWS Lambda.
You can learn more about AWS Lambda custom runtimes [here](https://docs.aws.amazon.com/lambda/latest/dg/runtimes-custom.html).

## LambdaBootstrap

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


