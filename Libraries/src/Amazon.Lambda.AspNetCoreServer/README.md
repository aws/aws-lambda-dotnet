# Amazon.Lambda.AspNetCoreServer

This package makes it easy to run ASP.NET Core Web API applications as a Lambda function with API Gateway or an ELB Application Load Balancer. This allows .NET Core developers to
create "serverless" applications using the ASP.NET Core Web API framework. 

The function takes a request from an [API Gateway Proxy](http://docs.aws.amazon.com/apigateway/latest/developerguide/api-gateway-create-api-as-simple-proxy.html)
or from an [Application Load Balancer](https://docs.aws.amazon.com/elasticloadbalancing/latest/application/lambda-functions.html)
and converts that request into the classes the ASP.NET Core framework expects and then converts the response from the ASP.NET Core
framework into the response body that API Gateway Proxy or Application Load Balancer understands.

## Supported .NET versions

This library supports .NET 6 and above. Lambda provides managed runtimes for long term supported (LTS) versions like .NET 6 and .NET 8. To use standard term supported (STS) versions like .NET 9
the Lambda function must be bundled as a self contained executable or an OCI image.

## Amazon.Lambda.AspNetCoreServer vs Amazon.Lambda.AspNetCoreServer.Hosting

The `Amazon.Lambda.AspNetCoreServer` is typically used with the older ASP.NET Core pattern of having a `Startup` class to setup the ASP.NET Core application. This allows you to share the ASP.NET Core setup logic between the `LambdaEntryPoint` and the normal `Main` entrypoint used for running the ASP.NET Core applications locally. For integrating Lambda into an ASP.NET Core project using the minimal api pattern checkout the [Amazon.Lambda.AspNetCoreServer.Hosting](https://github.com/aws/aws-lambda-dotnet/tree/master/Libraries/src/Amazon.Lambda.AspNetCoreServer.Hosting). This package integrates Amazon.Lambda.AspNetCoreServer setup into the project's `Main` or top level statements.

Using Amazon.Lambda.AspNetCoreServer directly instead of Amazon.Lambda.AspNetCoreServer.Hosting is recommened when projects need to customize the Lambda behavior through the `LambdaEntryPoint` by overriding the base class methods.

## Lambda Entry Point

In the ASP.NET Core application add a class that will be the entry point for Lambda to call into the application. Commonly this class
is called `LambdaEntryPoint`. The base class is determined based on where the Lambda functions will be invoked from.

|Lambda Involve| Base Class |
|----------|---------------|
| API Gateway REST API | APIGatewayProxyFunction |
| API Gateway WebSocket API | APIGatewayProxyFunction |
| API Gateway HTTP API Payload 1.0 | APIGatewayProxyFunction |
| API Gateway HTTP API Payload 2.0 | APIGatewayHttpApiV2ProxyFunction |
| Application Load Balancer | ApplicationLoadBalancerFunction |

**Note:** HTTP API default to payload 2.0 so unless 1.0 is explicitly set the base class should be APIGatewayHttpApiV2ProxyFunction.

The function handler for the Lambda function will be **TestWebApp::TestWebApp.LambdaEntryPoint::FunctionHandlerAsync**.

## ASP.NET Core setup

The `LambdaEntryPoint` class in the ASP.NET Core project inherits `Init` methods that can be used to setup ASP.NET Core application. The most common approach is override the `Init` and using the `UseStartup` method on the `IWebHostBuilder` register the startup class. The startup class contains ASP.NET Core setup logic that can be shared between the `LambdaEntryPoint` and the project's `Main` method.

Example `LambdaEntryPoint`:
```csharp
public class LambdaEntryPoint : Amazon.Lambda.AspNetCoreServer.APIGatewayProxyFunction
{
    /// <summary>
    /// The builder has configuration, logging and Amazon API Gateway already configured. The startup class
    /// needs to be configured in this method using the UseStartup<>() method.
    /// </summary>
    /// <param name="builder">The IWebHostBuilder to configure.</param>
    protected override void Init(IWebHostBuilder builder)
    {
        builder
            .UseStartup<Startup>();
    }
}
```

Example `Startup`:
```csharp
public class Startup
{
    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    public IConfiguration Configuration { get; }

    // This method gets called by the runtime. Use this method to add services to the container
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddControllers();
    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseHttpsRedirection();

        app.UseRouting();

        app.UseAuthorization();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
            endpoints.MapGet("/", async context =>
            {
                await context.Response.WriteAsync("Welcome to running ASP.NET Core on AWS Lambda");
            });
        });
    }
}
```

## JSON Serialization

Starting with version 5.0.0 when targeting .NET Core 3.1 `Amazon.Lambda.Serialization.SystemTextJson`. When targeting previous 
versions of .NET Core or using a version of Amazon.Lambda.AspNetCoreServer before 5.0.0 will use `Amazon.Lambda.Serialization.Json`.


## Web App Path Base

By default this package configure the path base for incoming requests to be root of the API Gateway Stage or Application Load Balancer.

If you want to treat a subresource in the resource path to be the path base you will need to modify how requests are marshalled 
into ASP.NET Core. For example if the listener of an Application Load Balancer 
points to a Lambda Target Group for requests starting with `/webapp/*` and you want to call a controller `api/values` ASP.NET Core
will think the resource you want to access is `/webapp/api/values` which will return a 404 NotFound.

In the `LambdaEntryPoint` class you can override the `PostMarshallRequestFeature` method to add custom logic to how
the path base is computed. In the example below it configures the path base to be `/webapp/`. When the Application Load balancer 
sends in a request with the resource path set to /webapp/api/values. This code configures the ASP.NET Core request to have the
path base set to /webapp/ and the path to /api/values.

```csharp
    public class LambdaEntryPoint : ApplicationLoadBalancerFunction
    {
        protected override void Init(IWebHostBuilder builder)
        {
            builder
                .UseStartup<Startup>();
        }

        protected override void PostMarshallRequestFeature(IHttpRequestFeature aspNetCoreRequestFeature, ApplicationLoadBalancerRequest lambdaRequest, ILambdaContext lambdaContext)
        {
            aspNetCoreRequestFeature.PathBase = "/webapp/";

            // The minus one is ensure path is always at least set to `/`
            aspNetCoreRequestFeature.Path = 
                aspNetCoreRequestFeature.Path.Substring(aspNetCoreRequestFeature.PathBase.Length - 1);
            lambdaContext.Logger.LogLine($"Path: {aspNetCoreRequestFeature.Path}, PathBase: {aspNetCoreRequestFeature.PathBase}");
        }
    }
```


## Supporting Binary Response Content

The interface between the API Gateway/Application Load Balancer and Lambda assumes response content to be returned as a UTF-8 string.
In order to return binary content it is necessary to encode the raw response content in Base64 and to set a flag in the
response object that Base64-encoding has been applied.

In order to facilitate this mechanism, the base class maintains a registry of MIME content types
and how they should be transformed before being returned to the calling API Gateway or Application Load Balancer.  For any binary content types that are
returned by your application, you should register them for Base64 tranformation and then the framework will take care of
intercepting any such responses and making an necessary transformations to preserve the binary content.  For example:

```csharp
using System.IO;

using Amazon.Lambda.AspNetCoreServer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;

namespace TestWebApp
{
    public class LambdaFunction : APIGatewayProxyFunction
    {
        protected override void Init(IWebHostBuilder builder)
        {
            // Register any MIME content types you want treated as binary
            RegisterResponseContentEncodingForContentType("application/octet-stream",
                    ResponseContentEncoding.Base64);
            
            // ...
        }
    }

    // In your controller actions, be sure to provide a Content Type for your responses
    public class LambdaController : Controller
    {
        public IActionResult GetBinary()
        {
            var binData = new byte[] { 0x00, 0x01, 0x02, 0x03 };
 
            return base.File(binData, "application/octet-stream");
        }
    }
}
```

### IMPORTANT - Registering Binary Response with API Gateway

In order to use this mechanism to return binary response content, in addition to registering any binary
MIME content types that will be returned by your application, it also necessary to register those same
content types with the API Gateway using either the [console](http://docs.aws.amazon.com/apigateway/latest/developerguide/api-gateway-payload-encodings-configure-with-console.html)
or the [REST interface](http://docs.aws.amazon.com/apigateway/latest/developerguide/api-gateway-payload-encodings-configure-with-control-service-api.html).

For Application Load Balancer this step is not necessary.

### Default Registered Content Types

By default several commonly used MIME types that are typically used with Web API services
are already pre-registered.  You can make use of these content types without any further
changes in your code, *however*, for any binary content types, you do still need to make
the necessary adjustments in the API Gateway as described above.


MIME Content Type | Response Content Encoding
------------------|--------------------------
`text/plain`               | Default (UTF-8)
`text/xml`                 | Default (UTF-8)
`application/xml`          | Default (UTF-8)
`application/json`         | Default (UTF-8)
`text/html`                | Default (UTF-8)
`text/css`                 | Default (UTF-8)
`text/javascript`          | Default (UTF-8)
`text/ecmascript`          | Default (UTF-8)
`text/markdown`            | Default (UTF-8)
`text/csv`                 | Default (UTF-8)
`application/octet-stream` | Base64
`image/png`                | Base64
`image/gif`                | Base64
`image/jpeg`               | Base64
`application/zip`          | Base64
`application/pdf`          | Base64
