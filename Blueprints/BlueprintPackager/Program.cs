using System;
using System.IO;
using System.Linq;

namespace Packager
{
    public class Program
    {
        public static void Main(string[] args)
        {
            ProcessArgs(args, out var updateVersions);

            var outputDirectory = GetFullPath(@"../../Deployment/Blueprints");
            var blueprintPaths = new (string Source, string Output)[] { (Source: GetFullPath(@"../BlueprintDefinitions/vs2022"), Output: "vs2022"), (Source: GetFullPath(@"../BlueprintDefinitions/vs2017"), Output: "vs2017"), (Source: GetFullPath(@"../BlueprintDefinitions/vs2019"), Output: "vs2019") };
            try
            {
                Init(outputDirectory);

                foreach(var blueprintPath in blueprintPaths)
                {
                    if (updateVersions)
                    {
                        var versionUpdater = new UpdatePackageReferenceVersions(blueprintPath.Source);
                        versionUpdater.Execute();
                    }

                    foreach(var jsonFile in Directory.GetFiles(blueprintPath.Source, "*.*", SearchOption.AllDirectories).Where(x => string.Equals(Path.GetExtension(x), ".json") || string.Equals(Path.GetExtension(x), ".template")))
                    {
                        Utilities.FormatJsonFile(jsonFile);
                    }

                    var packager = new VSMsbuildBlueprintPackager(blueprintPath.Source, Path.Combine(outputDirectory, new DirectoryInfo(blueprintPath.Output).Name));
                    packager.Execute();
                }
            }
            catch(Exception e)
            {
                Console.WriteLine($"Unknown error processing blueprints: {e.Message}");
                Console.WriteLine(e.StackTrace);
                Environment.Exit(-1);
            }
        }

        private static void ProcessArgs(string[] args, out bool updateVersions)
        {
            updateVersions = false;
            if (args.Length == 1 && args[0] == "--updateVersions")
            {
                updateVersions = true;
            }
            else if (args.Length != 0)
            {
                Console.Error.WriteLine("usage: BlueprintPackager [--updateVersions]");
                Console.Error.WriteLine("--updateVersions Run job to automatically update nuget package versions for template projects.");
                Environment.Exit(-1);
            }
        }

        public static string GetFullPath(string relativePath)
        {
            if (Directory.GetCurrentDirectory().Contains("Debug") || Directory.GetCurrentDirectory().Contains("Release"))
                relativePath = Path.Combine("../../../", relativePath);

            return Path.GetFullPath(relativePath);
        }

        private static void Init(string outputDirectory)
        {
            var di = new DirectoryInfo(outputDirectory);
            if (di.Exists)
            {
                di.Delete(true);
            }

            di.Create();
        }
    }
}
