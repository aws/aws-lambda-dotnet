using System.IO;
using Amazon.Lambda.Annotations.SourceGenerator;
using Amazon.Lambda.Annotations.SourceGenerator.FileIO;
using Xunit;

namespace Amazon.Lambda.Annotations.SourceGenerators.Tests.CloudFormationTemplateHandlerTests
{
    public class DetermineTemplateFormatTests
    {
        [Fact]
        public void DetermineTemplateFormat_NoTemplate()
        {
            // ARRANGE
            var tempDir = Path.Combine(Path.GetTempPath(), Helpers.GetRandomDirectoryName());
            var templatePath = Path.Combine(tempDir, "serverless.template");

            // ACT
            var templateHandler = new CloudFormationTemplateHandler(new FileManager(), new DirectoryManager());
            var format = templateHandler.DetermineTemplateFormat(templatePath);

            // ASSERT
            Assert.Equal(CloudFormationTemplateFormat.Json, format);
        }

        [Fact]
        public void DetermineTemplateFormat_EmptyTemplate()
        {
            // ARRANGE
            var tempDir = Path.Combine(Path.GetTempPath(), Helpers.GetRandomDirectoryName());
            var templatePath = Path.Combine(tempDir, "serverless.template");
            Helpers.CreateFile(templatePath);

            // ACT
            var templateHandler = new CloudFormationTemplateHandler(new FileManager(), new DirectoryManager());
            var format = templateHandler.DetermineTemplateFormat(templatePath);

            // ASSERT
            Assert.Equal(CloudFormationTemplateFormat.Json, format);
        }

        [Fact]
        public void DetermineTemplateFormat_Json()
        {
            // ARRANGE
            var tempDir = Path.Combine(Path.GetTempPath(), Helpers.GetRandomDirectoryName());
            var templatePath = Path.Combine(tempDir, "serverless.template");
            Helpers.CreateFile(templatePath);
            const string content = @"{
               'AWSTemplateFormatVersion':'2010-09-09',
               'Transform':'AWS::Serverless-2016-10-31',
               'Description':'An AWS Serverless Application.',
               'Parameters':{
                  'ArchitectureTypeParameter':{
                     'Type':'String',
                     'Default':'x86_64',
                     'AllowedValues':[
                        'x86_64',
                        'arm64'
                     ]
                  }
               }
            }";
            File.WriteAllText(templatePath, content);

            // ACT
            var templateHandler = new CloudFormationTemplateHandler(new FileManager(), new DirectoryManager());
            var format = templateHandler.DetermineTemplateFormat(templatePath);

            // ASSERT
            Assert.Equal(CloudFormationTemplateFormat.Json, format);
        }

        [Fact]
        public void DetermineTemplateFormat_Yaml()
        {
            // ARRANGE
            var tempDir = Path.Combine(Path.GetTempPath(), Helpers.GetRandomDirectoryName());
            var templatePath = Path.Combine(tempDir, "serverless.template");
            Helpers.CreateFile(templatePath);
            const string content = @"AWSTemplateFormatVersion: '2010-09-09'
                            Transform: AWS::Serverless-2016-10-31
                            Description: An AWS Serverless Application.
                            Parameters:
                              ArchitectureTypeParameter:
                                Type: String
                                Default: x86_64
                                AllowedValues:
                                - x86_64
                                - arm64
                            ";
            File.WriteAllText(templatePath, content);

            // ACT
            var templateHandler = new CloudFormationTemplateHandler(new FileManager(), new DirectoryManager());
            var format = templateHandler.DetermineTemplateFormat(templatePath);

            // ASSERT
            Assert.Equal(CloudFormationTemplateFormat.Yaml, format);
        }
    }
}