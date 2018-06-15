using System;
using System.IO;

namespace Packager
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var outputDirectory = Path.GetFullPath(@"../../Deployment/Blueprints");
            var msbuildBased_2_1_Blueprints = Path.GetFullPath(@"../BlueprintDefinitions/Msbuild-NETCore_2_1");
            try
            {
                Init(outputDirectory);

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
