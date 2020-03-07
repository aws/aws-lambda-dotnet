using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Amazon.Lambda.TestTool.WebTester31
{
    public class Startup
    {
        public static void LaunchWebTester(LocalLambdaOptions lambdaOptions, bool openWindow)
        {
            var port = lambdaOptions.Port ?? Constants.DEFAULT_PORT;
            
            var url = $"http://localhost:{port}";

            var contentPath = Path.GetFullPath(Directory.GetCurrentDirectory());
            var builder = new WebHostBuilder()
                .UseKestrel()
                .SuppressStatusMessages(true)
                .ConfigureServices(services => services.AddSingleton(lambdaOptions))
                .UseContentRoot(contentPath)
                .UseUrls(url)
                .UseStartup<Startup>();

            var host = builder.Build();

            host.Start();
            Console.WriteLine($"Environment running at {url}");

            if (openWindow)
            {
                try
                {
                    var info = new ProcessStartInfo
                    {
                        UseShellExecute = true,
                        FileName = url
                    };
                    Process.Start(info);
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine($"Error launching browser: {e.Message}");
                }
            }

            host.WaitForShutdown();
        }
        
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddRazorPages();
            services.AddServerSideBlazor();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseDeveloperExceptionPage();

            // Handled Embedded Resources like static files
            app.Use(async (context, next) =>
            {
                // All web resources used by the test tool are stored as embedded resources. Check to see if the incoming
                // request is for an embedded resource and if so return it.
                var embeddedResourceName = "Amazon.Lambda.TestTool.WebTester31.wwwroot" + context.Request.Path.Value.Replace('/', '.');
                using (var stream = this.GetType().Assembly.GetManifestResourceStream(embeddedResourceName))
                {
                    if (stream != null)
                    {
                        if (embeddedResourceName.EndsWith(".js"))
                        {
                            context.Response.ContentType = "text/javascript";
                        }
                        else if (embeddedResourceName.EndsWith(".css"))
                        {
                            context.Response.ContentType = "text/css";
                        }

                        await stream.CopyToAsync(context.Response.Body);
                        return;
                    }
                }

                await next();
            });

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapBlazorHub();
                endpoints.MapFallbackToPage("/_Host");
            });
        }
    }
}
