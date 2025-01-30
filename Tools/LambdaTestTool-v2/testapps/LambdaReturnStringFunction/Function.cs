using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

var ToUpper = (APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context) =>
{
    return request.Body.ToUpper();
};

await LambdaBootstrapBuilder.Create(ToUpper, new CamelCaseLambdaJsonSerializer())
    .Build()
    .RunAsync();
