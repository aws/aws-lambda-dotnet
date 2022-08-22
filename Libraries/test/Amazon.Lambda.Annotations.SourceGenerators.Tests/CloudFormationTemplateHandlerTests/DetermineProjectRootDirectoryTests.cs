using System.IO;
using Amazon.Lambda.Annotations.SourceGenerator;
using Amazon.Lambda.Annotations.SourceGenerator.FileIO;
using Xunit;

namespace Amazon.Lambda.Annotations.SourceGenerators.Tests.CloudFormationTemplateHandlerTests
{
    public class DetermineProjectRootDirectoryTests
    {
        [Fact]
        public void DetermineProjectRootDirectory()
        {
            // ARRANGE
            var tempDir = Path.Combine(Path.GetTempPath(), Helpers.GetRandomDirectoryName());
            var projectRoot = Path.Combine(tempDir, "Codebase", "Src", "MyServerlessApp");
            Helpers.CreateCustomerApplication(projectRoot);
            var templateHandler = new CloudFormationTemplateHandler(new FileManager(), new DirectoryManager());

            // ACT and ASSERT
            Assert.Equal(projectRoot, templateHandler.DetermineProjectRootDirectory(Path.Combine(projectRoot, "Models", "Cars.cs")));
            Assert.Equal(projectRoot, templateHandler.DetermineProjectRootDirectory(Path.Combine(projectRoot, "BusinessLogic", "Logic2.cs")));
            Assert.Equal(projectRoot, templateHandler.DetermineProjectRootDirectory(Path.Combine(projectRoot, "Program.cs")));
            Assert.Equal(projectRoot, templateHandler.DetermineProjectRootDirectory(Path.Combine(projectRoot, "MyServerlessApp.csproj")));
        }
    }
}