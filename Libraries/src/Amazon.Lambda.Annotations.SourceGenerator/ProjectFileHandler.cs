using Amazon.Lambda.Annotations.SourceGenerator.FileIO;
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.Xml;

namespace Amazon.Lambda.Annotations.SourceGenerator
{
    /// <summary>
    /// Utilities for working with project (.csproj) files
    /// </summary>
    public class ProjectFileHandler
    {
        /// <summary>
        /// Timeout for the `dotnet msbuild -getProperty` command we use to determine target framework
        /// </summary>
        private const int DotnetMsbuildTimeoutMs = 5000;

        /// <summary>
        /// MSBuild property to determine if the project has opted out of the CFN template description
        /// </summary>
        private const string OptOutNodeXpath = "//PropertyGroup/AWSSuppressLambdaAnnotationsTelemetry";

        /// <summary>
        /// Determines if the project has opted out of any Lambda Annotations telemetry
        /// </summary>
        /// <param name="projectFilePath">Path to a .csproj file</param>
        /// <param name="fileManager">FileManager instance used to read the csproj contents</param>
        /// <returns>True if opted out of telemetry, false otherwise</returns>
        public static bool IsTelemetrySuppressed(string projectFilePath, IFileManager fileManager)
        {
            // If we were unable to find the csproj file, treat as if not opted out
            if (string.IsNullOrEmpty(projectFilePath))
            {
                return false;
            }

            var projectfileContent = fileManager.ReadAllText(projectFilePath);

            var xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(projectfileContent);

            var optOutNode = xmlDoc.SelectSingleNode(OptOutNodeXpath) as XmlElement;
            
            if (optOutNode != null && !string.IsNullOrEmpty(optOutNode.InnerText))
            {
                if (string.Equals("true", optOutNode.InnerText, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
        
        /// <summary>
        /// Attempts to determine a single target framework moniker from a .csproj file
        /// </summary>
        /// <param name="projectFilePath">Path to a .csproj file</param>
        /// <param name="outTargetFramework">Output variable for the target framework moniker</param>
        /// <returns>True if a single TFM was determined, false otherwise</returns>
        public static bool TryDetermineTargetFramework(string projectFilePath, out string outTargetFramework)
        {
            outTargetFramework = null;
            JObject parsedJson;
            try
            {
                var process = new Process()
                {
                    StartInfo = new ProcessStartInfo()
                    {
                        FileName = "dotnet",
                        Arguments = $"msbuild {projectFilePath} -getProperty:TargetFramework,TargetFrameworks",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        WindowStyle = ProcessWindowStyle.Hidden
                    }
                };

                process.Start();
                var outputJson = process.StandardOutput.ReadToEnd();
                var hasExited = process.WaitForExit(DotnetMsbuildTimeoutMs);

                // If it hasn't completed in the specified timeout, stop the process and give up
                if (!hasExited) 
                {
                    process.Kill();
                    return false;
                }

                // If it has completed but unsuccessfully, give up
                if (process.ExitCode != 0)
                {
                    return false;
                }

                if (string.IsNullOrEmpty(outputJson))
                {
                    return false;
                }

                parsedJson = JObject.Parse(outputJson);
            }
            catch (Exception)
            {
                // swallow any exceptions related to `dotnet msbuild`, Generator
                // will fall back to allowing the user to specify the target framework
                // via the global property
                return false;
            }

            // If there isn't the Properties key in the JSON, we failed to read values for either
            if (!parsedJson.ContainsKey("Properties"))
            {
                return false;
            }

            // If <TargetFramework> (singular) is specified, that takes precedence over <TargetFrameworks> (plural)
            var targetFramework = parsedJson["Properties"]?["TargetFramework"]?.ToString();

            if (!string.IsNullOrEmpty(targetFramework))
            {
                outTargetFramework = targetFramework;
                return true;
            }
            
            
            // Otherwise fallback to <TargetFrameworks> (plural)
            var possibleList = parsedJson["Properties"]?["TargetFrameworks"]?.ToString();

            // But only use it if it contains a single entry,
            // otherwise we don't know at this point which entry is being built 
            if (!string.IsNullOrEmpty(possibleList) && !possibleList.Contains(";"))
            {
                outTargetFramework = possibleList;
                return true;
            }
            
            return false;
        }
    }
}
