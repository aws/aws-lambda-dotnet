# Amazon.Lambda.AspNetCoreServer

This package makes it easy to run ASP.NET Core Web API applications as a Lambda function with API Gateway or an ELB Application Load Balancer. This allows .NET Core developers to
create "serverless" applications using the ASP.NET Core Web API framework. 

The function takes a request from an [API Gateway Proxy](http://docs.aws.amazon.com/apigateway/latest/developerguide/api-gateway-create-api-as-simple-proxy.html)
or from an [Application Load Balancer](https://docs.aws.amazon.com/elasticloadbalancing/latest/application/lambda-functions.html)
and converts that request into the classes the ASP.NET Core framework expects and then converts the response from the ASP.NET Core
framework into the response body that API Gateway Proxy or Application Load Balancer understands.

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

Here is an example implementation of the Lamba function in an ASP.NET Core Web API application.
```csharp
using System.IO;

using Amazon.Lambda.AspNetCoreServer;
using Microsoft.AspNetCore.Hosting;

namespace TestWebApp
{
    /// <summary>
    /// This class extends from APIGatewayProxyFunction which contains the method FunctionHandlerAsync which is the 
    /// actual Lambda function entry point. The Lambda handler field should be set to
    /// 
    /// AWSServerless19::AWSServerless19.LambdaEntryPoint::FunctionHandlerAsync
    /// </summary>
    public class LambdaEntryPoint :

        // The base class must be set to match the AWS service invoking the Lambda function. If not Amazon.Lambda.AspNetCoreServer
        // will fail to convert the incoming request correctly into a valid ASP.NET Core request.
        //
        // API Gateway REST API                         -> Amazon.Lambda.AspNetCoreServer.APIGatewayProxyFunction
        // API Gateway HTTP API payload version 1.0     -> Amazon.Lambda.AspNetCoreServer.APIGatewayProxyFunction
        // API Gateway HTTP API payload version 2.0     -> Amazon.Lambda.AspNetCoreServer.APIGatewayHttpApiV2ProxyFunction
        // Application Load Balancer                    -> Amazon.Lambda.AspNetCoreServer.ApplicationLoadBalancerFunction
        // 
        // Note: When using the AWS::Serverless::Function resource with an event type of "HttpApi" then payload version 2.0
        // will be the default and you must make Amazon.Lambda.AspNetCoreServer.APIGatewayHttpApiV2ProxyFunction the base class.

        Amazon.Lambda.AspNetCoreServer.APIGatewayProxyFunction
    {
        /// <summary>
        /// The builder has configuration, logging and Amazon API Gateway already configured. The startup class
        /// needs to be configured in this method using the UseStartup<>() method.
        /// </summary>
        /// <param name="builder"></param>
        protected override void Init(IWebHostBuilder builder)
        {
            builder
                .UseStartup<Startup>();
        }
    }
}
```

The function handler for the Lambda function will be **TestWebApp::TestWebApp.LambdaEntryPoint::FunctionHandlerAsync**.

## Bootstrapping application (IWebHostBuilder vs IHostBuilder)

ASP.NET Core applications are bootstrapped by using a host builder. The host builder is used to configure all of the required services needed to run the ASP.NET Core application. With Amazon.Lambda.AspNetCoreServer there are multiple options for customizing the bootstrapping and they vary between targeted versions of .NET Core.

### ASP.NET Core 3.1

ASP.NET Core 3.1 uses the generic `IHostBuilder` to bootstrap the application. In a typical ASP.NET Core 3.1 application the `Program.cs` file will bootstrap the application using `IHostBuilder` like the following snippet shows. As part of creating the `IHostBuilder` an `IWebHostBuilder` is created by the `ConfigureWebHostDefaults` method.

```csharp
public static IHostBuilder CreateHostBuilder(string[] args) =>
    Host.CreateDefaultBuilder(args)
        .ConfigureWebHostDefaults(webBuilder =>
        {
            webBuilder.UseStartup<Startup>();
        });
```

Amazon.Lambda.AspNetCoreServer creates this `IHostBuilder` and configures all of the default settings needed to run the ASP.NET Core application in Lambda. 

There are two `Init` methods that can be overridden to customize the `IHostBuilder`. The most common customization is to override the `Init(IWebHostBuilder)` method and set the startup class via the `UseStartup` method. To customize the `IHostBuilder` then override the `Init(IHostBuilder)`. **Do not call `ConfigureWebHostDefaults` when overriding `Init(IHostBuilder)` because Amazon.Lambda.AspNetCoreServer will call `ConfigureWebHostDefaults` when creating the `IHostBuilder`. By calling `ConfigureWebHostDefaults` in the `Init(IHostBuilder)` method, the `IWebHostBuilder` will be configured twice.**

If you want complete control over creating the `IHostBuilder` then override the `CreateHostBuilder` method. When overriding the `CreateHostBuilder` method neither of the `Init` methods will be called unless the override calls the base implementation. When overriding `CreateHostBuilder` it is recommended to call `ConfigureWebHostLambdaDefaults` instead of `ConfigureWebHostDefaults` to configure the `IWebHostBuilder` for Lambda.

If the `CreateWebHostBuilder` is overridden in an ASP.NET Core 3.1 application then only the `IWebHostBuilder` is used for bootstrapping using the same pattern that ASP.NET Core 2.1 applications use. `CreateHostBuilder` and `Init(IHostBuilder)` will not be called when `CreateWebHostBuilder` is overridden.



### ASP.NET Core 2.1

ASP.NET Core 2.1 applications are bootstrapped with the `IWebHostBuilder` type. Amazon.Lambda.AspNetCoreServer will create an instance of `IWebHostBuilder` and it can be customized by overriding the `Init(IWebHostBuilder)` method. The most common customization is configuring the startup class via the `UseStartup` method.

If you want complete control over creating the `IWebHostBuilder` then override the `CreateWebHostBuilder` method. When overriding the `CreateWebHostBuilder` method the `Init(IWebHostBuilder)` method will not be called unless the override calls the base implementation or explicitly calls the `Init(IWebHostBuilder)` method.


## Access to Lambda Objects from HttpContext

The original lambda request object and the `ILambdaContext` object can be accessed from the `HttpContext.Items` collection.

| Constant | Object |
|-------|--------|
| AbstractAspNetCoreFunction.LAMBDA_CONTEXT | ILambdaContext |
| AbstractAspNetCoreFunction.LAMBDA_REQUEST_OBJECT | <ul><li>APIGatewayProxyFunction -> APIGatewayProxyRequest</li><li>APIGatewayHttpApiV2ProxyFunction -> APIGatewayHttpApiV2ProxyRequest</li><li>ApplicationLoadBalancerFunction -> ApplicationLoadBalancerRequest</li></ul> |


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
