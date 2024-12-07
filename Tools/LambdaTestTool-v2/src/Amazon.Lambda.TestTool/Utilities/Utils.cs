using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace Amazon.Lambda.TestTool.Utilities;

public static class Utils
{
    public const string DefaultConfigFile = "aws-lambda-tools-defaults.json";

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



    public static void PrintToolTitle(string productName)
    {
        var sb = new StringBuilder(productName);
        var version = Utils.DetermineToolVersion();
        if (!string.IsNullOrEmpty(version))
        {
            sb.Append($" ({version})");
        }

        Console.WriteLine(sb.ToString());
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

    public static string DetermineLaunchUrl(string host, int port, string defaultHost)
    {
        if (!IPAddress.TryParse(host, out _))
            // Any host other than explicit IP will be redirected to default host (i.e. localhost)
            return $"http://{defaultHost}:{port}";

        return $"http://{host}:{port}";
    }
}
