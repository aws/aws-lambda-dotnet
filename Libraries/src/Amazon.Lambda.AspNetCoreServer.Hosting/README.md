# Amazon.Lambda.AspNetCoreServer.Hosting

This package allows ASP .NET Core applications written using the minimal api style to be deployed
 as AWS Lambda functions. This is done by adding a call to `AddAWSLambdaHosting` to the 
 services collection of the application. This method takes in the `LambdaEventSource` enum
 that configures which Lambda event source the Lambda function will be configured for.

The `AddAWSLambdaHosting` will setup the `Amazon.Lambda.AspNetCoreServer` package to process 
the incoming Lambda events as ASP .NET Core requests. It will also initialize `Amazon.Lambda.RuntimeSupport` package to interact with the Lambda service.

## Supported .NET versions

This library supports .NET 6 and above. Lambda provides managed runtimes for long term supported (LTS) versions like .NET 6 and .NET 8. To use standard term supported (STS) versions like .NET 9
the Lambda function must be bundled as a self contained executable or an OCI image.

## Sample ASP .NET Core application

The code sample below is the typical initilization code for an ASP .NET Core application using the minimal api style. The one difference is the extra line of code calling `AddAWSLambdaHosting`.

```csharp
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register Lambda to replace Kestrel as the web server for the ASP.NET Core application.
// If the application is not running in Lambda then this method will do nothing. 
builder.Services.AddAWSLambdaHosting(LambdaEventSource.HttpApi);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

```

## Handler Configuration

The Lambda function handler must be set to the assembly name (e.g., `MyLambdaProject`). The `AddAWSLambdaHosting` method sets up the Lambda runtime client and registers the callback for processing Lambda events, so the handler should not use the class library format (`<assembly-name>::<full-type-name>::<method-name>`).

## Extension Points

`AddAWSLambdaHosting` accepts an optional `HostingOptions` configuration action that exposes the same customization hooks available in the traditional `AbstractAspNetCoreFunction` base class approach.

### Binary response handling

By default, common binary content types like `image/png` and `application/pdf` are already configured for Base64 encoding. You can register additional types or override the default encoding:

```csharp
builder.Services.AddAWSLambdaHosting(LambdaEventSource.HttpApi, options =>
{
    // Register a custom binary content type
    options.RegisterResponseContentEncodingForContentType("application/x-custom-binary", ResponseContentEncoding.Base64);

    // Ensure compressed responses are Base64-encoded (gzip, deflate, br are already defaults)
    options.RegisterResponseContentEncodingForContentEncoding("zstd", ResponseContentEncoding.Base64);

    // Change the fallback encoding for any unregistered content type
    options.DefaultResponseContentEncoding = ResponseContentEncoding.Base64;
});
```

### Exception details in responses

Useful during development to surface unhandled exception details in the HTTP response body:

```csharp
builder.Services.AddAWSLambdaHosting(LambdaEventSource.HttpApi, options =>
{
    options.IncludeUnhandledExceptionDetailInResponse = app.Environment.IsDevelopment();
});
```

### Customizing request and response marshalling

Callbacks let you inspect or modify the ASP.NET Core feature objects after the Lambda event has been marshalled into them. The second parameter is the raw Lambda request or response object — cast it to the appropriate type for your event source (`APIGatewayHttpApiV2ProxyRequest` for `HttpApi`, `APIGatewayProxyRequest` for `RestApi`, `ApplicationLoadBalancerRequest` for `ApplicationLoadBalancer`).

```csharp
builder.Services.AddAWSLambdaHosting(LambdaEventSource.HttpApi, options =>
{
    // Add a custom header derived from the raw Lambda request
    options.PostMarshallRequestFeature = (requestFeature, lambdaRequest, context) =>
    {
        var apiRequest = (APIGatewayHttpApiV2ProxyRequest)lambdaRequest;
        requestFeature.Headers["X-Stage"] = apiRequest.RequestContext.Stage;
    };

    // Inject the Lambda context into HttpContext.Items for use in middleware or controllers
    options.PostMarshallItemsFeature = (itemsFeature, lambdaRequest, context) =>
    {
        itemsFeature.Items["MyCustomKey"] = context.FunctionName;
    };

    // Modify the response after it has been marshalled back to a Lambda response
    options.PostMarshallResponseFeature = (responseFeature, lambdaResponse, context) =>
    {
        var apiResponse = (APIGatewayHttpApiV2ProxyResponse)lambdaResponse;
        apiResponse.Headers ??= new Dictionary<string, string>();
        apiResponse.Headers["X-Request-Id"] = context.AwsRequestId;
    };
});