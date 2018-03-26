using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Swashbuckle.AspNetCore.Swagger;

namespace TestWebApp
{
    public class Startup
    {
        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true);

            if (env.IsEnvironment("Development"))
            {
                // This will push telemetry data through Application Insights pipeline faster, allowing you to view results immediately.
                builder.AddApplicationInsightsSettings(developerMode: true);
            }

            builder.AddEnvironmentVariables();
            Configuration = builder.Build();
        }

        public IConfigurationRoot Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new Info { Title = "My API", Version = "v1" });
            });

            services.AddAuthorization(options =>
            {
                options.AddPolicy("YouAreSpecial", policy => policy.RequireClaim("you_are_special"));
            });

            // Add framework services.
            services.AddApplicationInsightsTelemetry(Configuration);

            services.AddMvc();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            app.UseSwagger();
            loggerFactory.AddConsole(Configuration.GetSection("Logging"));
            loggerFactory.AddDebug();

            app.UseMiddleware<Middleware>();

            app.UseMvc();

            app.Run(async (context) =>
            {
                var root = new JObject();
                root["Path"] = new JValue(context.Request.Path);
                root["PathBase"] = new JValue(context.Request.PathBase);

                var query = new JObject();
                foreach(var queryKey in context.Request.Query.Keys)
                {
                    var variables = new JArray();
                    foreach(var v in context.Request.Query[queryKey])
                    {
                        variables.Add(new JValue(v));
                    }
                    query[queryKey] = variables;
                }
                root["QueryVariables"] = query;

                context.Response.Headers["Content-Type"] = "application/json";
                await context.Response.WriteAsync(root.ToString());
            });
        }
    }
}
