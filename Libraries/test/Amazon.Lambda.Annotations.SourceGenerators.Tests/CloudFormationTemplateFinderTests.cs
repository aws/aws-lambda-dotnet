using System.IO;
using System.IO.Abstractions.TestingHelpers;
using Amazon.Lambda.Annotations.SourceGenerator;
using Xunit;

namespace Amazon.Lambda.Annotations.SourceGenerators.Tests
{
    public class CloudFormationTemplateFinderTests
    {
        [Fact]
        public void DetermineProjectRootDirectoryTest()
        {
            // ARRANGE
            var fileSystem = GetMockFileSystem();
            var templateFinder = new CloudFormationTemplateFinder(fileSystem);
            
            // ACT and ASSERT
            var expectedPath = Path.Combine("C:", "codebase", "src", "MyServerlessApp");
            Assert.Equal(expectedPath, templateFinder.DetermineProjectRootDirectory("C:/codebase/src/MyServerlessApp/Models/Cars.cs"));
            Assert.Equal(expectedPath, templateFinder.DetermineProjectRootDirectory("C:/codebase/src/MyServerlessApp/BusinessLogic/Logic2.cs"));
            Assert.Equal(expectedPath, templateFinder.DetermineProjectRootDirectory("C:/codebase/src/MyServerlessApp/Program.cs"));
            Assert.Equal(expectedPath, templateFinder.DetermineProjectRootDirectory("C:/codebase/src/MyServerlessApp/MyServerlessApp.csproj"));
        }

        [Fact]
        public void FindTemplateWithoutDefaultConfigFile()
        {
            // ARRANGE
            var fileSystem = GetMockFileSystem();
            var templateFinder = new CloudFormationTemplateFinder(fileSystem);
            var projectRootDirectory = Path.Combine("C:", "codebase", "src", "MyServerlessApp");
            
            // ACT
            var templatePath = templateFinder.FindCloudFormationTemplate(projectRootDirectory);
            
            // ASSERT
            var expectedPath = Path.Combine(projectRootDirectory, "serverless.template");
            Assert.Equal(expectedPath, templatePath);
            Assert.True(fileSystem.FileExists(templatePath));
        }
        
        [Fact]
        public void FindTemplateFromDefaultConfigFile()
        {
            // ARRANGE
            var content = @"{
                'Information': [
                'This file provides default values for the deployment wizard inside Visual Studio and the AWS Lambda commands added to the .NET Core CLI.',
                'To learn more about the Lambda commands with the .NET Core CLI execute the following command at the command line in the project root directory.',
                'dotnet lambda help',
                'All the command line options for the Lambda command can be specified in this file.'
                ],
                'profile': 'default',
                'region': 'us-west-2',
                'configuration': 'Release',
                'framework': 'netcoreapp3.1',
                's3-prefix': 'AWSServerless1/',
                'template': 'serverless.template',
                'template-parameters': ''
            }";
           
            var fileSystem = GetMockFileSystem();
            fileSystem.AddFile("C:/codebase/src/MyServerlessApp/Configurations/aws-lambda-tools-defaults.json", content);
            var templateFinder = new CloudFormationTemplateFinder(fileSystem);
            var projectRootDirectory = Path.Combine("C:", "codebase", "src", "MyServerlessApp");
            
            // ACT
            var templatePath = templateFinder.FindCloudFormationTemplate(projectRootDirectory);
            
            // ASSERT
            var expectedPath = Path.Combine(projectRootDirectory, "Configurations", "serverless.template");
            Assert.Equal(expectedPath, templatePath);
            Assert.True(fileSystem.FileExists(templatePath));
        }

        [Fact]
        public void FindTemplatePathWhenDefaultConfigFileDoesNotHaveTemplateProperty()
        {
            // ARRANGE
            var content = @"{
                'Information': [
                'This file provides default values for the deployment wizard inside Visual Studio and the AWS Lambda commands added to the .NET Core CLI.',
                'To learn more about the Lambda commands with the .NET Core CLI execute the following command at the command line in the project root directory.',
                'dotnet lambda help',
                'All the command line options for the Lambda command can be specified in this file.'
                ],
                'profile': 'default',
                'region': 'us-west-2',
                'configuration': 'Release',
                'framework': 'netcoreapp3.1',
                's3-prefix': 'AWSServerless1/',
            }";
           
            var fileSystem = GetMockFileSystem();
            fileSystem.AddFile("C:/codebase/src/MyServerlessApp/Configurations/aws-lambda-tools-defaults.json", content);
            var templateFinder = new CloudFormationTemplateFinder(fileSystem);
            var projectRootDirectory = Path.Combine("C:", "codebase", "src", "MyServerlessApp");
            
            // ACT
            var templatePath = templateFinder.FindCloudFormationTemplate(projectRootDirectory);
            
            // ASSERT
            var expectedPath = Path.Combine(projectRootDirectory, "serverless.template");
            Assert.Equal(expectedPath, templatePath);
            Assert.True(fileSystem.FileExists(templatePath));
        }

        private MockFileSystem GetMockFileSystem()
        {
            var fileSystem = new MockFileSystem();
            
            fileSystem.AddFile("C:/codebase/src/MyServerlessApp/MyServerlessApp.csproj", string.Empty);
            fileSystem.AddFile("C:/codebase/src/MyServerlessApp/Models/Cars.cs", string.Empty);
            fileSystem.AddFile("C:/codebase/src/MyServerlessApp/Models/Bus.cs", string.Empty);
            fileSystem.AddFile("C:/codebase/src/MyServerlessApp/BusinessLogic/Logic1.cs", string.Empty);
            fileSystem.AddFile("C:/codebase/src/MyServerlessApp/BusinessLogic/Logic2.cs", string.Empty);
            fileSystem.AddFile("C:/codebase/src/MyServerlessApp/Program.cs", string.Empty);

            return fileSystem;
        }
    }
}