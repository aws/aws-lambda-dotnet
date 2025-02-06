// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

namespace Amazon.Lambda.TestTool.Configuration;

/// <summary>
/// Provides functionality to set up and retrieve configuration settings for the Lambda Test Tool.
/// </summary>
/// <remarks>
/// This class handles the configuration setup by loading settings from JSON files located in the assembly's directory.
/// It supports both base configuration (appsettings.json) and environment-specific configuration files.
/// </remarks>
public static class ConfigurationSetup
{
    /// <summary>
    /// Retrieves the application configuration by loading settings from JSON configuration files.
    /// </summary>
    /// <returns>An <see cref="IConfiguration"/> instance containing the application settings.</returns>
    /// <exception cref="InvalidOperationException">Thrown when unable to determine the assembly's location.</exception>
    /// <remarks>
    /// The method performs the following:
    /// <list type="bullet">
    /// <item>
    ///     <description>Locates the directory containing the assembly</description>
    /// </item>
    /// <item>
    ///     <description>Loads the base configuration from appsettings.json</description>
    /// </item>
    /// <item>
    ///     <description>Loads environment-specific configuration from appsettings.{environment}.json if available</description>
    /// </item>
    /// </list>
    /// The environment is determined by the ASPNETCORE_ENVIRONMENT environment variable, defaulting to "Production" if not set.
    /// </remarks>
    /// <example>
    /// Usage example:
    /// <code>
    /// IConfiguration configuration = ConfigurationSetup.GetConfiguration();
    /// var setting = configuration["SectionName:SettingName"];
    /// </code>
    /// </example>
    public static IConfiguration GetConfiguration()
    {
        // Get the directory where the assembly is located
        var assemblyLocation = typeof(ConfigurationSetup).Assembly.Location;
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
