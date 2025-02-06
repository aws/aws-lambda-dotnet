// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

namespace Amazon.Lambda.TestTool.Configuration;

/// <summary>
/// Provides configuration setup functionality for the Lambda Test Tool.
/// </summary>
public static class ConfigurationSetup
{
    /// <summary>
    /// Configures essential services for the application, including logging and configuration.
    /// </summary>
    /// <param name="services">The service collection to configure services in.</param>
    /// <remarks>
    /// This method:
    /// 1. Retrieves the configuration from appsettings files
    /// 2. Sets up logging with console provider
    /// 3. Registers the configuration as a singleton service
    /// </remarks>
    public static void ConfigureServices(IServiceCollection services)
    {
        var configuration = GetConfiguration();

        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddConfiguration(configuration.GetSection("Logging"));
            builder.AddConsole();
        });

        services.AddSingleton<IConfiguration>(configuration);
    }

    /// <summary>
    /// Builds and returns the application configuration from JSON files.
    /// </summary>
    /// <returns>An IConfiguration instance containing application settings.</returns>
    /// <exception cref="InvalidOperationException">Thrown when unable to determine the assembly location.</exception>
    /// <remarks>
    /// The configuration is built using:
    /// 1. Base settings from appsettings.json
    /// 2. Environment-specific settings from appsettings.{environment}.json
    ///
    /// The environment is determined by the ASPNETCORE_ENVIRONMENT environment variable,
    /// defaulting to "Production" if not set.
    /// </remarks>
    private static IConfiguration GetConfiguration()
    {
        // Get the directory where your package DLL is located
        var assemblyLocation = typeof(ConfigurationSetup).Assembly.Location;
        var packageDirectory = Path.GetDirectoryName(assemblyLocation);

        if (string.IsNullOrEmpty(packageDirectory))
        {
            throw new InvalidOperationException("Unable to determine assembly location");
        }

        // Determine environment
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                          ?? "Production";

        // Build configuration
        var builder = new ConfigurationBuilder()
            .SetBasePath(packageDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true);

        return builder.Build();
    }
}
