using System;
using System.Collections.Generic;
using Amazon.Lambda.Annotations.SourceGenerator.FileIO;
using Amazon.Lambda.Annotations.SourceGenerator.Models;
using Amazon.Lambda.Annotations.SourceGenerator.Writers;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Amazon.Lambda.Annotations.SourceGenerators.Tests.WriterTests
{
    public class CloudFormationJsonWriterTests
    {
        private const string ServerlessTemplateFilePath = "path/to/serverless.template";

        [Fact]
        public void AddSingletonFunctionToEmptyTemplate()
        {
            // ARRANGE
            var mockFileManager = GetMockFileManager(string.Empty);
            var lambdaFunctionModel = GetLambdaFunctionModel("MyAssembly::MyNamespace.MyType::Handler",
                "TestMethod", 45, 512, null, null);
            var cloudFormationJsonWriter = new CloudFormationJsonWriter(mockFileManager, new JsonWriter());
            var report = GetAnnotationReport(new List<ILambdaFunctionSerializable>() {lambdaFunctionModel});

            // ACT
            cloudFormationJsonWriter.ApplyReport(report);

            // ASSERT
            var rootToken = JObject.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));
            var functionToken = rootToken["Resources"]["TestMethod"];
            var propertiesToken = functionToken["Properties"];

            Assert.Equal("2010-09-09", rootToken["AWSTemplateFormatVersion"]);
            Assert.Equal("AWS::Serverless-2016-10-31", rootToken["Transform"]);

            Assert.Equal("AWS::Serverless::Function", functionToken["Type"]);
            Assert.Equal("Amazon.Lambda.Annotations", functionToken["Metadata"]["Tool"]);

            Assert.Equal("MyAssembly::MyNamespace.MyType::Handler", propertiesToken["Handler"]);
            Assert.Equal(512, propertiesToken["MemorySize"]);
            Assert.Equal(45, propertiesToken["Timeout"]);
            Assert.Equal(new List<string> {"AWSLambdaBasicExecutionRole"}, propertiesToken["Policies"].ToObject<List<string>>());
        }

        [Fact]
        public void SwitchFromPolicyToRole()
        {
            // ARRANGE
            var mockFileManager = GetMockFileManager(string.Empty);
            var cloudFormationJsonWriter = new CloudFormationJsonWriter(mockFileManager, new JsonWriter());

            //ACT - USE POLICY
            var lambdaFunctionModel = GetLambdaFunctionModel("MyAssembly::MyNamespace.MyType::Handler",
                "TestMethod", 45, 512, null, "Policy1, Policy2, Policy3");
            var report = GetAnnotationReport(new List<ILambdaFunctionSerializable> {lambdaFunctionModel});
            cloudFormationJsonWriter.ApplyReport(report);

            // ASSERT
            var rootToken = JObject.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));
            var policyArray = rootToken["Resources"]["TestMethod"]["Properties"]["Policies"];
            Assert.Equal(new List<string> {"Policy1", "Policy2", "Policy3"}, policyArray.ToObject<List<string>>());
            Assert.Null(rootToken["Resources"]["TestMethod"]["Properties"]["Role"]);

            // ACT - SWITCH TO ROLE
            lambdaFunctionModel = GetLambdaFunctionModel("MyAssembly::MyNamespace.MyType::Handler",
                "TestMethod", 45, 512, "Basic", null);
            report = GetAnnotationReport(new List<ILambdaFunctionSerializable> {lambdaFunctionModel});
            cloudFormationJsonWriter.ApplyReport(report);

            // ASSERT
            rootToken = JObject.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));
            Assert.Equal("Basic", rootToken["Resources"]["TestMethod"]["Properties"]["Role"]);
            Assert.Null(rootToken["Resources"]["TestMethod"]["Properties"]["Policies"]);

            // ACT - SWITCH BACK TO POLICY
            lambdaFunctionModel = GetLambdaFunctionModel("MyAssembly::MyNamespace.MyType::Handler",
                "TestMethod", 45, 512, null, "Policy1, Policy2, Policy3");
            report = GetAnnotationReport(new List<ILambdaFunctionSerializable> {lambdaFunctionModel});
            cloudFormationJsonWriter.ApplyReport(report);

            // ASSERT
            rootToken = JObject.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));
            policyArray = rootToken["Resources"]["TestMethod"]["Properties"]["Policies"];
            Assert.Equal(new List<string> {"Policy1", "Policy2", "Policy3"}, policyArray.ToObject<List<string>>());
            Assert.Null(rootToken["Resources"]["TestMethod"]["Properties"]["Role"]);
        }

        [Fact]
        public void UseRefForPolicies()
        {
            // ARRANGE
            var mockFileManager = GetMockFileManager(string.Empty);
            var lambdaFunctionModel = GetLambdaFunctionModel("MyAssembly::MyNamespace.MyType::Handler",
                "TestMethod", 45, 512, null, "Policy1, @Policy2, Policy3");
            var cloudFormationJsonWriter = new CloudFormationJsonWriter(mockFileManager, new JsonWriter());
            var report = GetAnnotationReport(new List<ILambdaFunctionSerializable> {lambdaFunctionModel});

            // ACT
            cloudFormationJsonWriter.ApplyReport(report);

            // ASSERT
            var rootToken = JObject.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));
            var policies = rootToken["Resources"]["TestMethod"]["Properties"]["Policies"] as JArray;
            Assert.Equal(3, policies.Count);
            Assert.Equal("Policy1", policies[0]);
            Assert.Equal("Policy2", policies[1]["Ref"]);
            Assert.Equal("Policy3", policies[2]);
        }

        [Fact]
        public void UseRefForRole()
        {
            // ARRANGE
            var mockFileManager = GetMockFileManager(string.Empty);
            var lambdaFunctionModel = GetLambdaFunctionModel("MyAssembly::MyNamespace.MyType::Handler",
                "TestMethod", 45, 512, "@Basic", null);
            var cloudFormationJsonWriter = new CloudFormationJsonWriter(mockFileManager, new JsonWriter());
            var report = GetAnnotationReport(new List<ILambdaFunctionSerializable> {lambdaFunctionModel});

            // ACT
            cloudFormationJsonWriter.ApplyReport(report);

            // ASSERT
            var rootToken = JObject.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));
            Assert.Equal("Basic", rootToken["Resources"]["TestMethod"]["Properties"]["Role"]["Ref"]);
        }

        [Fact]
        public void RenameFunction()
        {
            // ARRANGE
            var mockFileManager = GetMockFileManager(string.Empty);
            var lambdaFunctionModel = GetLambdaFunctionModel("MyAssembly::MyNamespace.MyType::Handler",
                "OldName", 45, 512, "@Basic", null);
            var cloudFormationJsonWriter = new CloudFormationJsonWriter(mockFileManager, new JsonWriter());
            var report = GetAnnotationReport(new List<ILambdaFunctionSerializable> {lambdaFunctionModel});

            // ACT
            cloudFormationJsonWriter.ApplyReport(report);

            // ASSERT
            var rootToken = JObject.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));
            Assert.NotNull(rootToken["Resources"]["OldName"]);
            Assert.Equal("MyAssembly::MyNamespace.MyType::Handler",
                rootToken["Resources"]["OldName"]["Properties"]["Handler"]);

            // ACT - CHANGE NAME
            lambdaFunctionModel.Name = "NewName";
            cloudFormationJsonWriter.ApplyReport(report);

            // ASSERT
            rootToken = JObject.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));
            Assert.Null(rootToken["Resources"]["OldName"]);
            Assert.NotNull(rootToken["Resources"]["NewName"]);
            Assert.Equal("MyAssembly::MyNamespace.MyType::Handler",
                rootToken["Resources"]["NewName"]["Properties"]["Handler"]);
        }

        [Fact]
        public void RemoveOrphanedLambdaFunctions()
        {
            // ARRANGE
            var originalContent = @"{
                              'AWSTemplateFormatVersion': '2010-09-09',
                              'Transform': 'AWS::Serverless-2016-10-31',
                              'Resources': {
                                'ObsoleteMethod': {
                                  'Type': 'AWS::Serverless::Function',
                                  'Metadata': {
                                    'Tool': 'Amazon.Lambda.Annotations'
                                  },
                                  'Properties': {
                                    'Runtime': 'dotnetcore3.1',
                                    'CodeUri': '',
                                    'MemorySize': 128,
                                    'Timeout': 100,
                                    'Policies': [
                                      'AWSLambdaBasicExecutionRole'
                                    ],
                                    'Handler': 'MyAssembly::MyNamespace.MyType::Handler'
                                  }
                                },
                                'MethodNotCreatedFromAnnotationsPackage': {
                                  'Type': 'AWS::Serverless::Function',
                                  'Properties': {
                                    'Runtime': 'dotnetcore3.1',
                                    'CodeUri': '',
                                    'MemorySize': 128,
                                    'Timeout': 100,
                                    'Policies': [
                                      'AWSLambdaBasicExecutionRole'
                                    ],
                                    'Handler': 'MyAssembly::MyNamespace.MyType::Handler'
                                  }
                                }
                              }
                            }";
            var mockFileManager = GetMockFileManager(originalContent);
            var lambdaFunctionModel = GetLambdaFunctionModel("MyAssembly::MyNamespace.MyType::Handler",
                "NewMethod", 45, 512, null, null);
            var cloudFormationJsonWriter = new CloudFormationJsonWriter(mockFileManager, new JsonWriter());
            var report = GetAnnotationReport(new List<ILambdaFunctionSerializable> {lambdaFunctionModel});

            // ACT
            cloudFormationJsonWriter.ApplyReport(report);

            // ASSERT
            var rootToken = JObject.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));
            Assert.Null(rootToken["Resources"]["ObsoleteMethod"]);
            Assert.NotNull(rootToken["Resources"]["NewMethod"]);
            Assert.NotNull(rootToken["Resources"]["MethodNotCreatedFromAnnotationsPackage"]);
        }

        [Fact]
        public void DoNotModifyFunctionWithoutRequiredMetadata()
        {
            // ARRANGE
            var originalContent = @"{
                              'AWSTemplateFormatVersion': '2010-09-09',
                              'Transform': 'AWS::Serverless-2016-10-31',
                              'Resources': {
                                'MethodNotCreatedFromAnnotationsPackage': {
                                  'Type': 'AWS::Serverless::Function',
                                  'Properties': {
                                    'Runtime': 'dotnetcore3.1',
                                    'CodeUri': '',
                                    'MemorySize': 128,
                                    'Timeout': 100,
                                    'Policies': [
                                      'AWSLambdaBasicExecutionRole'
                                    ],
                                    'Handler': 'MyAssembly::MyNamespace.MyType::Handler'
                                  }
                                }
                              }
                            }";

            var mockFileManager = GetMockFileManager(originalContent);
            var lambdaFunctionModel = GetLambdaFunctionModel("MyAssembly::MyNamespace.MyType::Handler",
                "MethodNotCreatedFromAnnotationsPackage", 45, 512, null, "Policy1, Policy2, Policy3");
            var cloudFormationJsonWriter = new CloudFormationJsonWriter(mockFileManager, new JsonWriter());
            var report = GetAnnotationReport(new() {lambdaFunctionModel});

            // ACT
            cloudFormationJsonWriter.ApplyReport(report);

            // ASSERT
            var rootToken = JObject.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));

            Assert.NotNull(rootToken["Resources"]["MethodNotCreatedFromAnnotationsPackage"]);
            Assert.Equal(128, rootToken["Resources"]["MethodNotCreatedFromAnnotationsPackage"]["Properties"]["MemorySize"]); // unchanged
            Assert.Equal(100, rootToken["Resources"]["MethodNotCreatedFromAnnotationsPackage"]["Properties"]["Timeout"]); // unchanged

            var policies = rootToken["Resources"]["MethodNotCreatedFromAnnotationsPackage"]["Properties"]["Policies"] as JArray;
            Assert.Equal(1, policies.Count);
            Assert.Equal("AWSLambdaBasicExecutionRole", policies[0]); // unchanged
        }

        private IFileManager GetMockFileManager(string originalContent)
        {
            var mockFileManager = new InMemoryFileManager();
            mockFileManager.WriteAllText(ServerlessTemplateFilePath, originalContent);
            return mockFileManager;
        }
        private LambdaFunctionModelTest GetLambdaFunctionModel(string handler, string name, uint? timeout,
            uint? memorySize, string role, string policies)
        {
            return new LambdaFunctionModelTest
            {
                Handler = handler,
                Name = name,
                MemorySize = memorySize,
                Timeout = timeout,
                Policies = policies,
                Role = role
            };
        }

        private AnnotationReport GetAnnotationReport(List<ILambdaFunctionSerializable> lambdaFunctionModels)
        {
            var annotationReport = new AnnotationReport
            {
                CloudFormationTemplatePath = ServerlessTemplateFilePath
            };
            foreach (var model in lambdaFunctionModels)
            {
                annotationReport.LambdaFunctions.Add(model);
            }

            return annotationReport;
        }

        public class LambdaFunctionModelTest : ILambdaFunctionSerializable
        {
            public string Handler { get; set; }
            public string Name { get; set; }
            public uint? Timeout { get; set; }
            public uint? MemorySize { get; set; }
            public string Role { get; set; }
            public string Policies { get; set; }
            public IList<Attribute> Attributes { get; set; }
        }
    }
}