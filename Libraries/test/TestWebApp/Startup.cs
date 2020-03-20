using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Runtime.Serialization.Json;
using System.IO;

#if NETCOREAPP_2_1
using Newtonsoft.Json.Linq;
using Swashbuckle.AspNetCore.Swagger;
#else
using System.Text.Json;
#endif

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

            builder.AddEnvironmentVariables();
            Configuration = builder.Build();
        }

        public IConfigurationRoot Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddAuthorization(options =>
            {
                options.AddPolicy("YouAreSpecial", policy => policy.RequireClaim("you_are_special"));
            });

            services.AddResponseCompression((options) =>
            {
                options.Providers.Add<GzipCompressionProvider>();
                options.EnableForHttps = true;
                options.MimeTypes = new string[] { "application/json-compress" };
            });

#if NETCOREAPP_2_1
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new Info { Title = "My API", Version = "v1" });
            });

            // Add framework services.
            services.AddApplicationInsightsTelemetry(Configuration);

            services.AddMvc();
#elif NETCOREAPP_3_1
            services.AddControllers();
#endif
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            app.UseMiddleware<Middleware>();
            app.UseResponseCompression();

#if NETCOREAPP_3_1
            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
#else
            app.UseSwagger();

            app.UseMvc();
#endif
            app.Run(async (context) =>
            {
#if NETCOREAPP_2_1
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

                var body = root.ToString();
#else
                var stream = new MemoryStream();
                var writer = new Utf8JsonWriter(stream);
                writer.WriteStartObject();
                writer.WriteString("Path", context.Request.Path);
                writer.WriteString("PathBase", context.Request.PathBase);
                writer.WriteStartObject("QueryVariables");
                foreach (var queryKey in context.Request.Query.Keys)
                {
                    writer.WriteStartArray(queryKey);
                    foreach (var v in context.Request.Query[queryKey])
                    {
                        writer.WriteStringValue(v);
                    }
                    writer.WriteEndArray();
                }
                writer.WriteEndObject();
                writer.WriteEndObject();
                writer.Dispose();
                stream.Position = 0;
                var body = new StreamReader(stream).ReadToEnd();
#endif
                context.Response.Headers["Content-Type"] = "application/json";
                await context.Response.WriteAsync(body);
            });
        }
    }
}
