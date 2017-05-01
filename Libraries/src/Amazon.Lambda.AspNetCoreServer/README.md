# Amazon.Lambda.AspNetCoreServer

This package makes it easy to run ASP.NET Core Web API applications as a Lambda function with API Gateway. This allows .NET Core developers to
create "serverless" applications using the ASP.NET Core Web API framework. 

The function takes a request from an [API Gateway Proxy](http://docs.aws.amazon.com/apigateway/latest/developerguide/api-gateway-create-api-as-simple-proxy.html)
and converts that request into the classes the ASP.NET Core framework expects and then converts the response from the ASP.NET Core
framework into the response body that API Gateway Proxy understands.

## Example Lambda Function

In the ASP.NET Core application add a class that extends from [APIGatewayProxyFunction](../Amazon.Lambda.AspNetCoreServer/APIGatewayProxyFunction.cs)
and implement the Init method.

Here is an example implementation of the Lamba function in an ASP.NET Core Web API application.
```csharp
using System.IO;

using Amazon.Lambda.AspNetCoreServer;
using Microsoft.AspNetCore.Hosting;

namespace TestWebApp
{
    public class LambdaFunction : APIGatewayProxyFunction
    {
        protected override void Init(IWebHostBuilder builder)
        {
            builder
                .UseApiGateway()
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseStartup<Startup>();
        }
    }
}
```

The function handler for the Lambda function will be **TestWebApp::TestWebApp.LambdaFunction::FunctionHandlerAsync**.

Once the function is deployed configure API Gateway with a HTTP Proxy to call the Lambda Function. Refer to the API Gateway 
[developer guide](http://docs.aws.amazon.com/apigateway/latest/developerguide/api-gateway-create-api-as-simple-proxy.html) for more information.

## Supporting Binary Response Content

The interface between the API Gateway and Lambda provides for and assumes response content to be returned as a UTF-8 string.
In order to return binary content it is necessary to encode the raw response content in Base64 and to set a flag in the
response object that Base64-encoding has been applied.

In order to facilitate this mechanism, the `APIGatewayProxyFunction` base class maintains a registry of MIME content types
and how they should be transformed before being returned to the calling API Gateway.  For any binary content types that are
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