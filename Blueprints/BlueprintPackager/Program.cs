using System;
using System.IO;

namespace Packager
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var outputDirectory = Path.GetFullPath(@"../../Deployment/Blueprints");
            var blueprintRoot = Path.GetFullPath(@"../BlueprintDefinitions");
            try
            {
                Init(outputDirectory);

                var vsPackager = new VSBlueprintPackager(blueprintRoot, outputDirectory);
                vsPackager.Execute();

                var yeomanPackager = new YeomanBlueprintPackager(blueprintRoot, outputDirectory);
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
