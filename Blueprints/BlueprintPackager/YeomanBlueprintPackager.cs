using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.IO.Compression;

using System.Text;
using System.Xml;

using Newtonsoft.Json;

namespace Packager
{
    public class YeomanBlueprintPackager : BaseBlueprintPackager
    {
        string _outputDirectory;

        string _yeomanGeneratorSourceLocation = Path.GetFullPath("../YeomanGenerator/generator-aws-lambda-dotnet");

        public YeomanBlueprintPackager(string blueprintRoot, string outputDirectory)
            :base(blueprintRoot)
        {
            _outputDirectory = outputDirectory;
        }        

        public void Execute()
        {
            WriteManifest();

            var copyDebugLocation = Path.Combine(_yeomanGeneratorSourceLocation, "Blueprints");
            if(Directory.Exists(copyDebugLocation))
                Directory.Delete(copyDebugLocation, true);

            CopyDirectory(this._blueprintRoot, copyDebugLocation);

            var copyDeploymentLocation = Path.Combine(_outputDirectory, "generator-aws-lambda-dotnet");
            if(Directory.Exists(copyDeploymentLocation))
                Directory.Delete(copyDeploymentLocation, true);

            CopyDirectory(_yeomanGeneratorSourceLocation, copyDeploymentLocation);
        }   

        private void CopyDirectory(string source, string target)
        {
            if(!Directory.Exists(target))
                new DirectoryInfo(target).Create();

            foreach(var file in Directory.GetFiles(source))
            {
                if(file.EndsWith("blueprint-manifest.json"))
                    continue;

                File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
            }

            foreach(var directory in Directory.GetDirectories(source))
            {
                CopyDirectory(Path.Combine(source, directory), Path.Combine(target, new DirectoryInfo(directory).Name));
            }
        }     

        private void WriteManifest()
        {
            var manifests = SearchForblueprintManifests();

            var blueprintManifests = new List<BlueprintManifest>();
            foreach(var manifest in manifests)
            {
                blueprintManifests.Add(JsonConvert.DeserializeObject<BlueprintManifest>(File.ReadAllText(manifest)));
            }

            blueprintManifests.Sort((x, y) =>
            {
                if (x.SortOrder == y.SortOrder)
                    return string.Compare(x.DisplayName, y.DisplayName);
                else if (x.SortOrder < y.SortOrder)
                    return -1;
                else
                    return 1;
            });

            var manifestJsPath = Path.Combine(_yeomanGeneratorSourceLocation, "app", "manifest.js");
            using (var manifestWriter = new StreamWriter(File.Create(manifestJsPath)))
            {
                manifestWriter.WriteLine("'use strict'");
                manifestWriter.WriteLine("");
                manifestWriter.WriteLine("module.exports = {");
                manifestWriter.WriteLine("    choices: [");

                for(int i = 0; i < blueprintManifests.Count; i++)
                {
                    var blueprintManifest = blueprintManifests[i];
                    var displayName = blueprintManifest.DisplayName;
                    if(blueprintManifest.HiddenTags.Contains("ServerlessProject"))
                        displayName += " (AWS Serverless)";

                    manifestWriter.WriteLine("        {");

                    manifestWriter.WriteLine($"            name: \"{displayName}\",");
                    manifestWriter.WriteLine($"            value: \"{blueprintManifest.SystemName}\",");
                    manifestWriter.WriteLine($"            description: \"{blueprintManifest.Description}\",");
                    manifestWriter.WriteLine($"            defaultAppName: \"{blueprintManifest.SystemName}\"");

                    if((i + 1) < manifests.Count)
                        manifestWriter.WriteLine("        },");
                    else
                        manifestWriter.WriteLine("        }");
                }                

                manifestWriter.WriteLine("    ]");                    
                manifestWriter.WriteLine("}");
            }
        }
    }
}