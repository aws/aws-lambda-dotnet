using Amazon.Lambda.TestTool.BlazorTester.Services;
using Blazored.Modal;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;

namespace Amazon.Lambda.TestTool.BlazorTester
{
    public class Startup
    {
        public static void LaunchWebTester(LocalLambdaOptions lambdaOptions, bool openWindow)
        {
            var host = StartWebTesterAsync(lambdaOptions, openWindow).GetAwaiter().GetResult();
            host.WaitForShutdown();
        }

#if NET10_0_OR_GREATER
        public static async Task<WebApplication> StartWebTesterAsync(LocalLambdaOptions lambdaOptions, bool openWindow, CancellationToken token = default(CancellationToken))
        {
            var host = string.IsNullOrEmpty(lambdaOptions.Host)
                ? Constants.DEFAULT_HOST
                : lambdaOptions.Host;
            var port = lambdaOptions.Port ?? Constants.DEFAULT_PORT;
            var url = $"http://{host}:{port}";


            var contentPath = Directory.GetParent(Assembly.GetExecutingAssembly().Location).FullName;
            if (!Directory.Exists(contentPath))
            {
                contentPath = Path.GetFullPath(Directory.GetCurrentDirectory());
            }

            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                ContentRootPath = contentPath,
            });

            // When running as an end user, we want the console to show only logs from Console.Write calls
            // and not be cluttered with framework logs.
#if !DEBUG
            builder.Logging.ClearProviders();
#endif

            builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents();

            builder.Services.AddControllers()
                            .AddApplicationPart(typeof(Startup).Assembly);

            builder.Services.AddSingleton(lambdaOptions);

            builder.Services.AddSingleton<IRuntimeApiDataStore, RuntimeApiDataStore>();
            builder.Services.AddHttpContextAccessor();

            builder.Services.AddBlazoredModal();

            builder.WebHost
                    .SuppressStatusMessages(true)
                    .UseUrls(url);

            builder.Services.AddSignalR(options =>
            {
                options.MaximumReceiveMessageSize = 1024 * 1024 * 6;
            });

            var app = builder.Build();

            app.UseAntiforgery();
            app.UseDeveloperExceptionPage();

            app.MapControllers();
            app.UseStaticFiles();
            app.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode();

            await app.StartAsync(token);
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

            return app;
        }
#else
        public static async Task<IWebHost> StartWebTesterAsync(LocalLambdaOptions lambdaOptions, bool openWindow, CancellationToken token = default(CancellationToken))
        {
            var host = string.IsNullOrEmpty(lambdaOptions.Host)
                ? Constants.DEFAULT_HOST
                : lambdaOptions.Host;
            var port = lambdaOptions.Port ?? Constants.DEFAULT_PORT;
            var url = $"http://{host}:{port}";

            var contentPath = Directory.GetParent(Assembly.GetExecutingAssembly().Location).FullName;
            if (!Directory.Exists(contentPath))
            {
                contentPath = Path.GetFullPath(Directory.GetCurrentDirectory());
            }

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

            services.AddBlazoredModal();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseDeveloperExceptionPage();

            app.UseStaticFiles();

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapBlazorHub(o =>
                {
                    o.ApplicationMaxBufferSize = 6 * (1024 * 1024);
                    o.TransportMaxBufferSize = 6 * (1024 * 1024);
                });
                endpoints.MapFallbackToPage("/_Host");
            });
        }
#endif
    }
}
