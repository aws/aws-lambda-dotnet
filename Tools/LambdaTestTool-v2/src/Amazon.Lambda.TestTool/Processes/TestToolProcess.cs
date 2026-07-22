// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.TestTool.Commands.Settings;
using Amazon.Lambda.TestTool.Components;
using Amazon.Lambda.TestTool.Models;
using Amazon.Lambda.TestTool.Services;
using Amazon.Lambda.TestTool.Services.IO;
using Amazon.Lambda.TestTool.Utilities;
using Microsoft.Extensions.FileProviders;

namespace Amazon.Lambda.TestTool.Processes;

/// <summary>
/// A process that runs the local Lambda Runtime API and its web interface.
/// </summary>
public class TestToolProcess
{
    /// <summary>
    /// The service provider that will contain all the registered services.
    /// </summary>
    public required IServiceProvider Services { get; init; }

    /// <summary>
    /// The Lambda Runtime API task that was started.
    /// </summary>
    public required Task RunningTask { get; init; }

    /// <summary>
    /// The endpoint of the Lambda Runtime API.
    /// </summary>
    public required string ServiceUrl { get; init; }

    /// <summary>
    /// Creates the Web Application and runs it in the background.
    /// </summary>
    public static TestToolProcess Startup(RunCommandSettings settings, CancellationToken cancellationToken = default)
    {
        var builder = WebApplication.CreateBuilder();

        Utils.ConfigureWebApplicationBuilder(builder);

#if NET9_0_OR_GREATER
        // On .NET 9+ framework assets are served through MapStaticAssets (see below). When the
        // build-time static web assets manifest is in use (dotnet build / dotnet run), ASP.NET Core
        // attaches a development-time runtime-patching handler that probes each asset via the app's
        // physical WebRootFileProvider (wwwroot) to detect local edits. The framework assets
        // (_framework/*) and the scoped-CSS bundle do not physically exist under wwwroot, so that
        // probe throws FileNotFoundException and the assets return HTTP 500. Disabling the runtime
        // reload makes MapStaticAssets serve directly from the manifest's content roots, which is
        // exactly what the installed global tool (publish manifest) already does. This tool manages
        // its own static file serving, so runtime hot-reload of wwwroot is not needed.
        builder.Configuration["ReloadStaticAssetsAtRuntime"] = "false";
#endif

        builder.Services.AddSingleton<IRuntimeApiDataStoreManager, RuntimeApiDataStoreManager>();
        builder.Services.AddSingleton<IThemeService, ThemeService>();
        builder.Services.AddSingleton<ILambdaClient, LambdaClient>();
        builder.Services.AddSingleton<ILambdaRequestManager, LambdaRequestManager>();

        builder.Services.Configure<LambdaOptions>(options =>
        {
            options.Endpoint = $"http://{settings.LambdaEmulatorHost}:{settings.LambdaEmulatorPort}";
            options.ConfigStoragePath = settings.ConfigStoragePath;
        });


        // Add services to the container.
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents()
            .AddHubOptions(options => options.MaximumReceiveMessageSize = null);

        builder.Services.AddHttpContextAccessor();

        var wwwrootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        var wwwrootFileProvider = new PhysicalFileProvider(wwwrootPath);
        builder.Services.AddSingleton<IFileProvider>(wwwrootFileProvider);
        builder.Services.AddSingleton<IDirectoryManager, DirectoryManager>();

        var serviceHttp = $"http://{settings.LambdaEmulatorHost}:{settings.LambdaEmulatorPort}";

        string? serviceHttps = null;

        if (settings.LambdaEmulatorHttpsPort.HasValue)
        {
            serviceHttps = $"https://{settings.LambdaEmulatorHost}:{settings.LambdaEmulatorHttpsPort}";
            builder.WebHost.UseUrls(serviceHttp, serviceHttps);
        }
        else
        {
            builder.WebHost.UseUrls(serviceHttp);
        }
        
        builder.WebHost.SuppressStatusMessages(true);

        builder.Services.AddSingleton<IGlobalSettingsRepository, FileSettingsRepository>();
        builder.Services.AddSingleton<IGlobalSettingsService, GlobalSettingsService>();

        var app = builder.Build();

        if (!app.Environment.IsProduction())
        {
            // nosemgrep: csharp.lang.security.stacktrace-disclosure.stacktrace-disclosure
            app.UseDeveloperExceptionPage();
        }

        // Always use the explicit file provider to serve static files from the tool's install
        // directory. Without this, non-Production environments attempt to use the static web
        // assets manifest which contains absolute paths from the build machine and will fail
        // when running as an installed global tool on a different machine.
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = wwwrootFileProvider
        });

#if NET9_0_OR_GREATER
        // On .NET 9+ the Blazor framework files (_framework/blazor.web.js) and the scoped-CSS
        // bundle (Amazon.Lambda.TestTool.styles.css) are served through the endpoint-routing
        // static assets API backed by the *.staticwebassets.endpoints.json manifest, not the
        // classic static-files middleware above. Without this call those assets return 404, so
        // window.Blazor is never defined and the interactive server circuit is never established,
        // leaving the entire Blazor UI non-interactive. This manifest is generated relative to the
        // published output, so it works for the installed global-tool scenario. Guarded so net8.0
        // (which serves these assets via the wwwroot provider above) keeps its existing behavior.
        app.MapStaticAssets();
#endif

        app.UseAntiforgery();

        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();

        LambdaRuntimeApi.SetupLambdaRuntimeApiEndpoints(app);

        var runTask = app.RunAsync(cancellationToken);

        var startup = new TestToolProcess
        {
            Services = app.Services,
            RunningTask = runTask,
            ServiceUrl = serviceHttps ?? serviceHttp
        };

        return startup;
    }
}
