using System;
using System.IO;
using System.Linq;
using Amazon.Lambda.Annotations.SourceGenerator;
using Amazon.Lambda.Annotations.SourceGenerator.FileIO;
using Xunit;

namespace Amazon.Lambda.Annotations.SourceGenerators.Tests
{
    public class CloudFormationTemplateFinderTests
    {
        [Fact]
        public void DetermineProjectRootDirectoryTest()
        {
            // ARRANGE
            var tempDir = Path.Combine(Path.GetTempPath(), GetRandomDirectoryName());
            CreateCustomerApplication(tempDir);
            var templateFinder = new CloudFormationTemplateFinder(new FileManager(), new DirectoryManager());

            // ACT and ASSERT
            var expectedRoot = Path.Combine(tempDir, "Codebase", "Src", "MyServerlessApp");
            Assert.Equal(expectedRoot, templateFinder.DetermineProjectRootDirectory(Path.Combine(expectedRoot, "Models", "Cars.cs")));
            Assert.Equal(expectedRoot, templateFinder.DetermineProjectRootDirectory(Path.Combine(expectedRoot, "BusinessLogic", "Logic2.cs")));
            Assert.Equal(expectedRoot, templateFinder.DetermineProjectRootDirectory(Path.Combine(expectedRoot, "Program.cs")));
            Assert.Equal(expectedRoot, templateFinder.DetermineProjectRootDirectory(Path.Combine(expectedRoot, "MyServerlessApp.csproj")));
        }
        
        [Fact]
        public void FindTemplateWithoutDefaultConfigFile()
        {
            // ARRANGE
            var tempDir = Path.Combine(Path.GetTempPath(), GetRandomDirectoryName());
            CreateCustomerApplication(tempDir);
            var templateFinder = new CloudFormationTemplateFinder(new FileManager(), new DirectoryManager());
            var projectRootDirectory = Path.Combine(tempDir, "Codebase", "Src", "MyServerlessApp");

            // ACT
            var templatePath = templateFinder.FindCloudFormationTemplate(projectRootDirectory);

            // ASSERT
            var expectedPath = Path.Combine(projectRootDirectory, "serverless.template");
            Assert.Equal(expectedPath, templatePath);
            Assert.True(File.Exists(templatePath));
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

            var tempDir = Path.Combine(Path.GetTempPath(), GetRandomDirectoryName());
            CreateCustomerApplication(tempDir);
            var templateFinder = new CloudFormationTemplateFinder(new FileManager(), new DirectoryManager());
            var projectRootDirectory = Path.Combine(tempDir, "Codebase", "Src", "MyServerlessApp");
            CreateFile(Path.Combine(projectRootDirectory, "Configurations", "aws-lambda-tools-defaults.json"));
            File.WriteAllText(Path.Combine(projectRootDirectory, "Configurations", "aws-lambda-tools-defaults.json"), content);

            // ACT
            var templatePath = templateFinder.FindCloudFormationTemplate(projectRootDirectory);

            // ASSERT
            var expectedPath = Path.Combine(projectRootDirectory, "Configurations", "serverless.template");
            Assert.Equal(expectedPath, templatePath);
            Assert.True(File.Exists(templatePath));
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
                's3-prefix': 'AWSServerless1/'
            }";

            var tempDir = Path.Combine(Path.GetTempPath(), GetRandomDirectoryName());
            CreateCustomerApplication(tempDir);
            var templateFinder = new CloudFormationTemplateFinder(new FileManager(), new DirectoryManager());
            var projectRootDirectory = Path.Combine(tempDir, "Codebase", "Src", "MyServerlessApp");
            CreateFile(Path.Combine(projectRootDirectory, "Configurations", "aws-lambda-tools-defaults.json"));
            File.WriteAllText(Path.Combine(projectRootDirectory, "Configurations", "aws-lambda-tools-defaults.json"), content);

            // ACT
            var templatePath = templateFinder.FindCloudFormationTemplate(projectRootDirectory);

            // ASSERT
            var expectedPath = Path.Combine(projectRootDirectory, "serverless.template");
            Assert.Equal(expectedPath, templatePath);
            Assert.True(File.Exists(templatePath));
        }
        
        private void CreateCustomerApplication(string tempDir)
        {
            var projectRoot = Path.Combine(tempDir, "Codebase", "Src", "MyServerlessApp");
            CreateFile(Path.Combine(projectRoot, "MyServerlessApp.csproj"));
            CreateFile(Path.Combine(projectRoot, "Models", "Cars.cs"));
            CreateFile(Path.Combine(projectRoot, "Models", "Bus.cs"));
            CreateFile(Path.Combine(projectRoot, "BusinessLogic", "Logic1.cs"));
            CreateFile(Path.Combine(projectRoot, "BusinessLogic", "Logic2.cs"));
            CreateFile(Path.Combine(projectRoot, "Program.cs"));
        }

        private void CreateFile(string filePath)
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            File.Create(filePath).Close();
        }

        private string GetRandomDirectoryName()
        {
            var guid = Guid.NewGuid().ToString();
            return guid.Split('-').FirstOrDefault();
        }
    }
}