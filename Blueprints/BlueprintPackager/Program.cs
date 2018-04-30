using System;
using System.IO;

namespace Packager
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var outputDirectory = Path.GetFullPath(@"../../Deployment/Blueprints");
            var msbuildBased_2_0_Blueprints = Path.GetFullPath(@"../BlueprintDefinitions/Msbuild-NETCore_2_0");
            try
            {
                Init(outputDirectory);

                var vsMsbuildPackager_2_0 = new VSMsbuildBlueprintPackager(msbuildBased_2_0_Blueprints, Path.Combine(outputDirectory, "VisualStudioBlueprintsMsbuild_2_0"));
                vsMsbuildPackager_2_0.Execute();
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
