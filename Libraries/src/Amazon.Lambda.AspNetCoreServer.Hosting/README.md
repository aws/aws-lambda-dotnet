
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

## Amazon Bedrock Agent API

The `BedrockAgentApi` event source allows your ASP.NET Core application to be invoked by Amazon Bedrock Agent API. This is useful for building custom actions for your Bedrock agents.

When using the `BedrockAgentApi` event source, the Lambda function will receive events in the Bedrock Agent API format and convert them to HTTP requests that your ASP.NET Core application can process. The responses from your application will be converted back to the Bedrock Agent API format.

Example:

```csharp
builder.Services.AddAWSLambdaHosting(LambdaEventSource.BedrockAgentApi);
```