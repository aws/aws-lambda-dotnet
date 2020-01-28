using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

using Newtonsoft.Json;

namespace Packager
{
    public static class Utilities
    {
        public static void ZipCode(string sourceDirectory, string zipArchivePath)
        {
            sourceDirectory = sourceDirectory.Replace("\\", "/");

            using (var stream = File.Open(zipArchivePath, FileMode.Create, FileAccess.Write))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                foreach (var file in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
                {
                    var normalized = file.Replace("\\", "/");
                    var relativePath = normalized.Substring(sourceDirectory.Length);
                    if (relativePath.StartsWith("/"))
                    {
                        relativePath = relativePath.Substring(1);
                    }

                    if (relativePath.StartsWith("bin/") ||
                        relativePath.Contains("/bin/") ||
                        relativePath.StartsWith("obj/") ||
                        relativePath.Contains("/obj/"))
                        continue;

                    var entry = archive.CreateEntry(relativePath);
                    using (var fileStream = File.OpenRead(file))
                    using (var entryStream = entry.Open())
                    {
                        fileStream.CopyTo(entryStream);
                    }
                }
            }
        }

        public static void FormatJsonFile(string file)
        {
            var rootObj = JsonConvert.DeserializeObject(File.ReadAllText(file));
            var formattedJson = JsonConvert.SerializeObject(rootObj, Formatting.Indented);
            File.WriteAllText(file, formattedJson);
        }
    }
}
