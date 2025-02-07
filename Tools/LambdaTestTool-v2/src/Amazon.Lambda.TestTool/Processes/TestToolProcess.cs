// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using Amazon.Lambda.TestTool.Commands.Settings;
using Amazon.Lambda.TestTool.Components;
using Amazon.Lambda.TestTool.Configuration;
using Amazon.Lambda.TestTool.Services;
using Amazon.Lambda.TestTool.Services.IO;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;

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

        builder.Services.AddSingleton(typeof(Assembly), typeof(ConfigurationSetup).Assembly);
        builder.Services.AddSingleton<ConfigurationSetup>();

        var configSetup = builder.Services.BuildServiceProvider().GetRequiredService<ConfigurationSetup>();
        var configuration = configSetup.GetConfiguration();
        builder.Configuration.AddConfiguration(configuration);

        builder.Logging.ClearProviders();
        builder.Logging.AddConfiguration(configuration.GetSection("Logging"));
        builder.Logging.AddConsole();

        builder.Services.AddSingleton<IRuntimeApiDataStoreManager, RuntimeApiDataStoreManager>();
        builder.Services.AddSingleton<IThemeService, ThemeService>();

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

        var serviceUrl = $"http://{settings.LambdaEmulatorHost}:{settings.LambdaEmulatorPort}";
        builder.WebHost.UseUrls(serviceUrl);
        builder.WebHost.SuppressStatusMessages(true);

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
            ServiceUrl = serviceUrl
        };

        return startup;
    }
}
