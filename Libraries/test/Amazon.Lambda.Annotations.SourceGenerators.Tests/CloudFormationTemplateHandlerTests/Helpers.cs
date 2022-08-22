using System;
using System.IO;
using System.Linq;

namespace Amazon.Lambda.Annotations.SourceGenerators.Tests.CloudFormationTemplateHandlerTests
{
    public static class Helpers
    {
        public static void CreateCustomerApplication(string projectRoot)
        {
            CreateFile(Path.Combine(projectRoot, "MyServerlessApp.csproj"));
            CreateFile(Path.Combine(projectRoot, "Models", "Cars.cs"));
            CreateFile(Path.Combine(projectRoot, "Models", "Bus.cs"));
            CreateFile(Path.Combine(projectRoot, "BusinessLogic", "Logic1.cs"));
            CreateFile(Path.Combine(projectRoot, "BusinessLogic", "Logic2.cs"));
            CreateFile(Path.Combine(projectRoot, "Program.cs"));
        }

        public static void CreateFile(string filePath)
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            File.Create(filePath).Close();
        }

        public static string GetRandomDirectoryName()
        {
            var guid = Guid.NewGuid().ToString();
            return guid.Split('-').FirstOrDefault();
        }
    }
}