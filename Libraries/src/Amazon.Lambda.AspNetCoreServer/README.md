# Amazon.Lambda.AspNetCoreServer

This package makes it easy to run ASP.NET Core Web API applications as a Lambda function with API Gateway. This allows .NET Core developers to
create "serverless" applications using the ASP.NET Core Web API framework. 

The function takes expects a request from an [API Gateway Proxy](http://docs.aws.amazon.com/apigateway/latest/developerguide/api-gateway-create-api-as-simple-proxy.html)
and converts that request into the classes the ASP.NET Core framework expects and then converts the response from the ASP.NET Core
framework into the response body that API Gateway Proxy understands.

## Example Lambda Function

In the ASP.NET Core application add a class that extends from [APIGatewayProxyFunction](../Amazon.Lambda.AspNetCoreServer/APIGatewayProxyFunction.cs)
and implement the Init method.

Here is an example implementation of the Lamba function in an ASP.NET Core Web API application.
```
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