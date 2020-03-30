using Amazon.Lambda.AspNetCoreServer;
namespace TestWebApp
{
    public class HttpV2LambdaFunction : APIGatewayHttpApiV2ProxyFunction<Startup>
    {
    }
}