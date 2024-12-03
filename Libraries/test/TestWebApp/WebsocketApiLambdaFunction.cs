using Amazon.Lambda.AspNetCoreServer;
namespace TestWebApp
{
    public class WebsocketLambdaFunction : APIGatewayWebsocketApiProxyFunction<Startup>
    {
    }
}