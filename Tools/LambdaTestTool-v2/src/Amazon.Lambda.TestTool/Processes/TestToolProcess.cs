using Amazon.Lambda.TestTool.Components;
using Amazon.Lambda.TestTool.Models;
using Amazon.Lambda.TestTool.Services;
using Blazored.Modal;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;

namespace Amazon.Lambda.TestTool.Processes;

public class TestToolProcess
{
    public required IServiceProvider Services { get; init; }

    public required Task RunningTask { get; init; }

    public required string ServiceUrl { get; init; }

    public required CancellationTokenSource CancellationTokenSource { get; init; }

    private TestToolProcess()
    {

    }

    public static TestToolProcess Startup(ApplicationOptions lambdaOptions)
    {
        var builder = WebApplication.CreateBuilder();

        builder.Services.AddSingleton<IRuntimeApiDataStore, RuntimeApiDataStore>();

        // Add services to the container.
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents()
            .AddHubOptions(options => options.MaximumReceiveMessageSize = null);

        builder.Services.AddHttpContextAccessor();

        builder.Services.AddBlazoredModal();

        builder.Services.AddTransient<IPostConfigureOptions<StaticFileOptions>, ConfigureStaticFilesOptions>();

        builder.Services.AddSingleton(lambdaOptions);

        var serviceUrl = $"http://{lambdaOptions.Host}:{lambdaOptions.Port}";
        builder.WebHost.UseUrls(serviceUrl);
        builder.WebHost.SuppressStatusMessages(true);

        var app = builder.Build();

        app.UseDeveloperExceptionPage();

        app.UseStaticFiles();
        app.UseAntiforgery();

        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();

        _ = new LambdaRuntimeAPI(app, app.Services.GetService<IRuntimeApiDataStore>()!);

        var cancellationTokenSource = new CancellationTokenSource();
        var runTask = app.RunAsync(cancellationTokenSource.Token);

        var startup = new TestToolProcess
        {
            Services = app.Services,
            RunningTask = runTask,
            CancellationTokenSource = cancellationTokenSource,
            ServiceUrl = serviceUrl
        };

        return startup;
    }

    internal class ConfigureStaticFilesOptions : IPostConfigureOptions<StaticFileOptions>
    {
        public ConfigureStaticFilesOptions(IWebHostEnvironment environment)
        {
            Environment = environment;
        }

        public IWebHostEnvironment Environment { get; }

        public void PostConfigure(string? name, StaticFileOptions options)
        {
            name = name ?? throw new ArgumentNullException(nameof(name));
            options = options ?? throw new ArgumentNullException(nameof(options));

            if (name != Options.DefaultName)
            {
                return;
            }

            var fileProvider = new ManifestEmbeddedFileProvider(typeof(Program).Assembly, "wwwroot");
            Environment.WebRootFileProvider = new CompositeFileProvider(fileProvider, Environment.WebRootFileProvider);
        }
    }
}
