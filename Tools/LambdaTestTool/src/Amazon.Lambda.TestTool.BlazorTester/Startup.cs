using Amazon.Lambda.TestTool.BlazorTester.Services;
using Amazon.Lambda.TestTool.Services;
using Blazored.Modal;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Amazon.Lambda.TestTool.BlazorTester
{
    public class Startup
    {
        public static void LaunchWebTester(LocalLambdaOptions lambdaOptions, bool openWindow)
        {
            var host = StartWebTesterAsync(lambdaOptions, openWindow).GetAwaiter().GetResult();
            host.WaitForShutdown();
        }

        public static async Task<IWebHost> StartWebTesterAsync(LocalLambdaOptions lambdaOptions, bool openWindow, CancellationToken token = default(CancellationToken))
        {
            var host = string.IsNullOrEmpty(lambdaOptions.Host)
                ? Constants.DEFAULT_HOST
                : lambdaOptions.Host;
            var port = lambdaOptions.Port ?? Constants.DEFAULT_PORT;
            var url = $"http://{host}:{port}";

            var contentPath = Path.GetFullPath(Directory.GetCurrentDirectory());
            var builder = new WebHostBuilder()
                .UseKestrel()
                .SuppressStatusMessages(true)
                .ConfigureServices(services => services.AddSingleton(lambdaOptions))
                .UseContentRoot(contentPath)
                .UseUrls(url)
                .UseStartup<Startup>();

            var webHost = builder.Build();

            await webHost.StartAsync(token);
            Console.WriteLine($"Environment running at {url}");

            if (openWindow)
            {
                try
                {
                    string launchUrl = Utils.DetermineLaunchUrl(host, port, Constants.DEFAULT_HOST);
                    var info = new ProcessStartInfo
                    {
                        UseShellExecute = true,
                        FileName = launchUrl
                    };
                    Process.Start(info);
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine($"Error launching browser: {e.Message}");
                }
            }

            return webHost;
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
            services.AddSingleton<IRuntimeApiDataStore, RuntimeApiDataStore>();
            services.AddControllers();
            services.AddRazorPages();
            services.AddServerSideBlazor()
                    .AddHubOptions(options => options.MaximumReceiveMessageSize = null);
            services.AddHttpContextAccessor();
            services.AddScoped<ILamdaService, LamdaService>();
            services.AddSingleton<IAwsProfileConfig, AwsProfileConfig>();
            services.AddBlazoredModal();

            services.AddOptions<StaticFileOptions>()
                .PostConfigure(o =>
                {
                    var fileProvider = new ManifestEmbeddedFileProvider(typeof(Startup).Assembly, "wwwroot");

                    // Make sure we don't remove the existing file providers (blazor needs this)
                    o.FileProvider = new CompositeFileProvider(o.FileProvider, fileProvider);
                });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILamdaService lamdaService, LocalLambdaOptions localLambdaOptions, IAwsProfileConfig awsProfileConfig)
        {
            app.UseDeveloperExceptionPage();

            app.UseStaticFiles();

            app.UseRouting();

            app.UseCors();
            var configInfo = awsProfileConfig.LambdaConfigInfo();

            app.UseEndpoints(endpoints =>
            {
                if (configInfo != null)
                {
                    foreach (var function in configInfo.FunctionInfos.Where(x => !string.IsNullOrEmpty(x.Method)))
                    {
                        endpoints.MapMethods(function.Path, new string[] { function.Method.ToUpper() }, async (context) =>
                        {
                            var routeValue = context.Request.RouteValues.ToDictionary(x => x.Key, x => x.Value.ToString());
                            var result = await lamdaService.Execute(function.Name, context, context.Request.RouteValues.ToDictionary(x => x.Key, x => x.Value.ToString()));
                            var response = result.IsSuccess ? result.Response : result.Error;

                            var statusCode = (int)HttpStatusCode.OK;
                            //check if response contains the error status code
                            if (result.IsSuccess) //we belive it is success but is it really?
                            {
                                try
                                {
                                    var jDoc = JsonDocument.Parse(response);
                                    if (jDoc.RootElement.TryGetProperty("statusCode", out JsonElement jsonElement))
                                    {
                                        jsonElement.TryGetInt32(out statusCode);
                                    }

                                }
                                catch (Exception)
                                {

                                }

                            }

                            //check if there is body in response

                            try
                            {
                                var jDoc = JsonDocument.Parse(response);
                                if (jDoc.RootElement.TryGetProperty("body", out JsonElement jsonElement))
                                {
                                    response = jsonElement.ToString();
                                }
                            }
                            catch (Exception)
                            {

                            }

                            var responseCode = result.IsSuccess ? statusCode : (int)HttpStatusCode.InternalServerError;
                            context.Response.StatusCode = responseCode;
                            await context.Response.WriteAsync(response);
                        });
                    }
                }
                endpoints.MapControllers();
                endpoints.MapBlazorHub(o =>
                {
                    o.ApplicationMaxBufferSize = 6 * (1024 * 1024);
                    o.TransportMaxBufferSize = 6 * (1024 * 1024);
                });
                endpoints.MapFallbackToPage("/_Host");
            });
        }
    }
}
