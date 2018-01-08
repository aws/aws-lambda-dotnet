using System;
using System.IO;

namespace Packager
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var outputDirectory = Path.GetFullPath(@"../../Deployment/Blueprints");
            var msbuildBased_1_0_Blueprints = Path.GetFullPath(@"../BlueprintDefinitions/Msbuild-NETCore_1_0");
            var msbuildBased_2_0_Blueprints = Path.GetFullPath(@"../BlueprintDefinitions/Msbuild-NETCore_2_0");
            var projectJsonBasedBlueprints = Path.GetFullPath(@"../BlueprintDefinitions/ProjectJson");
            try
            {
                Init(outputDirectory);

                var vsMsbuildPackager_1_0 = new VSMsbuildBlueprintPackager(msbuildBased_1_0_Blueprints, Path.Combine(outputDirectory, "VisualStudioBlueprintsMsbuild_1_0"));
                vsMsbuildPackager_1_0.Execute();

                var vsMsbuildPackager_2_0 = new VSMsbuildBlueprintPackager(msbuildBased_2_0_Blueprints, Path.Combine(outputDirectory, "VisualStudioBlueprintsMsbuild_2_0"));
                vsMsbuildPackager_2_0.Execute();

                var vsProjectJsonPackager = new VSProjectJsonBlueprintPackager(projectJsonBasedBlueprints, outputDirectory);
                vsProjectJsonPackager.Execute();

                var yeomanPackager = new YeomanBlueprintPackager(projectJsonBasedBlueprints, outputDirectory);
                yeomanPackager.Execute();
            }
            catch(Exception e)
            {
                Console.WriteLine($"Unknown error processing blueprints: {e.Message}");
                Console.WriteLine(e.StackTrace);
                Environment.Exit(-1);
            }
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
