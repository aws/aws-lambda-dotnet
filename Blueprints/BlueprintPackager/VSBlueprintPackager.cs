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
    public class VSBlueprintPackager : BaseBlueprintPackager
    {
        
        string _outputDirectory;

        public VSBlueprintPackager(string blueprintRoot, string outputDirectory)
            : base(blueprintRoot)
        {
            _outputDirectory = Path.Combine(outputDirectory, "VisualStudioBlueprints");
            if(!Directory.Exists(_outputDirectory))
                new DirectoryInfo(_outputDirectory).Create();
        }

        public void Execute()
        {          
            var manifests = SearchForblueprintManifests();

            var vsblueprintManifestPath = Path.Combine(_outputDirectory, "vs-lambda-blueprint-manifest.xml");

            using (var manifestStream = File.Create(vsblueprintManifestPath))
            using (var vsManifestWriter = XmlWriter.Create(manifestStream, new XmlWriterSettings {Indent = true }))
            {
                vsManifestWriter.WriteStartElement("BlueprintManifest");
                vsManifestWriter.WriteElementString("ManifestVersion", "1");

                vsManifestWriter.WriteStartElement("Blueprints");
                foreach (var manifest in manifests)
                {
                    ProcessblueprintManifest(vsManifestWriter, manifest);
                }
                vsManifestWriter.WriteEndElement();
                vsManifestWriter.WriteEndElement();
            }
        }

        private void ProcessblueprintManifest(XmlWriter vsManifestWriter, string manifest)
        {
            Console.WriteLine($"Processing blueprint manifest {manifest}");

            var srcZip = Path.Combine(Path.GetTempPath(), "src.zip");
            {
                var srcSource = Path.Combine(Directory.GetParent(manifest).FullName, "src");
                if (File.Exists(srcZip))
                {
                    File.Delete(srcZip);
                }
                ZipFile.CreateFromDirectory(srcSource, srcZip);
            }


            var testZip = Path.Combine(Path.GetTempPath(), "test.zip");
            {
                var testSource = Path.Combine(Directory.GetParent(manifest).FullName, "test");
                if (File.Exists(testZip))
                {
                    File.Delete(testZip);
                }

                ZipFile.CreateFromDirectory(testSource, testZip);
            }

            var blueprintZip = Path.Combine(_outputDirectory, Directory.GetParent(manifest).Name + ".zip");
            using (var stream = File.Create(blueprintZip))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                archive.CreateEntryFromFile(srcZip, "src.zip");
                archive.CreateEntryFromFile(testZip, "test.zip");
            }

            var blueprintManifest = JsonConvert.DeserializeObject<BlueprintManifest>(File.ReadAllText(manifest));
            vsManifestWriter.WriteStartElement("Blueprint");
            vsManifestWriter.WriteElementString("Name", blueprintManifest.DisplayName);
            vsManifestWriter.WriteElementString("Description", blueprintManifest.Description);
            vsManifestWriter.WriteElementString("SortOrder", blueprintManifest.SortOrder.ToString());
            vsManifestWriter.WriteElementString("File", new FileInfo(blueprintZip).Name);

            vsManifestWriter.WriteStartElement("Tags");
            foreach (var tag in blueprintManifest.Tags)
            {
                vsManifestWriter.WriteElementString("Tag", tag);
            }
            vsManifestWriter.WriteEndElement();

            vsManifestWriter.WriteStartElement("HiddenTags");
            foreach (var tag in blueprintManifest.HiddenTags)
            {
                vsManifestWriter.WriteElementString("HiddenTag", tag);
            }
            vsManifestWriter.WriteEndElement();

            vsManifestWriter.WriteEndElement();
        }
    }
}