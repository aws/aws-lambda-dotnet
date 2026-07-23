using Amazon.Lambda.AspNetCoreServer;

using Microsoft.Extensions.Hosting;

namespace TestWebApp
{
    public class ALBLambdaFunction : ApplicationLoadBalancerFunction<Startup>
    {
        protected override void Init(IHostBuilder builder) => DisableConfigFileWatching.Apply(builder);
    }
}
