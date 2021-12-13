using System;
using System.Collections.Generic;
using System.Text;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Amazon.Lambda.AspNetCoreServer.Internal;
using Microsoft.AspNetCore.Hosting.Server;

using Microsoft.AspNetCore.Hosting;
using System.IO;

namespace Microsoft.Extensions.Hosting
{
    /// <summary>
    /// Extension methods for IHostBuilder.
    /// </summary>
    public static class HostBuilderExtensions
    {
        /// <summary>
        /// Configures the default settings for IWebHostBuilder when running in Lambda. The major difference between ConfigureWebHostDefaults and ConfigureWebHostLambdaDefaults
        /// is that it calls "webBuilder.UseLambdaServer()" to swap out Kestrel for Lambda as the IServer.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="configure"></param>
        /// <returns></returns>
        public static IHostBuilder ConfigureWebHostLambdaDefaults(this IHostBuilder builder, Action<IWebHostBuilder> configure)
        {
            builder.ConfigureWebHostDefaults(webBuilder =>
                    {
                        webBuilder
                            .UseContentRoot(Directory.GetCurrentDirectory())
                            .ConfigureLogging((hostingContext, logging) =>
                            {
                                logging.AddConfiguration(hostingContext.Configuration.GetSection("Logging"));

                                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LAMBDA_TASK_ROOT")))
                                {
                                    logging.AddConsole();
                                    logging.AddDebug();
                                }
                                else
                                {
                                    logging.ClearProviders();
                                    logging.AddLambdaLogger(hostingContext.Configuration, "Logging");
                                }
                            })
                            .UseDefaultServiceProvider((hostingContext, options) =>
                            {
                                options.ValidateScopes = hostingContext.HostingEnvironment.IsDevelopment();
                            });

                        // Swap out Kestrel as the webserver and use our implementation of IServer
                        webBuilder.UseLambdaServer();

                        configure(webBuilder);
                    });

            
            return builder;
        }
    }
}