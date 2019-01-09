using Amazon.Lambda.AspNetCoreServer;

namespace TestWebApp
{
    public class ApiGatewayLambdaFunction : APIGatewayProxyFunction<Startup>
    {
    }
}
