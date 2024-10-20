using Amazon.Lambda.Annotations.SourceGenerator.FileIO;
using System;
using System.Xml;

namespace Amazon.Lambda.Annotations.SourceGenerator
{
    /// <summary>
    /// Utilities for working with project (.csproj) files
    /// </summary>
    internal class ProjectFileHandler
    {
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
    }
}
