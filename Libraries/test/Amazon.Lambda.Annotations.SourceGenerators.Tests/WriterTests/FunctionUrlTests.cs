// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using Amazon.Lambda.Annotations.APIGateway;
using Amazon.Lambda.Annotations.SourceGenerator;
using Amazon.Lambda.Annotations.SourceGenerator.Models;
using Amazon.Lambda.Annotations.SourceGenerator.Models.Attributes;
using Amazon.Lambda.Annotations.SourceGenerator.Writers;
using Xunit;

namespace Amazon.Lambda.Annotations.SourceGenerators.Tests.WriterTests
{
    public partial class CloudFormationWriterTests
    {
        [Theory]
        [InlineData(CloudFormationTemplateFormat.Json)]
        [InlineData(CloudFormationTemplateFormat.Yaml)]
        public void FunctionUrlWithDefaultAuthType(CloudFormationTemplateFormat templateFormat)
        {
            var mockFileManager = GetMockFileManager(string.Empty);
            var lambdaFunctionModel = GetLambdaFunctionModel("MyAssembly::MyNamespace.MyType::Handler",
                "TestMethod", 30, 512, null, null);
            lambdaFunctionModel.Attributes = new List<AttributeModel>
            {
                new AttributeModel<FunctionUrlAttribute> { Data = new FunctionUrlAttribute() }
            };
            var cloudFormationWriter = GetCloudFormationWriter(mockFileManager, _directoryManager, templateFormat, _diagnosticReporter);
            var report = GetAnnotationReport(new List<ILambdaFunctionSerializable> { lambdaFunctionModel });
            ITemplateWriter templateWriter = templateFormat == CloudFormationTemplateFormat.Json ? new JsonWriter() : new YamlWriter();

            cloudFormationWriter.ApplyReport(report);

            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));
            Assert.Equal("NONE", templateWriter.GetToken<string>("Resources.TestMethod.Properties.FunctionUrlConfig.AuthType"));
        }

        [Theory]
        [InlineData(CloudFormationTemplateFormat.Json)]
        [InlineData(CloudFormationTemplateFormat.Yaml)]
        public void FunctionUrlWithIamAuth(CloudFormationTemplateFormat templateFormat)
        {
            var mockFileManager = GetMockFileManager(string.Empty);
            var lambdaFunctionModel = GetLambdaFunctionModel("MyAssembly::MyNamespace.MyType::Handler",
                "TestMethod", 30, 512, null, null);
            lambdaFunctionModel.Attributes = new List<AttributeModel>
            {
                new AttributeModel<FunctionUrlAttribute>
                {
                    Data = new FunctionUrlAttribute { AuthType = FunctionUrlAuthType.AWS_IAM }
                }
            };
            var cloudFormationWriter = GetCloudFormationWriter(mockFileManager, _directoryManager, templateFormat, _diagnosticReporter);
            var report = GetAnnotationReport(new List<ILambdaFunctionSerializable> { lambdaFunctionModel });
            ITemplateWriter templateWriter = templateFormat == CloudFormationTemplateFormat.Json ? new JsonWriter() : new YamlWriter();

            cloudFormationWriter.ApplyReport(report);

            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));
            Assert.Equal("AWS_IAM", templateWriter.GetToken<string>("Resources.TestMethod.Properties.FunctionUrlConfig.AuthType"));
        }

        [Theory]
        [InlineData(CloudFormationTemplateFormat.Json)]
        [InlineData(CloudFormationTemplateFormat.Yaml)]
        public void FunctionUrlDoesNotCreateEventEntry(CloudFormationTemplateFormat templateFormat)
        {
            var mockFileManager = GetMockFileManager(string.Empty);
            var lambdaFunctionModel = GetLambdaFunctionModel("MyAssembly::MyNamespace.MyType::Handler",
                "TestMethod", 30, 512, null, null);
            lambdaFunctionModel.Attributes = new List<AttributeModel>
            {
                new AttributeModel<FunctionUrlAttribute> { Data = new FunctionUrlAttribute() }
            };
            var cloudFormationWriter = GetCloudFormationWriter(mockFileManager, _directoryManager, templateFormat, _diagnosticReporter);
            var report = GetAnnotationReport(new List<ILambdaFunctionSerializable> { lambdaFunctionModel });
            ITemplateWriter templateWriter = templateFormat == CloudFormationTemplateFormat.Json ? new JsonWriter() : new YamlWriter();

            cloudFormationWriter.ApplyReport(report);

            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));
            Assert.True(templateWriter.Exists("Resources.TestMethod.Properties.FunctionUrlConfig"));
            Assert.False(templateWriter.Exists("Resources.TestMethod.Properties.Events"));
        }

        [Theory]
        [InlineData(CloudFormationTemplateFormat.Json)]
        [InlineData(CloudFormationTemplateFormat.Yaml)]
        public void FunctionUrlWithCors(CloudFormationTemplateFormat templateFormat)
        {
            var mockFileManager = GetMockFileManager(string.Empty);
            var lambdaFunctionModel = GetLambdaFunctionModel("MyAssembly::MyNamespace.MyType::Handler",
                "TestMethod", 30, 512, null, null);
            lambdaFunctionModel.Attributes = new List<AttributeModel>
            {
                new AttributeModel<FunctionUrlAttribute>
                {
                    Data = new FunctionUrlAttribute
                    {
                        AuthType = FunctionUrlAuthType.NONE,
                        AllowOrigins = new[] { "https://example.com" },
                        AllowMethods = new[] { LambdaHttpMethod.Get, LambdaHttpMethod.Post },
                        AllowHeaders = new[] { "Content-Type", "Authorization" },
                        AllowCredentials = true,
                        MaxAge = 3600
                    }
                }
            };
            var cloudFormationWriter = GetCloudFormationWriter(mockFileManager, _directoryManager, templateFormat, _diagnosticReporter);
            var report = GetAnnotationReport(new List<ILambdaFunctionSerializable> { lambdaFunctionModel });
            ITemplateWriter templateWriter = templateFormat == CloudFormationTemplateFormat.Json ? new JsonWriter() : new YamlWriter();

            cloudFormationWriter.ApplyReport(report);

            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));
            var corsPath = "Resources.TestMethod.Properties.FunctionUrlConfig.Cors";
            Assert.Equal(new List<string> { "https://example.com" }, templateWriter.GetToken<List<string>>($"{corsPath}.AllowOrigins"));
            Assert.Equal(new List<string> { "GET", "POST" }, templateWriter.GetToken<List<string>>($"{corsPath}.AllowMethods"));
            Assert.Equal(new List<string> { "Content-Type", "Authorization" }, templateWriter.GetToken<List<string>>($"{corsPath}.AllowHeaders"));
            Assert.True(templateWriter.GetToken<bool>($"{corsPath}.AllowCredentials"));
            Assert.Equal(3600, templateWriter.GetToken<int>($"{corsPath}.MaxAge"));
        }

        [Theory]
        [InlineData(CloudFormationTemplateFormat.Json)]
        [InlineData(CloudFormationTemplateFormat.Yaml)]
        public void FunctionUrlWithoutCorsDoesNotEmitCorsBlock(CloudFormationTemplateFormat templateFormat)
        {
            var mockFileManager = GetMockFileManager(string.Empty);
            var lambdaFunctionModel = GetLambdaFunctionModel("MyAssembly::MyNamespace.MyType::Handler",
                "TestMethod", 30, 512, null, null);
            lambdaFunctionModel.Attributes = new List<AttributeModel>
            {
                new AttributeModel<FunctionUrlAttribute>
                {
                    Data = new FunctionUrlAttribute { AuthType = FunctionUrlAuthType.AWS_IAM }
                }
            };
            var cloudFormationWriter = GetCloudFormationWriter(mockFileManager, _directoryManager, templateFormat, _diagnosticReporter);
            var report = GetAnnotationReport(new List<ILambdaFunctionSerializable> { lambdaFunctionModel });
            ITemplateWriter templateWriter = templateFormat == CloudFormationTemplateFormat.Json ? new JsonWriter() : new YamlWriter();

            cloudFormationWriter.ApplyReport(report);

            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));
            Assert.True(templateWriter.Exists("Resources.TestMethod.Properties.FunctionUrlConfig.AuthType"));
            Assert.False(templateWriter.Exists("Resources.TestMethod.Properties.FunctionUrlConfig.Cors"));
        }

        [Theory]
        [InlineData(CloudFormationTemplateFormat.Json)]
        [InlineData(CloudFormationTemplateFormat.Yaml)]
        public void FunctionUrlCorsRemovedWhenCorsCleared(CloudFormationTemplateFormat templateFormat)
        {
            // First pass: emit full CORS config
            var mockFileManager = GetMockFileManager(string.Empty);
            var lambdaFunctionModel = GetLambdaFunctionModel("MyAssembly::MyNamespace.MyType::Handler",
                "TestMethod", 30, 512, null, null);
            lambdaFunctionModel.Attributes = new List<AttributeModel>
            {
                new AttributeModel<FunctionUrlAttribute>
                {
                    Data = new FunctionUrlAttribute
                    {
                        AuthType = FunctionUrlAuthType.NONE,
                        AllowOrigins = new[] { "https://example.com" },
                        AllowMethods = new[] { LambdaHttpMethod.Get, LambdaHttpMethod.Post },
                        AllowHeaders = new[] { "Content-Type" },
                        AllowCredentials = true,
                        MaxAge = 3600
                    }
                }
            };
            var cloudFormationWriter = GetCloudFormationWriter(mockFileManager, _directoryManager, templateFormat, _diagnosticReporter);
            var report = GetAnnotationReport(new List<ILambdaFunctionSerializable> { lambdaFunctionModel });
            ITemplateWriter templateWriter = templateFormat == CloudFormationTemplateFormat.Json ? new JsonWriter() : new YamlWriter();

            cloudFormationWriter.ApplyReport(report);
            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));
            var corsPath = "Resources.TestMethod.Properties.FunctionUrlConfig.Cors";
            Assert.True(templateWriter.Exists(corsPath));
            Assert.Equal(new List<string> { "https://example.com" }, templateWriter.GetToken<List<string>>($"{corsPath}.AllowOrigins"));
            Assert.True(templateWriter.GetToken<bool>($"{corsPath}.AllowCredentials"));
            Assert.Equal(3600, templateWriter.GetToken<int>($"{corsPath}.MaxAge"));

            // Second pass: clear all CORS properties (AllowOrigins=null, AllowCredentials=false, MaxAge=0)
            lambdaFunctionModel.Attributes = new List<AttributeModel>
            {
                new AttributeModel<FunctionUrlAttribute>
                {
                    Data = new FunctionUrlAttribute { AuthType = FunctionUrlAuthType.NONE }
                }
            };
            cloudFormationWriter.ApplyReport(GetAnnotationReport(new List<ILambdaFunctionSerializable> { lambdaFunctionModel }));

            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));
            Assert.Equal("NONE", templateWriter.GetToken<string>("Resources.TestMethod.Properties.FunctionUrlConfig.AuthType"));
            Assert.False(templateWriter.Exists(corsPath));
        }

        [Theory]
        [InlineData(CloudFormationTemplateFormat.Json)]
        [InlineData(CloudFormationTemplateFormat.Yaml)]
        public void FunctionUrlCorsUpdatedBetweenPasses(CloudFormationTemplateFormat templateFormat)
        {
            // First pass: emit CORS with AllowOrigins and AllowMethods
            var mockFileManager = GetMockFileManager(string.Empty);
            var lambdaFunctionModel = GetLambdaFunctionModel("MyAssembly::MyNamespace.MyType::Handler",
                "TestMethod", 30, 512, null, null);
            lambdaFunctionModel.Attributes = new List<AttributeModel>
            {
                new AttributeModel<FunctionUrlAttribute>
                {
                    Data = new FunctionUrlAttribute
                    {
                        AuthType = FunctionUrlAuthType.NONE,
                        AllowOrigins = new[] { "https://example.com" },
                        AllowMethods = new[] { LambdaHttpMethod.Get, LambdaHttpMethod.Post },
                        AllowCredentials = true,
                        MaxAge = 3600
                    }
                }
            };
            var cloudFormationWriter = GetCloudFormationWriter(mockFileManager, _directoryManager, templateFormat, _diagnosticReporter);
            ITemplateWriter templateWriter = templateFormat == CloudFormationTemplateFormat.Json ? new JsonWriter() : new YamlWriter();

            cloudFormationWriter.ApplyReport(GetAnnotationReport(new List<ILambdaFunctionSerializable> { lambdaFunctionModel }));
            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));
            var corsPath = "Resources.TestMethod.Properties.FunctionUrlConfig.Cors";
            Assert.True(templateWriter.Exists($"{corsPath}.AllowOrigins"));
            Assert.True(templateWriter.Exists($"{corsPath}.AllowMethods"));
            Assert.True(templateWriter.Exists($"{corsPath}.AllowCredentials"));
            Assert.True(templateWriter.Exists($"{corsPath}.MaxAge"));

            // Second pass: change to only AllowOrigins with a different value, remove everything else
            lambdaFunctionModel.Attributes = new List<AttributeModel>
            {
                new AttributeModel<FunctionUrlAttribute>
                {
                    Data = new FunctionUrlAttribute
                    {
                        AuthType = FunctionUrlAuthType.NONE,
                        AllowOrigins = new[] { "https://other.com" }
                    }
                }
            };
            cloudFormationWriter.ApplyReport(GetAnnotationReport(new List<ILambdaFunctionSerializable> { lambdaFunctionModel }));

            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));
            Assert.Equal(new List<string> { "https://other.com" }, templateWriter.GetToken<List<string>>($"{corsPath}.AllowOrigins"));
            Assert.False(templateWriter.Exists($"{corsPath}.AllowMethods"));
            Assert.False(templateWriter.Exists($"{corsPath}.AllowCredentials"));
            Assert.False(templateWriter.Exists($"{corsPath}.MaxAge"));
        }

        [Theory]
        [InlineData(CloudFormationTemplateFormat.Json)]
        [InlineData(CloudFormationTemplateFormat.Yaml)]
        public void FunctionUrlConfigRemovedWhenAttributeRemoved(CloudFormationTemplateFormat templateFormat)
        {
            // First pass: create FunctionUrlConfig
            var mockFileManager = GetMockFileManager(string.Empty);
            var lambdaFunctionModel = GetLambdaFunctionModel("MyAssembly::MyNamespace.MyType::Handler",
                "TestMethod", 30, 512, null, null);
            lambdaFunctionModel.Attributes = new List<AttributeModel>
            {
                new AttributeModel<FunctionUrlAttribute>
                {
                    Data = new FunctionUrlAttribute { AllowOrigins = new[] { "*" } }
                }
            };
            var cloudFormationWriter = GetCloudFormationWriter(mockFileManager, _directoryManager, templateFormat, _diagnosticReporter);
            var report = GetAnnotationReport(new List<ILambdaFunctionSerializable> { lambdaFunctionModel });
            ITemplateWriter templateWriter = templateFormat == CloudFormationTemplateFormat.Json ? new JsonWriter() : new YamlWriter();

            cloudFormationWriter.ApplyReport(report);
            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));
            Assert.True(templateWriter.Exists("Resources.TestMethod.Properties.FunctionUrlConfig"));

            // Second pass: remove the attribute, FunctionUrlConfig should be cleaned up
            lambdaFunctionModel.Attributes = new List<AttributeModel>();
            cloudFormationWriter.ApplyReport(GetAnnotationReport(new List<ILambdaFunctionSerializable> { lambdaFunctionModel }));

            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));
            Assert.False(templateWriter.Exists("Resources.TestMethod.Properties.FunctionUrlConfig"));
        }

        [Theory]
        [InlineData(CloudFormationTemplateFormat.Json)]
        [InlineData(CloudFormationTemplateFormat.Yaml)]
        public void ManualFunctionUrlConfigPreservedWhenNoAttribute(CloudFormationTemplateFormat templateFormat)
        {
            // Simulate a template where FunctionUrlConfig was manually added (no SyncedFunctionUrlConfig metadata)
            var content = templateFormat == CloudFormationTemplateFormat.Json
                ? @"{
                      'AWSTemplateFormatVersion': '2010-09-09',
                      'Transform': 'AWS::Serverless-2016-10-31',
                      'Resources': {
                        'TestMethod': {
                          'Type': 'AWS::Serverless::Function',
                          'Metadata': {
                            'Tool': 'Amazon.Lambda.Annotations'
                          },
                          'Properties': {
                            'Runtime': 'dotnet8',
                            'CodeUri': '',
                            'MemorySize': 512,
                            'Timeout': 30,
                            'Policies': ['AWSLambdaBasicExecutionRole'],
                            'PackageType': 'Image',
                            'ImageUri': '.',
                            'ImageConfig': { 'Command': ['MyAssembly::MyNamespace.MyType::Handler'] },
                            'FunctionUrlConfig': {
                              'AuthType': 'AWS_IAM'
                            }
                          }
                        }
                      }
                    }"
                : "AWSTemplateFormatVersion: '2010-09-09'\nTransform: AWS::Serverless-2016-10-31\nResources:\n  TestMethod:\n    Type: AWS::Serverless::Function\n    Metadata:\n      Tool: Amazon.Lambda.Annotations\n    Properties:\n      Runtime: dotnet8\n      CodeUri: ''\n      MemorySize: 512\n      Timeout: 30\n      Policies:\n        - AWSLambdaBasicExecutionRole\n      PackageType: Image\n      ImageUri: .\n      ImageConfig:\n        Command:\n          - 'MyAssembly::MyNamespace.MyType::Handler'\n      FunctionUrlConfig:\n        AuthType: AWS_IAM";

            var mockFileManager = GetMockFileManager(content);
            var lambdaFunctionModel = GetLambdaFunctionModel("MyAssembly::MyNamespace.MyType::Handler",
                "TestMethod", 30, 512, null, null);
            // No FunctionUrl attribute
            lambdaFunctionModel.Attributes = new List<AttributeModel>();
            var cloudFormationWriter = GetCloudFormationWriter(mockFileManager, _directoryManager, templateFormat, _diagnosticReporter);
            var report = GetAnnotationReport(new List<ILambdaFunctionSerializable> { lambdaFunctionModel });
            ITemplateWriter templateWriter = templateFormat == CloudFormationTemplateFormat.Json ? new JsonWriter() : new YamlWriter();

            cloudFormationWriter.ApplyReport(report);

            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));
            // The manually-added FunctionUrlConfig should be preserved
            Assert.True(templateWriter.Exists("Resources.TestMethod.Properties.FunctionUrlConfig"));
            Assert.Equal("AWS_IAM", templateWriter.GetToken<string>("Resources.TestMethod.Properties.FunctionUrlConfig.AuthType"));
        }

        [Theory]
        [InlineData(CloudFormationTemplateFormat.Json)]
        [InlineData(CloudFormationTemplateFormat.Yaml)]
        public void FunctionUrlMetadataTrackedAndCleanedUp(CloudFormationTemplateFormat templateFormat)
        {
            // First pass: create FunctionUrlConfig via attribute
            var mockFileManager = GetMockFileManager(string.Empty);
            var lambdaFunctionModel = GetLambdaFunctionModel("MyAssembly::MyNamespace.MyType::Handler",
                "TestMethod", 30, 512, null, null);
            lambdaFunctionModel.Attributes = new List<AttributeModel>
            {
                new AttributeModel<FunctionUrlAttribute> { Data = new FunctionUrlAttribute() }
            };
            var cloudFormationWriter = GetCloudFormationWriter(mockFileManager, _directoryManager, templateFormat, _diagnosticReporter);
            ITemplateWriter templateWriter = templateFormat == CloudFormationTemplateFormat.Json ? new JsonWriter() : new YamlWriter();

            cloudFormationWriter.ApplyReport(GetAnnotationReport(new List<ILambdaFunctionSerializable> { lambdaFunctionModel }));
            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));

            // Verify metadata is set
            Assert.True(templateWriter.Exists("Resources.TestMethod.Properties.FunctionUrlConfig"));
            Assert.True(templateWriter.GetToken<bool>("Resources.TestMethod.Metadata.SyncedFunctionUrlConfig"));

            // Second pass: remove the attribute
            lambdaFunctionModel.Attributes = new List<AttributeModel>();
            cloudFormationWriter.ApplyReport(GetAnnotationReport(new List<ILambdaFunctionSerializable> { lambdaFunctionModel }));
            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));

            // Verify both FunctionUrlConfig and metadata are cleaned up
            Assert.False(templateWriter.Exists("Resources.TestMethod.Properties.FunctionUrlConfig"));
            Assert.False(templateWriter.Exists("Resources.TestMethod.Metadata.SyncedFunctionUrlConfig"));
        }

        [Theory]
        [InlineData(CloudFormationTemplateFormat.Json)]
        [InlineData(CloudFormationTemplateFormat.Yaml)]
        public void SwitchFromFunctionUrlToHttpApi(CloudFormationTemplateFormat templateFormat)
        {
            // First pass: FunctionUrl
            var mockFileManager = GetMockFileManager(string.Empty);
            var lambdaFunctionModel = GetLambdaFunctionModel("MyAssembly::MyNamespace.MyType::Handler",
                "TestMethod", 30, 512, null, null);
            lambdaFunctionModel.Attributes = new List<AttributeModel>
            {
                new AttributeModel<FunctionUrlAttribute> { Data = new FunctionUrlAttribute() }
            };
            var cloudFormationWriter = GetCloudFormationWriter(mockFileManager, _directoryManager, templateFormat, _diagnosticReporter);
            ITemplateWriter templateWriter = templateFormat == CloudFormationTemplateFormat.Json ? new JsonWriter() : new YamlWriter();

            cloudFormationWriter.ApplyReport(GetAnnotationReport(new List<ILambdaFunctionSerializable> { lambdaFunctionModel }));
            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));
            Assert.True(templateWriter.Exists("Resources.TestMethod.Properties.FunctionUrlConfig"));

            // Second pass: switch to HttpApi
            lambdaFunctionModel.Attributes = new List<AttributeModel>
            {
                new AttributeModel<HttpApiAttribute>
                {
                    Data = new HttpApiAttribute(LambdaHttpMethod.Get, "/items")
                }
            };
            cloudFormationWriter.ApplyReport(GetAnnotationReport(new List<ILambdaFunctionSerializable> { lambdaFunctionModel }));

            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));
            Assert.False(templateWriter.Exists("Resources.TestMethod.Properties.FunctionUrlConfig"));
            Assert.True(templateWriter.Exists("Resources.TestMethod.Properties.Events.RootGet"));
            Assert.Equal("HttpApi", templateWriter.GetToken<string>("Resources.TestMethod.Properties.Events.RootGet.Type"));
        }
    }
}
