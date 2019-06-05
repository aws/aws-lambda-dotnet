namespace Amazon.Lambda.ApplicationLoadBalancerIdentity.Tests
{
    using System;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.DependencyInjection;

    public class TestStartup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services
                .AddLogging()
                .AddSingleton(new ALBIdentityMiddlewareOptions
                {
                    MaxCacheSizeMB = 10,
                    MaxCacheLifetime = TimeSpan.FromHours(1),
                    VerifyTokenSignature = true,
                    ValidateTokenLifetime = false
                });
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment host)
        {
            app
                .UseMiddleware<ALBIdentityMiddleware>()
                .Run(async _ => await _.Response.WriteAsync(_.User.Identity.Name));
        }
    }
}
