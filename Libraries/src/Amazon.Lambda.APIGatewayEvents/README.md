# Amazon.Lambda.APIGatewayEvents

This package contains classes that can be used as input types for Lambda functions that process Amazon API Gateway events.

API Gateway events consist of a request that was routed to a Lambda function by API Gateway. When this happens, API Gateway expects the result of the function to be the response that API Gateway should respond with. To see a more detailed example of this, take a look at the [Amazon.Lambda.AspNetCoreServer README.md file](../Amazon.Lambda.AspNetCoreServer/README.md).

# Classes

## APIGatewayProxyRequest

The [APIGatewayProxyRequest](./APIGatewayProxyRequest.cs) class contains information relating to the proxy request coming from the [Amazon API Gateway](https://aws.amazon.com/api-gateway/).

# Sample Functions

The following is a sample class and Lambda function that receives Amazon API Gateway event record data as an input, writes some of the record data to CloudWatch Logs, and responds with a 200 status and the same body as the request. (Note that by default anything written to Console will be logged as CloudWatch Logs events.)

### Function handler

```csharp
public class Function
{
    public APIGatewayProxyResponse Handler(APIGatewayProxyRequest apigProxyEvent)
    {
        Console.WriteLine($"Processing request data for request {apigProxyEvent.RequestContext.RequestId}.");
        Console.WriteLine($"Body size = {apigProxyEvent.Body.Length}.");
        var headerNames = string.Join(", ", apigProxyEvent.Headers.Keys);
        Console.WriteLine($"Specified headers = {headerNames}.");

        return new APIGatewayProxyResponse
        {
            Body = apigProxyEvent.Body,
            StatusCode = 200,
        };
    }
}
```

### Microsoft.AspNetCore.Mvc controller

In a AspNetCore controller, An instance of this interface is attached to any `ControllerBase.Request.HttpContext` instances via the `Items` property using the key "[APIGATEWAY_REQUEST / APIGatewayRequest](../Amazon.Lambda.AspNetCoreServer/APIGatewayProxyFunction.cs)".

The following is an example of accessing an instance of this class in a controller method.

```csharp
[ApiController]
public class TestController : ControllerBase
{
    [HttpGet("/[controller]")]
    public IActionResult Get()
    {
        Response.Headers.Add("Access-Control-Allow-Origin", "*"); // NOTE: Should be configured via app.UseCors in Startup.cs

        var proxyRequest = (APIGatewayProxyRequest)Request.HttpContext.Items[APIGatewayProxyFunction.APIGATEWAY_REQUEST];
        var tmp = new
        {
            proxyRequest.RequestContext.RequestId,
            proxyRequest.RequestContext.Identity?.CognitoIdentityId
        };
        return new OkObjectResult(tmp);
    }
}
```
