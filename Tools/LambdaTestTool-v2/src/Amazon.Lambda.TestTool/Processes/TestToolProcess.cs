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
        if (builder.Environment.IsProduction())
        {
            builder.Services.AddSingleton<IFileProvider>(new PhysicalFileProvider(wwwrootPath));
        }
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

        if (app.Environment.IsProduction())
        {
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(wwwrootPath)
            });
        }
        else
        {
            // nosemgrep: csharp.lang.security.stacktrace-disclosure.stacktrace-disclosure
            app.UseDeveloperExceptionPage();
            app.UseStaticFiles();
        }

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
