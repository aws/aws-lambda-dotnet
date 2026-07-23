using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

// Adds the two path parameters {x} and {y} and returns the sum.
// Uses the HTTP API v2 request shape, so run the API Gateway emulator in HttpV2 mode.
var handler = (APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context) =>
{
    var x = int.Parse(request.PathParameters["x"]);
    var y = int.Parse(request.PathParameters["y"]);
    return (x + y).ToString();
};

await LambdaBootstrapBuilder.Create(handler, new CamelCaseLambdaJsonSerializer())
    .Build()
    .RunAsync();
