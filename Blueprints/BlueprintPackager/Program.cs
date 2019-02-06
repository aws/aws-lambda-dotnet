using System;
using System.IO;

namespace Packager
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var outputDirectory = GetFullPath(@"../../Deployment/Blueprints");
            var msbuildBased_2_1_Blueprints = GetFullPath(@"../BlueprintDefinitions/Msbuild-NETCore_2_1");
            try
            {
                Init(outputDirectory);

                var versionUpdater = new UpdatePackageReferenceVersions(msbuildBased_2_1_Blueprints);
                versionUpdater.Execute();

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
