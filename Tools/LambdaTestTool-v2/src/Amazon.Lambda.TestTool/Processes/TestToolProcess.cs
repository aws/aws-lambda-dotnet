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

#if NET9_0_OR_GREATER
        // On .NET 9+ the Blazor framework files (_framework/blazor.web.js) and the scoped-CSS
        // bundle (Amazon.Lambda.TestTool.styles.css) are served by the endpoint-routing static
        // assets pipeline (app.MapStaticAssets, added below), which reads the bytes from the web
        // host's WebRootFileProvider. When running from a build output (dotnet run / dotnet build)
        // those framework assets do not physically live under wwwroot; they live in the NuGet
        // cache and are mapped in via the *.staticwebassets.runtime.json manifest. ASP.NET Core
        // only composes that manifest into the WebRootFileProvider automatically in the
        // Development environment, but this tool runs in the Production environment by default, so
        // without the call below the WebRootFileProvider is a bare wwwroot provider, MapStaticAssets
        // cannot find the framework files, and it serves an empty (0-byte) HTTP 200 response. That
        // leaves window.Blazor undefined and the interactive server circuit never starts, so the
        // whole UI renders as non-interactive. Calling UseStaticWebAssets forces the manifest to be
        // composed regardless of environment. When running as an installed global tool the manifest
        // is absent (the framework files are published directly into wwwroot instead), so this is a
        // harmless no-op there and MapStaticAssets serves the files straight from wwwroot.
        builder.WebHost.UseStaticWebAssets();
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
        // leaving the entire Blazor UI non-interactive. The bytes are resolved from the web host's
        // WebRootFileProvider, which is why UseStaticWebAssets() is also required above (see the
        // comment there). Guarded so net8.0 (which serves these assets via the classic static-web-
        // assets middleware) keeps its existing behavior.
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
