using Amazon.Lambda.Annotations;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using TestServerlessApp.Services;

namespace TestServerlessApp
{
    [LambdaStartup]
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddScoped<ISimpleCalculatorService, SimpleCalculatorService>();
        }
    }
}
