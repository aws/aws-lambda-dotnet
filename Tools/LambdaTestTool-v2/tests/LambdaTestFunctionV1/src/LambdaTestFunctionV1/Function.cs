using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

var ToUpper = (APIGatewayProxyRequest request, ILambdaContext context) =>
{
    return new APIGatewayProxyResponse()
    {
        StatusCode = 200,
        Body = request.Body.ToUpper(),
        IsBase64Encoded = false,
    };
};

await LambdaBootstrapBuilder.Create(ToUpper, new CamelCaseLambdaJsonSerializer())
    .Build()
    .RunAsync();
