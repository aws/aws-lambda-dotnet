using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Xml;
using System.Text;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Packager
{
    public class UpdatePackageReferenceVersions
    {
        const string MicrosoftAspNetCoreAppVersion = "2.1.4";
        const string AWSSDK_VERSION_MANIFEST = "https://raw.githubusercontent.com/aws/aws-sdk-net/master/generator/ServiceModels/_sdk-versions.json";

        string BlueprintRoot { get; }

        public UpdatePackageReferenceVersions(string blueprintRoot)
        {
            this.BlueprintRoot = blueprintRoot;
        }

        public void Execute()
        {
            var versions = LoadKnownVersions();
            ProcessBlueprints(versions);
        }

        public void ProcessBlueprints(IDictionary<string, string> versions)
        {
            Console.WriteLine("Updating versions in blueprints");

            var projectFiles = GetProjectFiles(this.BlueprintRoot);
            foreach (var projectfile in projectFiles)
            {
                Console.WriteLine($"Processing {projectfile}");
                var xdoc = new XmlDocument();
                xdoc.Load(projectfile);

                bool changed = false;
                var packageReferenceNodes = xdoc.SelectNodes("//ItemGroup/PackageReference");
                foreach(XmlElement packageReferenceNode in packageReferenceNodes)
                {
                    var packageId = packageReferenceNode.GetAttribute("Include");
                    var blueprintVersion = packageReferenceNode.GetAttribute("Version");

                    if (string.IsNullOrEmpty(packageId) || string.IsNullOrEmpty(blueprintVersion) || !versions.ContainsKey(packageId))
                        continue;

                    var latestVersion = versions[packageId];
                    if (string.Equals(latestVersion, blueprintVersion))
                        continue;

                    Console.WriteLine($"\tUpdated {packageId}: {blueprintVersion} -> {latestVersion}");
                    changed = true;
                    packageReferenceNode.SetAttribute("Version", latestVersion);
                }

                if(changed)
                {
                    xdoc.Save(projectfile);
                }
            }
        }

        public IDictionary<string, string> LoadKnownVersions()
        {
            Console.WriteLine("Looking up Lambda package version numbers");
            var versions = new Dictionary<string, string>();

            versions["Microsoft.AspNetCore.App"] = MicrosoftAspNetCoreAppVersion;

            var librariesDir = Path.GetFullPath(Path.Combine(this.BlueprintRoot, "../../../Libraries/src"));

            var projectFiles = GetProjectFiles(librariesDir);
            foreach(var projectfile in projectFiles)
            {
                var xdoc = new XmlDocument();
                xdoc.Load(projectfile);

                var packageId = xdoc.SelectSingleNode("//PropertyGroup/PackageId")?.InnerText;
                var versionPrefix = xdoc.SelectSingleNode("//PropertyGroup/VersionPrefix")?.InnerText;

                if(!string.IsNullOrEmpty(packageId) && !string.IsNullOrEmpty(versionPrefix))
                {
                    Console.WriteLine($"\t{packageId}: {versionPrefix}");
                    versions[packageId] = versionPrefix;
                }
            }
            
            try
            {
                string jsonContent;
                using (var client = new HttpClient())
                {
                    jsonContent = client.GetStringAsync(AWSSDK_VERSION_MANIFEST).Result;
                }

                var root = JsonConvert.DeserializeObject(jsonContent) as JObject;
                var serviceVersions = root["ServiceVersions"] as JObject;
                foreach(var service in serviceVersions.Properties())
                {
                    
                    var packageId = "AWSSDK." + service.Name;
                    var version = service.Value["Version"]?.ToString();

                    versions[packageId] = version;
                }
            }
            catch(Exception e)
            {
                if (e is AggregateException)
                    e = e.InnerException;

                Console.WriteLine($"Error fetching version numbers from AWS SDK for .NET: {e.Message}");
            }

            return versions;
        }

        public static IEnumerable<string> GetProjectFiles(string directory)
        {
            var projectFiles = Directory.GetFiles(directory, "*.*", SearchOption.AllDirectories).Where(s => s.EndsWith(".csproj") || s.EndsWith(".fsproj"));
            return projectFiles;
        }
    }
}
