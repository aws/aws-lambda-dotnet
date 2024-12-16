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
