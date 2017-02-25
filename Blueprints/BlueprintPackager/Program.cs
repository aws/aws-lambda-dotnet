using System;
using System.IO;

namespace Packager
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var outputDirectory = Path.GetFullPath(@"../../Deployment/Blueprints");
            var msbuildBasedBlueprints = Path.GetFullPath(@"../BlueprintDefinitions/Msbuild");
            var projectJsonBasedBlueprints = Path.GetFullPath(@"../BlueprintDefinitions/ProjectJson");
            try
            {
                Init(outputDirectory);

                var vsMsbuildPackager = new VSMsbuildBlueprintPackager(msbuildBasedBlueprints, outputDirectory);
                vsMsbuildPackager.Execute();

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
