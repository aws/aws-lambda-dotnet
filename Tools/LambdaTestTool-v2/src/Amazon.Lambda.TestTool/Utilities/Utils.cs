// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using System.Text.Json;

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
            var assembly = typeof(Utils).Assembly;
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

    public static string GenerateToolInfoJson()
    {
        var stream = new MemoryStream();
        Utf8JsonWriter utf8JsonWriter = new Utf8JsonWriter(stream, options: new JsonWriterOptions()
        {
            Indented = false
        });
        utf8JsonWriter.WriteStartObject();
        utf8JsonWriter.WriteString("version", Utilities.Utils.DetermineToolVersion());
        utf8JsonWriter.WriteString("install-path", Directory.GetParent(typeof(Utils).Assembly.Location)!.FullName);
        utf8JsonWriter.WriteEndObject();
        utf8JsonWriter.Flush();

        stream.Position = 0;
        return new StreamReader(stream).ReadToEnd();
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
}
