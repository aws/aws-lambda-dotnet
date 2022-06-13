using System.Collections.Generic;
using Amazon.Lambda.Annotations.SourceGenerator.Diagnostics;
using Amazon.Lambda.Annotations.SourceGenerator.FileIO;
using Amazon.Lambda.Annotations.SourceGenerator.Models;
using Amazon.Lambda.Annotations.SourceGenerator.Models.Attributes;
using Amazon.Lambda.Annotations.SourceGenerator.Writers;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;
using JsonWriter = Amazon.Lambda.Annotations.SourceGenerator.Writers.JsonWriter;

namespace Amazon.Lambda.Annotations.SourceGenerators.Tests.WriterTests
{
    public class CloudFormationJsonWriterTests
    {
        private readonly IDirectoryManager _mockDirectoryManager = new InMemoryDirectoryManager();
        private readonly IJsonWriter _jsonWriter = new JsonWriter();
        private readonly IDiagnosticReporter _diagnosticReporter = new Mock<IDiagnosticReporter>().Object;
        private const string ProjectRootDirectory = "C:/CodeBase/MyProject";
        private const string ServerlessTemplateFilePath = "C:/CodeBase/MyProject/serverless.template";

        [Fact]
        public void AddSingletonFunctionToEmptyTemplate()
        {
            // ARRANGE
            var mockFileManager = GetMockFileManager(string.Empty);
            var lambdaFunctionModel = GetLambdaFunctionModel("MyAssembly::MyNamespace.MyType::Handler",
                "TestMethod", 45, 512, null, null);
            var cloudFormationJsonWriter = new CloudFormationJsonWriter(mockFileManager, _mockDirectoryManager, _jsonWriter, _diagnosticReporter);
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
            Assert.Equal("Zip", propertiesToken["PackageType"]);
            Assert.Equal(".", propertiesToken["CodeUri"]);
            Assert.Null(propertiesToken["ImageUri"]);
            Assert.Null(propertiesToken["ImageConfig"]);
        }

        [Fact]
        public void SwitchFromPolicyToRole()
        {
            // ARRANGE
            var mockFileManager = GetMockFileManager(string.Empty);
            var cloudFormationJsonWriter = new CloudFormationJsonWriter(mockFileManager, _mockDirectoryManager, _jsonWriter, _diagnosticReporter);

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
            var cloudFormationJsonWriter = new CloudFormationJsonWriter(mockFileManager, _mockDirectoryManager, _jsonWriter, _diagnosticReporter);
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
            var cloudFormationJsonWriter = new CloudFormationJsonWriter(mockFileManager, _mockDirectoryManager, _jsonWriter, _diagnosticReporter);
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
            var cloudFormationJsonWriter = new CloudFormationJsonWriter(mockFileManager, _mockDirectoryManager, _jsonWriter, _diagnosticReporter);
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
            var cloudFormationJsonWriter = new CloudFormationJsonWriter(mockFileManager, _mockDirectoryManager, _jsonWriter, _diagnosticReporter);
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
            var cloudFormationJsonWriter = new CloudFormationJsonWriter(mockFileManager, _mockDirectoryManager, _jsonWriter, _diagnosticReporter);
            var report = GetAnnotationReport(new() {lambdaFunctionModel});

            // ACT
            cloudFormationJsonWriter.ApplyReport(report);

            // ASSERT
            var rootToken = JObject.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));

            Assert.NotNull(rootToken["Resources"]["MethodNotCreatedFromAnnotationsPackage"]);
            Assert.Equal(128, rootToken["Resources"]["MethodNotCreatedFromAnnotationsPackage"]["Properties"]["MemorySize"]); // unchanged
            Assert.Equal(100, rootToken["Resources"]["MethodNotCreatedFromAnnotationsPackage"]["Properties"]["Timeout"]); // unchanged

            var policies = rootToken["Resources"]["MethodNotCreatedFromAnnotationsPackage"]["Properties"]["Policies"] as JArray;
            Assert.NotNull(policies);
            Assert.Single(policies);
            Assert.Equal("AWSLambdaBasicExecutionRole", policies[0]); // unchanged
        }

        [Fact]
        public void EventAttributesTest()
        {
            // ARRANGE - USE A HTTP GET METHOD
            var mockFileManager = GetMockFileManager(string.Empty);
            var lambdaFunctionModel = GetLambdaFunctionModel("MyAssembly::MyNamespace.MyType::Handler",
                "TestMethod", 45, 512, null, null);
            var httpAttributeModel = new AttributeModel<HttpApiAttribute>()
            {
                Data = new HttpApiAttribute(LambdaHttpMethod.Get, "/Calculator/Add")
                {
                    Version = HttpApiVersion.V1
                }
            };
            lambdaFunctionModel.Attributes = new List<AttributeModel>() {httpAttributeModel};
            var cloudFormationJsonWriter = new CloudFormationJsonWriter(mockFileManager, _mockDirectoryManager, _jsonWriter, _diagnosticReporter);
            var report = GetAnnotationReport(new List<ILambdaFunctionSerializable>() {lambdaFunctionModel});

            // ACT
            cloudFormationJsonWriter.ApplyReport(report);

            // ASSERT
            var rootToken = JObject.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));
            var getToken = rootToken["Resources"]["TestMethod"]["Properties"]["Events"]["RootGet"];

            Assert.NotNull(getToken);
            Assert.Equal("HttpApi", getToken["Type"]);
            Assert.Equal("/Calculator/Add", getToken["Properties"]["Path"]);
            Assert.Equal("GET", getToken["Properties"]["Method"]);
            Assert.Equal("1.0", getToken["Properties"]["PayloadFormatVersion"]);

            // ARRANGE - CHANGE TO A HTTP POST METHOD
            httpAttributeModel = new AttributeModel<HttpApiAttribute>()
            {
                Data = new HttpApiAttribute(LambdaHttpMethod.Post, "/Calculator/Add")
            };
            lambdaFunctionModel.Attributes = new List<AttributeModel>() {httpAttributeModel};

            // ACT
            cloudFormationJsonWriter.ApplyReport(report);

            // ASSERT
            rootToken = JObject.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));
            getToken = rootToken["Resources"]["TestMethod"]["Properties"]["Events"]["RootGet"];
            var postToken = rootToken["Resources"]["TestMethod"]["Properties"]["Events"]["RootPost"];

            Assert.Null(getToken); // Verify that the HTTP GET method entry is deleted
            Assert.NotNull(postToken);
            Assert.Equal("HttpApi", postToken["Type"]);
            Assert.Equal("/Calculator/Add", postToken["Properties"]["Path"]);
            Assert.Equal("POST", postToken["Properties"]["Method"]);
            Assert.Equal("2.0", postToken["Properties"]["PayloadFormatVersion"]);
        }

        [Fact]
        public void PackageTypePropertyTest()
        {
            // ARRANGE - Set PackageType to Zip
            var mockFileManager = GetMockFileManager(string.Empty);
            var lambdaFunctionModel = GetLambdaFunctionModel("MyAssembly::MyNamespace.MyType::Handler",
                "TestMethod", 45, 512, null, null);
            lambdaFunctionModel.PackageType = LambdaPackageType.Zip;
            var cloudFormationJsonWriter = new CloudFormationJsonWriter(mockFileManager, _mockDirectoryManager, _jsonWriter, _diagnosticReporter);
            var report = GetAnnotationReport(new List<ILambdaFunctionSerializable>() {lambdaFunctionModel});

            // ACT
            cloudFormationJsonWriter.ApplyReport(report);

            // ASSERT
            var rootToken = JObject.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));
            var propertiesToken = rootToken["Resources"]["TestMethod"]["Properties"];
            Assert.Equal("Zip", propertiesToken["PackageType"]);
            Assert.Equal(".", propertiesToken["CodeUri"]);
            Assert.Equal("MyAssembly::MyNamespace.MyType::Handler", propertiesToken["Handler"]);

            // ARRANGE - Change PackageType to Image
            lambdaFunctionModel.PackageType = LambdaPackageType.Image;
            report = GetAnnotationReport(new List<ILambdaFunctionSerializable>() {lambdaFunctionModel});

            // ACT
            cloudFormationJsonWriter.ApplyReport(report);

            // ASSERT
            rootToken = JObject.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));
            propertiesToken = rootToken["Resources"]["TestMethod"]["Properties"];
            Assert.Equal("Image", propertiesToken["PackageType"]);
            Assert.Equal(".", propertiesToken["ImageUri"]);
            Assert.Equal(new JArray("MyAssembly::MyNamespace.MyType::Handler"), propertiesToken["ImageConfig"]["Command"]);
            Assert.Null(propertiesToken["CodeUri"]);
            Assert.Null(propertiesToken["Handler"]);

            // ARRANGE - Change PackageType back to Zip
            lambdaFunctionModel.PackageType = LambdaPackageType.Zip;
            report = GetAnnotationReport(new List<ILambdaFunctionSerializable>() {lambdaFunctionModel});

            // ACT
            cloudFormationJsonWriter.ApplyReport(report);

            // ASSERT
            rootToken = JObject.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));
            propertiesToken = rootToken["Resources"]["TestMethod"]["Properties"];
            Assert.Equal("Zip", propertiesToken["PackageType"]);
            Assert.Equal(".", propertiesToken["CodeUri"]);
            Assert.Equal("MyAssembly::MyNamespace.MyType::Handler", propertiesToken["Handler"]);
            Assert.Null(propertiesToken["ImageUri"]);
            Assert.Null(propertiesToken["ImageConfig"]);
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
                CloudFormationTemplatePath = ServerlessTemplateFilePath,
                ProjectRootDirectory = ProjectRootDirectory
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
            public IList<AttributeModel> Attributes { get; set; } = new List<AttributeModel>();
            public string SourceGeneratorVersion { get; set; }
            public LambdaPackageType PackageType { get; set; } = LambdaPackageType.Zip;
        }
    }
}