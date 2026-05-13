// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;

namespace Amazon.Lambda.TestTool.Configuration;

/// <summary>
/// Handles the configuration setup for the Lambda Test Tool by loading settings from JSON files.
/// </summary>
/// <remarks>
/// This class uses dependency injection to receive an Assembly instance, allowing for better testability
/// and flexibility in determining the configuration file locations.
/// </remarks>
public class ConfigurationSetup(Assembly assembly)
{
    /// <summary>
    /// Retrieves the application configuration by loading settings from JSON configuration files.
    /// </summary>
    /// <returns>An IConfiguration instance containing the application settings.</returns>
    /// <exception cref="InvalidOperationException">Thrown when unable to determine the assembly location.</exception>
    /// <remarks>
    /// The method performs the following steps:
    /// 1. Locates the directory containing the assembly
    /// 2. Loads the base configuration from appsettings.json
    /// 3. Loads environment-specific configuration from appsettings.{environment}.json if available
    /// </remarks>
    public IConfiguration GetConfiguration()
    {
        // Get the directory where the assembly is located
        var assemblyLocation = assembly.Location;
        var packageDirectory = Path.GetDirectoryName(assemblyLocation)
                               ?? throw new InvalidOperationException("Unable to determine assembly location");

        // Construct path to configuration file
        var appsettingsPath = Path.Combine(packageDirectory, "appsettings.json");
        if (!File.Exists(appsettingsPath))
        {
            Console.WriteLine($"Warning: appsettings.json not found at {appsettingsPath}");
        }

        // Determine the current environment
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";

        // Build and return the configuration
        var builder = new ConfigurationBuilder()
            .SetBasePath(packageDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true);

        return builder.Build();
    }
}
