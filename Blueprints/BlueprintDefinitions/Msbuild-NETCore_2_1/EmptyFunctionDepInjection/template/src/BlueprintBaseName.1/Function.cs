using System;
using System.IO;
using Amazon.Lambda.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Amazon.Lambda.Core;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace BlueprintBaseName._1
{
    public class Function
    {
        private IConfiguration Configuration { get; }

        public Function()
        {
            var resolver = new DependencyResolver(ConfigureServices);
            Configuration = resolver.ServiceProvider.GetService<IConfiguration>();
        }

        /// <summary>
        /// Constructor for unit tests that can mock IConfiguration
        /// </summary>
        /// <param name="configuration"></param>
        public Function(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        /// <summary>
        /// A simple function that takes a configuration key and returns a value.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public string FunctionHandler(string input, ILambdaContext context)
        {
            return Configuration[input] ?? "None";
        }

        private void ConfigureServices(IServiceCollection services)
        {
            // Add Configuration to DI system
            services.AddConfiguration(builder =>
            {
                var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")?? "Production";
                return builder
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                    .AddJsonFile($"appsettings.{environment}.json", optional: true)
                    .AddEnvironmentVariables()
                    .Build();
            });
        }
    }
}
