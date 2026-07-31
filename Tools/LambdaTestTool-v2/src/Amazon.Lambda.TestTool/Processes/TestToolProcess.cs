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
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            // Pin the content root to the install dir. As a global tool the process launches from
            // an arbitrary cwd, and WebRootFileProvider (= contentRoot/wwwroot) defaults to it. A
            // foreign cwd points that provider at a nonexistent wwwroot, so assets 404 / serve empty.
            ContentRootPath = AppContext.BaseDirectory
        });

        Utils.ConfigureWebApplicationBuilder(builder);

        // Under `dotnet run`, the Blazor framework files (_framework/*) and RCL content
        // (_content/BlazorMonaco/**) don't live in wwwroot — they're surfaced from the NuGet cache
        // via the static-web-assets manifest. ASP.NET Core only composes that manifest into
        // WebRootFileProvider automatically in Development, but this tool runs as Production, so we
        // compose it explicitly here. Without it net9+ serves empty framework files (UI dead) and
        // net8 404s the Monaco editor assets. No-op for the installed tool (everything's in wwwroot).
        builder.WebHost.UseStaticWebAssets();

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

        // --- Static assets: a base layer for every TFM, then per-TFM handling for manifest assets ---

        // Base layer (all TFMs, all deployments): classic wwwroot files (app.css, images, favicon).
        // Authoritative for the installed tool, where every asset is published into wwwroot. Pinned to
        // an explicit provider so it never depends on the manifest's absolute build-machine paths.
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = wwwrootFileProvider
        });

        // Manifest-mapped assets (framework files, scoped CSS, RCL _content/** e.g. Monaco) aren't in
        // wwwroot under `dotnet run` — they're surfaced via the static-web-assets manifest that
        // UseStaticWebAssets() composed above. How they're served differs by TFM:
#if NET9_0_OR_GREATER
        // net9+: framework files + scoped CSS are served through the endpoint-routing static-assets
        // API, not the classic middleware above. Without this, window.Blazor is never defined and the
        // whole UI is non-interactive. MapStaticAssets() throws if the manifest (named after the entry
        // assembly) is absent, as under a test host — so only map when it exists.
        var staticAssetsManifest = Path.Combine(
            AppContext.BaseDirectory,
            $"{System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name}.staticwebassets.endpoints.json");
        if (File.Exists(staticAssetsManifest))
        {
            app.MapStaticAssets();
        }
#else
        // net8: no MapStaticAssets, and the base provider sees only the physical wwwroot. Serve the
        // manifest-composed WebRootFileProvider too, or the RCL _content/** (Monaco) assets 404 under
        // `dotnet run`. For the installed tool this resolves to the same wwwroot — a harmless second pass.
        if (app.Environment.WebRootFileProvider is not null)
        {
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = app.Environment.WebRootFileProvider
            });
        }
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
