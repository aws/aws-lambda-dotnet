using System.IO;
using Amazon.Lambda.Annotations.SourceGenerator;
using Amazon.Lambda.Annotations.SourceGenerator.FileIO;
using Xunit;

namespace Amazon.Lambda.Annotations.SourceGenerators.Tests.CloudFormationTemplateHandlerTests
{
    public class FindTemplateTests
    {
        [Fact]
        public void FindTemplate_WithoutDefaultConfigFile()
        {
            // ARRANGE
            var tempDir = Path.Combine(Path.GetTempPath(), Helpers.GetRandomDirectoryName());
            var projectRoot = Path.Combine(tempDir, "Codebase", "Src", "MyServerlessApp");
            Helpers.CreateCustomerApplication(projectRoot);
            var templateHandler = new CloudFormationTemplateHandler(new FileManager(), new DirectoryManager());

            // ACT
            var templatePath = templateHandler.FindTemplate(projectRoot);

            // ASSERT
            var expectedPath = Path.Combine(projectRoot, "serverless.template");
            Assert.Equal(expectedPath, templatePath);
            Assert.True(File.Exists(templatePath));
        }

        [Fact]
        public void FindTemplate_FromDefaultConfigFile()
        {
            // ARRANGE
            const string content = @"{
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

            var tempDir = Path.Combine(Path.GetTempPath(), Helpers.GetRandomDirectoryName());
            var projectRoot = Path.Combine(tempDir, "Codebase", "Src", "MyServerlessApp");
            Helpers.CreateCustomerApplication(projectRoot);
            var templateHandler = new CloudFormationTemplateHandler(new FileManager(), new DirectoryManager());
            Helpers.CreateFile(Path.Combine(projectRoot, "Configurations", "aws-lambda-tools-defaults.json"));
            File.WriteAllText(Path.Combine(projectRoot, "Configurations", "aws-lambda-tools-defaults.json"), content);

            // ACT
            var templatePath = templateHandler.FindTemplate(projectRoot);

            // ASSERT
            var expectedPath = Path.Combine(projectRoot, "Configurations", "serverless.template");
            Assert.Equal(expectedPath, templatePath);
            Assert.True(File.Exists(templatePath));
        }

        [Fact]
        public void FindTemplate_DefaultConfigFileDoesNotHaveTemplateProperty()
        {
            // ARRANGE
            const string content = @"{
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

            var tempDir = Path.Combine(Path.GetTempPath(), Helpers.GetRandomDirectoryName());
            var projectRoot = Path.Combine(tempDir, "Codebase", "Src", "MyServerlessApp");
            Helpers.CreateCustomerApplication(projectRoot);
            var templateHandler = new CloudFormationTemplateHandler(new FileManager(), new DirectoryManager());
            Helpers.CreateFile(Path.Combine(projectRoot, "Configurations", "aws-lambda-tools-defaults.json"));
            File.WriteAllText(Path.Combine(projectRoot, "Configurations", "aws-lambda-tools-defaults.json"), content);

            // ACT
            var templatePath = templateHandler.FindTemplate(projectRoot);

            // ASSERT
            var expectedPath = Path.Combine(projectRoot, "serverless.template");
            Assert.Equal(expectedPath, templatePath);
            Assert.True(File.Exists(templatePath));
        }

        [Fact]
        public void FindTemplate_DefaultConfigFile_Template_Is_AboveProjectRoot()
        {
            // ARRANGE
            const string content = @"{
                'profile': 'default',
                'region': 'us-west-2',
                'template': '../serverless.template',
            }";

            var tempDir = Path.Combine(Path.GetTempPath(), Helpers.GetRandomDirectoryName());
            var projectRoot = Path.Combine(tempDir, "Codebase", "Src", "MyServerlessApp");
            Helpers.CreateCustomerApplication(projectRoot);
            var templateHandler = new CloudFormationTemplateHandler(new FileManager(), new DirectoryManager());
            Helpers.CreateFile(Path.Combine(projectRoot, "aws-lambda-tools-defaults.json"));
            File.WriteAllText(Path.Combine(projectRoot, "aws-lambda-tools-defaults.json"), content);

            // ACT
            var templatePath = templateHandler.FindTemplate(projectRoot);

            // ASSERT
            var expectedPath = Path.Combine(projectRoot, "..", "serverless.template");
            Assert.Equal(expectedPath, templatePath);
            Assert.True(File.Exists(templatePath));
        }
    }
}