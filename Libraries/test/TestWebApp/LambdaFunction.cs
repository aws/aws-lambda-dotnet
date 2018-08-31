using Amazon.Lambda.AspNetCoreServer;

namespace TestWebApp
{
    public class LambdaFunction : APIGatewayProxyFunction<Startup>
    {
    }
}
