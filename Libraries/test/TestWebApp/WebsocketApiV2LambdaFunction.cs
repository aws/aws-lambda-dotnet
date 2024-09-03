using Amazon.Lambda.AspNetCoreServer;
namespace TestWebApp
{
    public class WebsocketV2LambdaFunction : APIGatewayWebsocketApiV2ProxyFunction<Startup>
    {
    }
}