// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using System.Text.Json;
using Amazon.Lambda.TestTool.Configuration;

namespace Amazon.Lambda.TestTool.Utilities;

/// <summary>
/// A utility class that encapsulates common functionlity.
/// </summary>
public static class Utils
{
    /// <summary>
    /// Determines the version of the tool.
    /// </summary>
    public static string DetermineToolVersion()
    {
        const string unknownVersion = "Unknown";

        AssemblyInformationalVersionAttribute? attribute = null;
        try
        {
            var assembly = Assembly.GetEntryAssembly();
            if (assembly == null)
                return unknownVersion;
            attribute = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        }
        catch (Exception)
        {
            // ignored
        }

        var version = attribute?.InformationalVersion;

        // Check to see if the version has a git commit id suffix and if so remove it.
        if (version != null && version.IndexOf('+') != -1)
        {
            version = version.Substring(0, version.IndexOf('+'));
        }

        return version ?? unknownVersion;
    }

    /// <summary>
    /// If true it means the test tool was launched via an Aspire AppHost.
    /// </summary>
    internal static bool IsAspireHosted
    {
        get { return string.Equals(Environment.GetEnvironmentVariable("ASPIRE_HOSTED"), "true", StringComparison.OrdinalIgnoreCase); }
    }

    /// <summary>
    /// Attempt to pretty print the input string. If pretty print fails return back the input string in its original form.
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    public static string TryPrettyPrintJson(string? data)
    {
        try
        {
            if (string.IsNullOrEmpty(data))
                return string.Empty;

            var doc = JsonDocument.Parse(data);
            var prettyPrintJson = JsonSerializer.Serialize(doc, new JsonSerializerOptions()
            {
                WriteIndented = true
            });
            return prettyPrintJson;
        }
        catch (Exception)
        {
            return data ?? string.Empty;
        }
    }

    /// <summary>
    /// Configures the web application builder with necessary services, configuration, and logging setup.
    /// </summary>
    /// <param name="builder">The WebApplicationBuilder instance to be configured</param>
    /// <remarks>
    /// This method performs the following configurations:
    /// 1. Registers the current assembly as a singleton service
    /// 2. Registers ConfigurationSetup as a singleton service
    /// 3. Builds and applies custom configuration
    /// 4. Sets up logging providers with console output
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when ConfigurationSetup service cannot be resolved from the service provider
    /// </exception>
    public static void ConfigureWebApplicationBuilder(WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton(typeof(Assembly), typeof(ConfigurationSetup).Assembly);
        builder.Services.AddSingleton<ConfigurationSetup>();

        var configSetup = builder.Services.BuildServiceProvider().GetRequiredService<ConfigurationSetup>();
        var configuration = configSetup.GetConfiguration();
        builder.Configuration.AddConfiguration(configuration);

        builder.Logging.ClearProviders();
        builder.Logging.AddConfiguration(configuration.GetSection("Logging"));
        builder.Logging.AddConsole();
    }
}
