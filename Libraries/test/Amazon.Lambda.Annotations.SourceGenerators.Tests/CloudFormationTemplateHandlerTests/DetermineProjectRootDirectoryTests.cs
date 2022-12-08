using System.IO;
using Amazon.Lambda.Annotations.SourceGenerator;
using Amazon.Lambda.Annotations.SourceGenerator.FileIO;
using Xunit;

namespace Amazon.Lambda.Annotations.SourceGenerators.Tests.CloudFormationTemplateHandlerTests
{
    public class DetermineProjectPathTests
    {
        /// <summary>
        /// Tests that that we can resolve the path of the .csproj from different source files in a Lambda project
        /// </summary>
        [Fact]
        public void DetermineProjectPath()
        {
            // ARRANGE
            var tempDir = Path.Combine(Path.GetTempPath(), Helpers.GetRandomDirectoryName());
            var projectRoot = Path.Combine(tempDir, "Codebase", "Src", "MyServerlessApp");
            Helpers.CreateCustomerApplication(projectRoot);
            var expectedProjectPath = Path.Combine(projectRoot, "MyServerlessApp.csproj");
            var templateHandler = new CloudFormationTemplateHandler(new FileManager(), new DirectoryManager());

            // ACT and ASSERT
            Assert.Equal(expectedProjectPath, templateHandler.DetermineProjectPath(Path.Combine(projectRoot, "Models", "Cars.cs")));
            Assert.Equal(expectedProjectPath, templateHandler.DetermineProjectPath(Path.Combine(projectRoot, "BusinessLogic", "Logic2.cs")));
            Assert.Equal(expectedProjectPath, templateHandler.DetermineProjectPath(Path.Combine(projectRoot, "Program.cs")));
            Assert.Equal(expectedProjectPath, templateHandler.DetermineProjectPath(Path.Combine(projectRoot, "MyServerlessApp.csproj")));
        }
    }
}