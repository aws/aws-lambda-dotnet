using Amazon.Lambda.AspNetCoreServer;

namespace TestWebApp
{
    public class ALBLambdaFunction : ApplicationLoadBalancerFunction<Startup>
    {
    }
}
