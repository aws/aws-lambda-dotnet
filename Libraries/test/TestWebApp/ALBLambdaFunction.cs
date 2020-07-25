using Amazon.Lambda.AspNetCoreServer;

namespace TestWebApp
{
    public class ALBLambdaFunction : ApplicationLoadBalancerFunction<Startup>
    {
        public ALBLambdaFunction(StartupMode startupMode)
            : base(startupMode)
        { }
    }
}
