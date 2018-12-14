# Amazon.Lambda.ApplicationLoadBalancerEvents

This package contains classes that can be used as requests and responses for Lambda functions that process Elastic Load Balancing's Application Load Balancers.

# Classes

## ApplicationLoadBalancerRequest

The [ApplicationLoadBalancerRequest](./ApplicationLoadBalancerRequest.cs) class contains information relating to the 
request coming from an [Application Load Balancer](https://docs.aws.amazon.com/elasticloadbalancing/latest/application/lambda-functions.html).

### Multi-Value Headers

ELB Lambda Target Groups can be enabled to support multiple values for headers and querystring parameters. If support is enabled and a 
client sends a request with multiple values then the `MultiValueHeaders` and `MultiValueQueryStringParameters` collections should be used
instead of `Headers` and `QueryStringParameters` collection. 

Either `Headers` or `MultiValueHeaders` collection will be set, never both. The Lambda function can check to see which collection
has values and then use that collection from then on.

# Sample Functions

The following is a sample class and Lambda function that receives an Application Load Balancer request as an input, 
writes some of the record data to CloudWatch Logs, and responds with a 200 status and the same body as the 
request. (Note that by default anything written to Console will be logged as CloudWatch Logs events.)

### Function handler

```csharp
public class Function
{
    public ApplicationLoadBalancerResponse Handler(ApplicationLoadBalancerRequest request)
    {
        Console.WriteLine($"Processing request data for request {request.RequestContext.RequestId}.");
        Console.WriteLine($"Body size = {request.Body.Length}.");
        var headerNames = string.Join(", ", request.Headers.Keys);
        Console.WriteLine($"Specified headers = {headerNames}.");

        return new ApplicationLoadBalancerResponse
        {
            Body = request.Body,
            StatusCode = 200,
        };
    }
}
```

