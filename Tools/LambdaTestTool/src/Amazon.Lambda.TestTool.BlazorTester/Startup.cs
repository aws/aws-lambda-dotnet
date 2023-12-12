using Amazon.Lambda.TestTool.BlazorTester.Services;
using Blazored.Modal;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using System;
using System.Diagnostics;
using System.IO;
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
                    .AddHubOptions(options => options.MaximumReceiveMessageSize = null)
                    .AddHubOptions(options => options.ClientTimeoutInterval = TimeSpan.FromMinutes(10));
            services.AddHttpContextAccessor();

            services.AddBlazoredModal();

#if NET8_0_OR_GREATER
            // Starting with .NET 8 how the IFileProvider is configured for Blazor
            // to serve the Blazor embedded content was changed. The previous version
            // of using the PostConfigure no longer works because the "o.FileProvider" is null.
            // Using this IPostConfigureOptions<StaticFileOptions> service approach does not
            // work in .NET versions before .NET 8.
            // For further context checkout this GitHub issue.
            // https://github.com/dotnet/aspnetcore/issues/51794
            services.AddTransient<IPostConfigureOptions<StaticFileOptions>, ConfigureStaticFilesOptions>();
#else
            services.AddOptions<StaticFileOptions>()
                .PostConfigure(o =>
                {
                    var fileProvider = new ManifestEmbeddedFileProvider(typeof(Startup).Assembly, "wwwroot");

                    // Make sure we don't remove the existing file providers (blazor needs this)
                    o.FileProvider = new CompositeFileProvider(o.FileProvider, fileProvider);
                });
#endif
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

#if NET8_0_OR_GREATER
        internal class ConfigureStaticFilesOptions : IPostConfigureOptions<StaticFileOptions>
        {
            public ConfigureStaticFilesOptions(IWebHostEnvironment environment)
            {
                Environment = environment;
            }

            public IWebHostEnvironment Environment { get; }

            public void PostConfigure(string name, StaticFileOptions options)
            {
                name = name ?? throw new ArgumentNullException(nameof(name));
                options = options ?? throw new ArgumentNullException(nameof(options));

                if (name != Options.DefaultName)
                {
                    return;
                }

                var fileProvider = new ManifestEmbeddedFileProvider(typeof(Startup).Assembly, "wwwroot");
                Environment.WebRootFileProvider = fileProvider;
            }
        }
#endif
    }
}
