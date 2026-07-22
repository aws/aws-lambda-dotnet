// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.TestTool.Commands.Settings;
using Amazon.Lambda.TestTool.Components;
using Amazon.Lambda.TestTool.Models;
using Amazon.Lambda.TestTool.Services;
using Amazon.Lambda.TestTool.Services.DurableExecution;
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
            // Pin the content root to the tool's install directory rather than the current
            // working directory. As an installed global tool the process is launched from an
            // arbitrary cwd, and the default content root is the cwd. On .NET 9+ the framework
            // and scoped-CSS assets are served by MapStaticAssets from the WebRootFileProvider,
            // which is derived from the content root (contentRoot/wwwroot). If the content root
            // is a foreign cwd, that provider points at a nonexistent wwwroot and MapStaticAssets
            // returns empty (0-byte) 200 responses, leaving the Blazor UI non-interactive.
            ContentRootPath = AppContext.BaseDirectory
        });

        Utils.ConfigureWebApplicationBuilder(builder);

        // Static web assets (the *.staticwebassets.runtime.json manifest) map two kinds of files
        // that do NOT physically live under wwwroot when running from a build output (dotnet run /
        // dotnet build): the Blazor framework files (_framework/blazor.web.js) and Razor class
        // library content such as BlazorMonaco's _content/BlazorMonaco/** editor assets. Both live
        // in the NuGet cache and are surfaced via that manifest. ASP.NET Core only composes the
        // manifest into the WebRootFileProvider automatically in the Development environment, but
        // this tool runs in the Production environment by default, so without the call below the
        // WebRootFileProvider is a bare wwwroot provider and those assets are unreachable.
        //
        // On .NET 9+ that leaves MapStaticAssets (added below) unable to find the framework files —
        // it serves an empty (0-byte) HTTP 200, window.Blazor is never defined, and the whole UI is
        // non-interactive. On .NET 8 the framework files are served by Blazor's own middleware, but
        // the RCL _content/** assets are served through UseStaticFiles + WebRootFileProvider, so
        // without the manifest the Monaco editor assets 404 and the code editor never initializes
        // (e.g. selecting an example request cannot populate the input). Either way the fix is the
        // same, so this runs on all target frameworks.
        //
        // When running as an installed global tool the manifest is absent (the framework and RCL
        // content are published directly into wwwroot instead), so this is a harmless no-op there.
        builder.WebHost.UseStaticWebAssets();

        builder.Services.AddSingleton<IRuntimeApiDataStoreManager, RuntimeApiDataStoreManager>();
        builder.Services.AddSingleton<IThemeService, ThemeService>();

        if (settings.DurableExecution)
        {
            builder.Services.AddSingleton(new DurableExecutionStore(settings.DurableTimeSkip));
            builder.Services.AddSingleton(sp => new DurableExecutionDriver(
                sp.GetRequiredService<DurableExecutionStore>(),
                sp.GetRequiredService<IRuntimeApiDataStoreManager>()));
        }
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

        // Serve classic static files from the tool's install directory (wwwroot). This is the
        // authoritative provider for an installed global tool, where every asset — app.css, the
        // Blazor framework files, and RCL _content/** (e.g. BlazorMonaco) — is published directly
        // into wwwroot. Pinning an explicit provider (rather than the default WebRootFileProvider)
        // also avoids depending on the static-web-assets manifest, whose absolute build-machine
        // paths would not resolve on another machine.
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = wwwrootFileProvider
        });

#if !NET9_0_OR_GREATER
        // On .NET 8 there is no MapStaticAssets endpoint pipeline (added below for net9+), and the
        // explicit provider above sees only the physical wwwroot. When running from a build output
        // (dotnet run / dotnet build), RCL _content/** assets are NOT physically in wwwroot — they
        // are surfaced through the static-web-assets manifest that UseStaticWebAssets() composes
        // into the WebRootFileProvider. Without a second pass over that provider, BlazorMonaco's
        // _content/BlazorMonaco/** editor assets 404 and the code editor never initializes (so, for
        // example, selecting an example request cannot populate the input). Serve from the
        // WebRootFileProvider as well to cover that case. For an installed tool the WebRootFile
        // provider resolves to the same wwwroot as above, so this is a harmless second lookup.
        if (app.Environment.WebRootFileProvider is not null)
        {
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = app.Environment.WebRootFileProvider
            });
        }
#endif

#if NET9_0_OR_GREATER
        // On .NET 9+ the Blazor framework files (_framework/blazor.web.js) and the scoped-CSS
        // bundle (Amazon.Lambda.TestTool.styles.css) are served through the endpoint-routing
        // static assets API backed by the *.staticwebassets.endpoints.json manifest, not the
        // classic static-files middleware above. Without this call those assets return 404, so
        // window.Blazor is never defined and the interactive server circuit is never established,
        // leaving the entire Blazor UI non-interactive. The bytes are resolved from the web host's
        // WebRootFileProvider, which is why UseStaticWebAssets() is also required above (see the
        // comment there). Guarded so net8.0 (which serves these assets via the classic static-web-
        // assets middleware) keeps its existing behavior.
        //
        // MapStaticAssets() throws if the manifest is absent. That manifest is named after the
        // entry assembly, so when Startup is invoked from within a test host (unit tests call it
        // directly) the tool's manifest isn't next to the running 'testhost' assembly. Only map
        // static assets when the expected manifest exists — the running tool has it; the test host
        // does not (and those tests only exercise the Runtime API, not the Blazor assets).
        var staticAssetsManifest = Path.Combine(
            AppContext.BaseDirectory,
            $"{System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name}.staticwebassets.endpoints.json");
        if (File.Exists(staticAssetsManifest))
        {
            app.MapStaticAssets();
        }
#endif

        app.UseAntiforgery();

        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();

        LambdaRuntimeApi.SetupLambdaRuntimeApiEndpoints(app);

        if (settings.DurableExecution)
        {
            DurableServiceApi.SetupDurableServiceEndpoints(app);
        }

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
