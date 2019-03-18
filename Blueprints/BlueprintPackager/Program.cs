using System;
using System.IO;

namespace Packager
{
    public class Program
    {
        public static void Main(string[] args)
        {
            ProcessArgs(args, out var updateVersions);

            var outputDirectory = GetFullPath(@"../../Deployment/Blueprints");
            var msbuildBased_2_1_Blueprints = GetFullPath(@"../BlueprintDefinitions/Msbuild-NETCore_2_1");
            try
            {
                Init(outputDirectory);

                if (updateVersions)
                {
                    var versionUpdater = new UpdatePackageReferenceVersions(msbuildBased_2_1_Blueprints);
                    versionUpdater.Execute();
                }

                var vsMsbuildPackager_2_1 = new VSMsbuildBlueprintPackager(msbuildBased_2_1_Blueprints, Path.Combine(outputDirectory, "VisualStudioBlueprintsMsbuild_2_1"));
                vsMsbuildPackager_2_1.Execute();
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
