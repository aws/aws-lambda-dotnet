using System.Collections.Generic;
using System.IO;
using System.Linq;
using Amazon.Lambda.Annotations.APIGateway;
using Amazon.Lambda.Annotations.SourceGenerator;
using Amazon.Lambda.Annotations.SourceGenerator.Diagnostics;
using Amazon.Lambda.Annotations.SourceGenerator.FileIO;
using Amazon.Lambda.Annotations.SourceGenerator.Models;
using Amazon.Lambda.Annotations.SourceGenerator.Models.Attributes;
using Amazon.Lambda.Annotations.SourceGenerator.Writers;
using Amazon.Lambda.Annotations.SQS;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Amazon.Lambda.Annotations.SourceGenerators.Tests.WriterTests
{
    public class CloudFormationWriterTests
    {
        private readonly IDirectoryManager _directoryManager = new DirectoryManager();
        private readonly IDiagnosticReporter _diagnosticReporter = new Mock<IDiagnosticReporter>().Object;
        private const string ProjectRootDirectory = "C:/CodeBase/MyProject";
        private const string ServerlessTemplateFilePath = "C:/CodeBase/MyProject/serverless.template";

        [Theory]
        [InlineData(CloudFormationTemplateFormat.Json)]
        [InlineData(CloudFormationTemplateFormat.Yaml)]
        public void ApplyLambdaFunctionDefaultProperties(CloudFormationTemplateFormat templateFormat)
        {
            // ARRANGE
            var mockFileManager = GetMockFileManager(string.Empty);
            var lambdaFunctionModel = GetLambdaFunctionModel("MyAssembly::MyNamespace.MyType::Handler",
                "TestMethod", 0, 0, null, null);
            var cloudFormationWriter = GetCloudFormationWriter(mockFileManager, _directoryManager, templateFormat, _diagnosticReporter);
            var report = GetAnnotationReport(new List<ILambdaFunctionSerializable>() { lambdaFunctionModel });
            ITemplateWriter templateWriter = templateFormat == CloudFormationTemplateFormat.Json ? new JsonWriter() : new YamlWriter();

            // ACT
            cloudFormationWriter.ApplyReport(report);

            // ASSERT
            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));
            const string functionPath = "Resources.TestMethod";
            const string propertiesPath = "Resources.TestMethod.Properties";

            Assert.Equal("AWS::Serverless::Function", templateWriter.GetToken<string>($"{functionPath}.Type"));
            Assert.Equal("Amazon.Lambda.Annotations", templateWriter.GetToken<string>($"{functionPath}.Metadata.Tool"));

            Assert.Equal("MyAssembly::MyNamespace.MyType::Handler", templateWriter.GetToken<string>($"{propertiesPath}.Handler"));
            Assert.Equal(512, templateWriter.GetToken<int>($"{propertiesPath}.MemorySize"));
            Assert.Equal(30, templateWriter.GetToken<int>($"{propertiesPath}.Timeout"));
            Assert.Equal(new List<string> { "AWSLambdaBasicExecutionRole" }, templateWriter.GetToken<List<string>>($"{propertiesPath}.Policies"));
            Assert.Equal("Zip", templateWriter.GetToken<string>($"{propertiesPath}.PackageType"));
            Assert.Equal(".", templateWriter.GetToken<string>($"{propertiesPath}.CodeUri"));
            Assert.False(templateWriter.Exists("ImageUri"));
            Assert.False(templateWriter.Exists("ImageConfig"));
        }

        [Theory]
        [InlineData(CloudFormationTemplateFormat.Json)]
        [InlineData(CloudFormationTemplateFormat.Yaml)]
        public void AddSingletonFunctionToEmptyTemplate(CloudFormationTemplateFormat templateFormat)
        {
            // ARRANGE
            var mockFileManager = GetMockFileManager(string.Empty);
            var lambdaFunctionModel = GetLambdaFunctionModel("MyAssembly::MyNamespace.MyType::Handler",
                "TestMethod", 45, 512, null, null);
            var cloudFormationWriter = GetCloudFormationWriter(mockFileManager, _directoryManager, templateFormat, _diagnosticReporter);
            var report = GetAnnotationReport(new List<ILambdaFunctionSerializable>() {lambdaFunctionModel});
            ITemplateWriter templateWriter = templateFormat == CloudFormationTemplateFormat.Json ? new JsonWriter() : new YamlWriter();

            // ACT
            cloudFormationWriter.ApplyReport(report);

            // ASSERT
            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));
            const string functionPath = "Resources.TestMethod";
            const string propertiesPath = "Resources.TestMethod.Properties";

            Assert.Equal("AWS::Serverless::Function", templateWriter.GetToken<string>($"{functionPath}.Type"));
            Assert.Equal("Amazon.Lambda.Annotations", templateWriter.GetToken<string>($"{functionPath}.Metadata.Tool"));

            Assert.Equal("MyAssembly::MyNamespace.MyType::Handler", templateWriter.GetToken<string>($"{propertiesPath}.Handler"));
            Assert.Equal(512, templateWriter.GetToken<int>($"{propertiesPath}.MemorySize"));
            Assert.Equal(45, templateWriter.GetToken<int>($"{propertiesPath}.Timeout"));
            Assert.Equal(new List<string> {"AWSLambdaBasicExecutionRole"}, templateWriter.GetToken<List<string>>($"{propertiesPath}.Policies"));
            Assert.Equal("Zip", templateWriter.GetToken<string>($"{propertiesPath}.PackageType"));
            Assert.Equal(".", templateWriter.GetToken<string>($"{propertiesPath}.CodeUri"));
            Assert.False(templateWriter.Exists("ImageUri"));
            Assert.False(templateWriter.Exists("ImageConfig"));
        }

        [Theory]
        [InlineData(CloudFormationTemplateFormat.Json)]
        [InlineData(CloudFormationTemplateFormat.Yaml)]
        public void SwitchFromPolicyToRole(CloudFormationTemplateFormat templateFormat)
        {
            // ARRANGE
            var mockFileManager = GetMockFileManager(string.Empty);
            var cloudFormationWriter = GetCloudFormationWriter(mockFileManager, _directoryManager, templateFormat, _diagnosticReporter);
            ITemplateWriter templateWriter = templateFormat == CloudFormationTemplateFormat.Json ? new JsonWriter() : new YamlWriter();
            const string policiesPath = "Resources.TestMethod.Properties.Policies";
            const string rolePath = "Resources.TestMethod.Properties.Role";

            //ACT - USE POLICY
            var lambdaFunctionModel = GetLambdaFunctionModel("MyAssembly::MyNamespace.MyType::Handler",
                "TestMethod", 45, 512, null, "Policy1, Policy2, Policy3");
            var report = GetAnnotationReport(new List<ILambdaFunctionSerializable> {lambdaFunctionModel});
            cloudFormationWriter.ApplyReport(report);

            // ASSERT
            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));
            Assert.Equal(new List<string> {"Policy1", "Policy2", "Policy3"}, templateWriter.GetToken<List<string>>(policiesPath));
            Assert.False(templateWriter.Exists(rolePath));

            // ACT - SWITCH TO ROLE
            lambdaFunctionModel = GetLambdaFunctionModel("MyAssembly::MyNamespace.MyType::Handler",
                "TestMethod", 45, 512, "Basic", null);
            report = GetAnnotationReport(new List<ILambdaFunctionSerializable> {lambdaFunctionModel});
            cloudFormationWriter.ApplyReport(report);

            // ASSERT
            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));
            Assert.Equal("Basic", templateWriter.GetToken<string>(rolePath));
            Assert.False(templateWriter.Exists(policiesPath));

            // ACT - SWITCH BACK TO POLICY
            lambdaFunctionModel = GetLambdaFunctionModel("MyAssembly::MyNamespace.MyType::Handler",
                "TestMethod", 45, 512, null, "Policy1, Policy2, Policy3");
            report = GetAnnotationReport(new List<ILambdaFunctionSerializable> {lambdaFunctionModel});
            cloudFormationWriter.ApplyReport(report);

            // ASSERT
            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));
            Assert.Equal(new List<string> {"Policy1", "Policy2", "Policy3"}, templateWriter.GetToken<List<string>>(policiesPath));
            Assert.False(templateWriter.Exists(rolePath));
        }

        [Theory]
        [InlineData(CloudFormationTemplateFormat.Json)]
        [InlineData(CloudFormationTemplateFormat.Yaml)]
        public void UseRefForPolicies(CloudFormationTemplateFormat templateFormat)
        {
            // ARRANGE
            var mockFileManager = GetMockFileManager(string.Empty);
            var lambdaFunctionModel = GetLambdaFunctionModel("MyAssembly::MyNamespace.MyType::Handler",
                "TestMethod", 45, 512, null, "Policy1, @Policy2, Policy3");
            var cloudFormationWriter = GetCloudFormationWriter(mockFileManager, _directoryManager, templateFormat, _diagnosticReporter);
            var report = GetAnnotationReport(new List<ILambdaFunctionSerializable> {lambdaFunctionModel});
            ITemplateWriter templateWriter = templateFormat == CloudFormationTemplateFormat.Json ? new JsonWriter() : new YamlWriter();
            const string policiesPath = "Resources.TestMethod.Properties.Policies";

            // ACT
            cloudFormationWriter.ApplyReport(report);

            // ASSERT
            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));
            var policies = templateWriter.GetToken<List<object>>(policiesPath);
            Assert.Equal(3, policies.Count);
            Assert.Equal("Policy1", policies[0]);
            if (templateFormat == CloudFormationTemplateFormat.Json)
                Assert.Equal("Policy2", ((JObject)policies[1])["Ref"]);
            else
                Assert.Equal("Policy2", ((Dictionary<object, object>)policies[1])["Ref"]);
            Assert.Equal("Policy3", policies[2]);
        }

        [Theory]
        [InlineData(CloudFormationTemplateFormat.Json)]
        [InlineData(CloudFormationTemplateFormat.Yaml)]
        public void UseRefForRole(CloudFormationTemplateFormat templateFormat)
        {
            // ARRANGE
            const string jsonContent = @"{
                       'Parameters':{
                          'Basic':{
                             'Type':'String',
                          }
                       }
                    }";

            const string yamlContent = @"Parameters:
                                          Basic:
                                            Type: String";

            ITemplateWriter templateWriter = templateFormat == CloudFormationTemplateFormat.Json ? new JsonWriter() : new YamlWriter();
            var content = templateFormat == CloudFormationTemplateFormat.Json ? jsonContent : yamlContent;

            var mockFileManager = GetMockFileManager(content);
            var lambdaFunctionModel = GetLambdaFunctionModel("MyAssembly::MyNamespace.MyType::Handler",
                "TestMethod", 45, 512, "@Basic", null);
            var cloudFormationWriter = GetCloudFormationWriter(mockFileManager, _directoryManager, templateFormat, _diagnosticReporter);
            var report = GetAnnotationReport(new List<ILambdaFunctionSerializable> {lambdaFunctionModel});

            // ACT
            cloudFormationWriter.ApplyReport(report);

            // ASSERT
            const string rolePath = "Resources.TestMethod.Properties.Role.Ref";
            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));
            Assert.Equal("Basic", templateWriter.GetToken<string>(rolePath));
            Assert.False(templateWriter.Exists("Resources.TestMethod.Properties.Role.Fn::GetAtt"));
        }

        [Theory]
        [InlineData(CloudFormationTemplateFormat.Json)]
        [InlineData(CloudFormationTemplateFormat.Yaml)]
        public void UseFnGetForRole(CloudFormationTemplateFormat templateFormat)
        {
            ITemplateWriter templateWriter = templateFormat == CloudFormationTemplateFormat.Json ? new JsonWriter() : new YamlWriter();

            var mockFileManager = GetMockFileManager(string.Empty);
            var lambdaFunctionModel = GetLambdaFunctionModel("MyAssembly::MyNamespace.MyType::Handler",
                "TestMethod", 45, 512, "@Basic", null);
            var cloudFormationWriter = GetCloudFormationWriter(mockFileManager, _directoryManager, templateFormat, _diagnosticReporter);
            var report = GetAnnotationReport(new List<ILambdaFunctionSerializable> {lambdaFunctionModel});

            // ACT
            cloudFormationWriter.ApplyReport(report);

            // ASSERT
            const string rolePath = "Resources.TestMethod.Properties.Role.Fn::GetAtt";
            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));
            Assert.Equal(new List<string>{"Basic", "Arn"}, templateWriter.GetToken<List<string>>(rolePath));
            Assert.False(templateWriter.Exists("Resources.TestMethod.Properties.Role.Ref"));
        }

        [Theory]
        [InlineData(CloudFormationTemplateFormat.Json)]
        [InlineData(CloudFormationTemplateFormat.Yaml)]
        public void RenameFunction(CloudFormationTemplateFormat templateFormat)
        {
            // ARRANGE
            var mockFileManager = GetMockFileManager(string.Empty);
            var lambdaFunctionModel = GetLambdaFunctionModel("MyAssembly::MyNamespace.MyType::Handler",
                "OldName", 45, 512, "@Basic", null);
            var cloudFormationWriter = GetCloudFormationWriter(mockFileManager, _directoryManager, templateFormat, _diagnosticReporter);
            var report = GetAnnotationReport(new List<ILambdaFunctionSerializable> {lambdaFunctionModel});
            ITemplateWriter templateWriter = templateFormat == CloudFormationTemplateFormat.Json ? new JsonWriter() : new YamlWriter();

            // ACT
            cloudFormationWriter.ApplyReport(report);

            // ASSERT
            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));
            Assert.True(templateWriter.Exists("Resources.OldName"));
            Assert.Equal("MyAssembly::MyNamespace.MyType::Handler", templateWriter.GetToken<string>("Resources.OldName.Properties.Handler"));

            // ACT - CHANGE NAME
            lambdaFunctionModel.ResourceName = "NewName";
            cloudFormationWriter.ApplyReport(report);

            // ASSERT
            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));
            Assert.False(templateWriter.Exists("Resources.OldName"));
            Assert.True(templateWriter.Exists("Resources.NewName"));
            Assert.Equal("MyAssembly::MyNamespace.MyType::Handler", templateWriter.GetToken<string>("Resources.NewName.Properties.Handler"));
        }

        [Theory]
        [InlineData(CloudFormationTemplateFormat.Json)]
        [InlineData(CloudFormationTemplateFormat.Yaml)]
        public void RemoveOrphanedLambdaFunctions(CloudFormationTemplateFormat templateFormat)
        {
            // ARRANGE
            const string yamlContent = @"
                            AWSTemplateFormatVersion: '2010-09-09'
                            Transform: AWS::Serverless-2016-10-31
                            Resources:
                              ObsoleteMethod:
                                Type: AWS::Serverless::Function
                                Metadata:
                                  Tool: Amazon.Lambda.Annotations
                                Properties:
                                  Handler: MyAssembly::MyNamespace.MyType::Handler
                              MethodNotCreatedFromAnnotationsPackage:
                                Type: AWS::Serverless::Function
                                Properties:
                                  Handler: MyAssembly::MyNamespace.MyType::Handler
                            ";

            const string jsonContent = @"{
                              'AWSTemplateFormatVersion': '2010-09-09',
                              'Transform': 'AWS::Serverless-2016-10-31',
                              'Resources': {
                                'ObsoleteMethod': {
                                  'Type': 'AWS::Serverless::Function',
                                  'Metadata': {
                                    'Tool': 'Amazon.Lambda.Annotations'
                                  },
                                  'Properties': {
                                    'Handler': 'MyAssembly::MyNamespace.MyType::Handler'
                                  }
                                },
                                'MethodNotCreatedFromAnnotationsPackage': {
                                  'Type': 'AWS::Serverless::Function',
                                  'Properties': {
                                    'Handler': 'MyAssembly::MyNamespace.MyType::Handler'
                                  }
                                }
                              }
                            }";

            var content = templateFormat == CloudFormationTemplateFormat.Json ? jsonContent : yamlContent;
            ITemplateWriter templateWriter = templateFormat == CloudFormationTemplateFormat.Json ? new JsonWriter() : new YamlWriter();

            var mockFileManager = GetMockFileManager(content);
            var lambdaFunctionModel = GetLambdaFunctionModel("MyAssembly::MyNamespace.MyType::Handler",
                "NewMethod", 45, 512, null, null);
            var cloudFormationWriter = GetCloudFormationWriter(mockFileManager, _directoryManager, templateFormat, _diagnosticReporter);
            var report = GetAnnotationReport(new List<ILambdaFunctionSerializable> {lambdaFunctionModel});

            // ACT
            cloudFormationWriter.ApplyReport(report);

            // ASSERT
            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));
            Assert.False(templateWriter.Exists("Resources.ObsoleteMethod"));
            Assert.True(templateWriter.Exists("Resources.NewMethod"));
            Assert.True(templateWriter.Exists("Resources.MethodNotCreatedFromAnnotationsPackage"));
        }

        [Theory]
        [InlineData(CloudFormationTemplateFormat.Json)]
        [InlineData(CloudFormationTemplateFormat.Yaml)]
        public void DoNotModifyFunctionWithoutRequiredMetadata(CloudFormationTemplateFormat templateFormat)
        {
            // ARRANGE
            const string jsonContent = @"{
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

            const string yamlContent = @"
                        AWSTemplateFormatVersion: '2010-09-09'
                        Transform: AWS::Serverless-2016-10-31
                        Resources:
                          MethodNotCreatedFromAnnotationsPackage:
                            Type: AWS::Serverless::Function
                            Properties:
                              Runtime: dotnetcore3.1
                              CodeUri: ''
                              MemorySize: 128
                              Timeout: 100
                              Policies:
                                - AWSLambdaBasicExecutionRole
                              Handler: MyAssembly::MyNamespace.MyType::Handler
                        ";

            var content = templateFormat == CloudFormationTemplateFormat.Json ? jsonContent : yamlContent;
            ITemplateWriter templateWriter = templateFormat == CloudFormationTemplateFormat.Json ? new JsonWriter() : new YamlWriter();

            var mockFileManager = GetMockFileManager(content);
            var lambdaFunctionModel = GetLambdaFunctionModel("MyAssembly::MyNamespace.MyType::Handler",
                "MethodNotCreatedFromAnnotationsPackage", 45, 512, null, "Policy1, Policy2, Policy3");
            var cloudFormationWriter = GetCloudFormationWriter(mockFileManager, _directoryManager, templateFormat, _diagnosticReporter);
            var report = GetAnnotationReport(new List<ILambdaFunctionSerializable> {lambdaFunctionModel});

            // ACT
            cloudFormationWriter.ApplyReport(report);

            // ASSERT
            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));
            const string resourcePath = "Resources.MethodNotCreatedFromAnnotationsPackage";

            Assert.True(templateWriter.Exists(resourcePath));
            Assert.Equal(128, templateWriter.GetToken<int>($"{resourcePath}.Properties.MemorySize"));
            Assert.Equal(100, templateWriter.GetToken<int>($"{resourcePath}.Properties.Timeout"));// unchanged

            var policies = templateWriter.GetToken<List<string>>($"{resourcePath}.Properties.Policies");
            Assert.Equal(new List<string>{"AWSLambdaBasicExecutionRole"}, policies); // unchanged
        }

        [Theory]
        [InlineData(CloudFormationTemplateFormat.Json)]
        [InlineData(CloudFormationTemplateFormat.Yaml)]
        public void EventAttributesTest(CloudFormationTemplateFormat templateFormat)
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
            var cloudFormationWriter = GetCloudFormationWriter(mockFileManager, _directoryManager, templateFormat, _diagnosticReporter);
            var report = GetAnnotationReport(new List<ILambdaFunctionSerializable>() {lambdaFunctionModel});
            ITemplateWriter templateWriter = templateFormat == CloudFormationTemplateFormat.Json ? new JsonWriter() : new YamlWriter();

            // ACT
            cloudFormationWriter.ApplyReport(report);

            // ASSERT
            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));
            const string rootGetPath = "Resources.TestMethod.Properties.Events.RootGet";
            Assert.True(templateWriter.Exists(rootGetPath));
            Assert.Equal("HttpApi", templateWriter.GetToken<string>($"{rootGetPath}.Type"));
            Assert.Equal("/Calculator/Add", templateWriter.GetToken<string>($"{rootGetPath}.Properties.Path"));
            Assert.Equal("GET", templateWriter.GetToken<string>($"{rootGetPath}.Properties.Method"));
            Assert.Equal("1.0", templateWriter.GetToken<string>($"{rootGetPath}.Properties.PayloadFormatVersion"));

            // ARRANGE - CHANGE TO A HTTP POST METHOD
            httpAttributeModel = new AttributeModel<HttpApiAttribute>()
            {
                Data = new HttpApiAttribute(LambdaHttpMethod.Post, "/Calculator/Add")
            };
            lambdaFunctionModel.Attributes = new List<AttributeModel>() {httpAttributeModel};

            // ACT
            cloudFormationWriter.ApplyReport(report);

            // ASSERT
            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));
            const string rootPostPath = "Resources.TestMethod.Properties.Events.RootPost";
            Assert.True(templateWriter.Exists(rootPostPath));
            Assert.Equal("HttpApi", templateWriter.GetToken<string>($"{rootPostPath}.Type"));
            Assert.Equal("/Calculator/Add", templateWriter.GetToken<string>($"{rootPostPath}.Properties.Path"));
            Assert.Equal("POST", templateWriter.GetToken<string>($"{rootPostPath}.Properties.Method"));
            Assert.False(templateWriter.Exists($"{rootPostPath}.Properties.PayloadFormatVersion"));
        }

        [Theory]
        [InlineData(CloudFormationTemplateFormat.Json)]
        [InlineData(CloudFormationTemplateFormat.Yaml)]
        public void PackageTypePropertyTest(CloudFormationTemplateFormat templateFormat)
        {
            // ARRANGE - Set PackageType to Zip
            var mockFileManager = GetMockFileManager(string.Empty);
            var lambdaFunctionModel = GetLambdaFunctionModel("MyAssembly::MyNamespace.MyType::Handler",
                "TestMethod", 45, 512, null, null);
            lambdaFunctionModel.PackageType = LambdaPackageType.Zip;
            var cloudFormationWriter = GetCloudFormationWriter(mockFileManager, _directoryManager, templateFormat, _diagnosticReporter);
            var report = GetAnnotationReport(new List<ILambdaFunctionSerializable>() {lambdaFunctionModel});
            ITemplateWriter templateWriter = templateFormat == CloudFormationTemplateFormat.Json ? new JsonWriter() : new YamlWriter();

            // ACT
            cloudFormationWriter.ApplyReport(report);

            // ASSERT
            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));
            const string propertiesPath = "Resources.TestMethod.Properties";
            Assert.Equal("Zip", templateWriter.GetToken<string>($"{propertiesPath}.PackageType"));
            Assert.Equal(".", templateWriter.GetToken<string>($"{propertiesPath}.CodeUri"));
            Assert.Equal("MyAssembly::MyNamespace.MyType::Handler", templateWriter.GetToken<string>($"{propertiesPath}.Handler"));

            // ARRANGE - Change PackageType to Image
            lambdaFunctionModel.PackageType = LambdaPackageType.Image;
            report = GetAnnotationReport(new List<ILambdaFunctionSerializable>() {lambdaFunctionModel});

            // ACT
            cloudFormationWriter.ApplyReport(report);

            // ASSERT
            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));
            Assert.Equal("Image", templateWriter.GetToken<string>($"{propertiesPath}.PackageType"));
            Assert.Equal(".", templateWriter.GetToken<string>($"{propertiesPath}.ImageUri"));
            Assert.Equal(new List<string>{"MyAssembly::MyNamespace.MyType::Handler"}, templateWriter.GetToken<List<string>>($"{propertiesPath}.ImageConfig.Command"));
            Assert.False(templateWriter.Exists($"{propertiesPath}.CodeUri"));
            Assert.False(templateWriter.Exists($"{propertiesPath}.Handler"));

            // ARRANGE - Change PackageType back to Zip
            lambdaFunctionModel.PackageType = LambdaPackageType.Zip;
            report = GetAnnotationReport(new List<ILambdaFunctionSerializable>() {lambdaFunctionModel});

            // ACT
            cloudFormationWriter.ApplyReport(report);

            // ASSERT
            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));
            Assert.Equal("Zip", templateWriter.GetToken<string>($"{propertiesPath}.PackageType"));
            Assert.Equal(".", templateWriter.GetToken<string>($"{propertiesPath}.CodeUri"));
            Assert.Equal("MyAssembly::MyNamespace.MyType::Handler", templateWriter.GetToken<string>($"{propertiesPath}.Handler"));
            Assert.False(templateWriter.Exists($"{propertiesPath}.ImageUri"));
            Assert.False(templateWriter.Exists($"{propertiesPath}.ImageConfig"));
        }

        [Theory]
        [InlineData(CloudFormationTemplateFormat.Json)]
        [InlineData(CloudFormationTemplateFormat.Yaml)]
        public void CodeUriTest(CloudFormationTemplateFormat templateFormat)
        {
            // ARRANGE
            var mockFileManager = GetMockFileManager(string.Empty);
            var lambdaFunctionModel = GetLambdaFunctionModel("MyAssembly::MyNamespace.MyType::Handler",
                "TestMethod", 45, 512, null, null);
            lambdaFunctionModel.PackageType = LambdaPackageType.Zip;
            var cloudFormationWriter = GetCloudFormationWriter(mockFileManager, _directoryManager, templateFormat, _diagnosticReporter);

            // ARRANGE - CloudFormation template is inside project root directory
            var projectRoot = Path.Combine("C:", "src", "serverlessApp");
            var cloudFormationTemplatePath = Path.Combine(projectRoot, "templates", "serverless.template");
            var report = GetAnnotationReport(new List<ILambdaFunctionSerializable>{lambdaFunctionModel}, projectRoot, cloudFormationTemplatePath);
            ITemplateWriter templateWriter = templateFormat == CloudFormationTemplateFormat.Json ? new JsonWriter() : new YamlWriter();

            // ACT
            cloudFormationWriter.ApplyReport(report);

            // ASSERT - CodeUri is relative to CloudFormation template directory
            templateWriter.Parse(mockFileManager.ReadAllText(cloudFormationTemplatePath));
            const string propertiesPath = "Resources.TestMethod.Properties";
            Assert.Equal("..", templateWriter.GetToken<string>($"{propertiesPath}.CodeUri"));

            // ARRANGE - CloudFormation template is above project root directory
            projectRoot = Path.Combine("C:", "src", "serverlessApp");
            cloudFormationTemplatePath = Path.Combine(projectRoot, "..", "serverless.template");
            report = GetAnnotationReport(new List<ILambdaFunctionSerializable>{lambdaFunctionModel}, projectRoot, cloudFormationTemplatePath);

            // ACT
            cloudFormationWriter.ApplyReport(report);

            // ASSERT - CodeUri is relative to CloudFormation template directory
            templateWriter.Parse(mockFileManager.ReadAllText(cloudFormationTemplatePath));
            Assert.Equal("serverlessApp", templateWriter.GetToken<string>($"{propertiesPath}.CodeUri"));
        }

        [Theory]
        [ClassData(typeof(SqsEventsTestData))]
        public void SqsEventsTest(CloudFormationTemplateFormat templateFormat, List<SQSEventAttribute> sqsEventAttribute, string lambdaReturnType)
        {
            // ARRANGE
            var mockFileManager = GetMockFileManager(string.Empty);
            var lambdaFunctionModel = GetLambdaFunctionModel();
            lambdaFunctionModel.PackageType = LambdaPackageType.Zip;
            lambdaFunctionModel.ReturnTypeFullName = lambdaReturnType;
            foreach (var att in sqsEventAttribute)
            {
                lambdaFunctionModel.Attributes.Add(new AttributeModel<SQSEventAttribute> { Data = att });
            }
            var cloudFormationWriter = GetCloudFormationWriter(mockFileManager, _directoryManager, templateFormat, _diagnosticReporter);
            var report = GetAnnotationReport([lambdaFunctionModel]);

            // ACT
            cloudFormationWriter.ApplyReport(report);

            // ASSERT
            ITemplateWriter templateWriter = templateFormat == CloudFormationTemplateFormat.Json ? new JsonWriter() : new YamlWriter();
            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));

            foreach (var att in sqsEventAttribute)
            {
                var eventName = att.Queue.StartsWith("@") ? att.Queue.Substring(1) : att.Queue.Split(':').ToList()[5];
                var eventPath = $"Resources.{lambdaFunctionModel.ResourceName}.Properties.Events.{eventName}";
                var eventPropertiesPath = $"{eventPath}.Properties";

                Assert.True(templateWriter.Exists(eventPath));
                Assert.Equal("SQS", templateWriter.GetToken<string>($"{eventPath}.Type"));

                if (!att.Queue.StartsWith("@"))
                {
                    Assert.Equal(att.Queue, templateWriter.GetToken<string>($"{eventPropertiesPath}.Queue"));
                }
                else
                {
                    Assert.Equal([att.Queue.Substring(1), "Arn"], templateWriter.GetToken<List<string>>($"{eventPropertiesPath}.Queue.Fn::GetAtt"));
                }

                Assert.Equal(att.IsBatchSizeSet, templateWriter.Exists($"{eventPropertiesPath}.BatchSize"));
                if (att.IsBatchSizeSet)
                {
                    Assert.Equal(att.BatchSize, templateWriter.GetToken<uint>($"{eventPropertiesPath}.BatchSize"));
                }

                Assert.Equal(att.IsEnabledSet, templateWriter.Exists($"{eventPropertiesPath}.Enabled"));
                if (att.IsEnabledSet)
                {
                    Assert.Equal(att.Enabled, templateWriter.GetToken<bool>($"{eventPropertiesPath}.Enabled"));
                }

                Assert.Equal(att.IsFiltersSet, templateWriter.Exists($"{eventPropertiesPath}.FilterCriteria"));
                if (att.IsFiltersSet)
                {
                    var filtersList = templateWriter.GetToken<List<Dictionary<string, string>>>($"{eventPropertiesPath}.FilterCriteria.Filters");
                    var index = 0;
                    foreach (var filter in att.Filters.Split(';').Select(x => x.Trim()))
                    {
                        Assert.Equal(filter, filtersList[index]["Pattern"]);
                        index++;
                    }
                }

                Assert.Equal(lambdaReturnType.Contains(TypeFullNames.SQSBatchResponse), templateWriter.Exists($"{eventPropertiesPath}.FunctionResponseTypes"));
                if (lambdaReturnType.Contains(TypeFullNames.SQSBatchResponse))
                {
                    Assert.Equal(["ReportBatchItemFailures"], templateWriter.GetToken<List<string>>($"{eventPropertiesPath}.FunctionResponseTypes"));
                }

                Assert.Equal(att.IsMaximumBatchingWindowInSecondsSet, templateWriter.Exists($"{eventPropertiesPath}.MaximumBatchingWindowInSeconds"));
                if (att.IsMaximumBatchingWindowInSecondsSet)
                {
                    Assert.Equal(att.MaximumBatchingWindowInSeconds, templateWriter.GetToken<uint>($"{eventPropertiesPath}.MaximumBatchingWindowInSeconds"));
                }

                Assert.Equal(att.IsMaximumConcurrencySet, templateWriter.Exists($"{eventPropertiesPath}.ScalingConfig"));
                if (att.IsMaximumConcurrencySet)
                {
                    Assert.Equal(att.MaximumConcurrency, templateWriter.GetToken<uint>($"{eventPropertiesPath}.ScalingConfig.MaximumConcurrency"));
                }
            }
        }

        #region CloudFormation template description

        /// <summary>
        /// Tests that the CloudFormation template's "Description" field is set
        /// correctly for an entirely new template.
        /// </summary>
        [Theory]
        [InlineData(CloudFormationTemplateFormat.Json, false)]
        [InlineData(CloudFormationTemplateFormat.Json, true)]
        [InlineData(CloudFormationTemplateFormat.Yaml, false)]
        [InlineData(CloudFormationTemplateFormat.Yaml, true)]
        public void TemplateDescription_NewTemplate(CloudFormationTemplateFormat templateFormat, bool isTelemetrySuppressed)
        {
            ITemplateWriter templateWriter = templateFormat == CloudFormationTemplateFormat.Json ? new JsonWriter() : new YamlWriter();
            var mockFileManager = GetMockFileManager(string.Empty);
            var cloudFormationWriter = GetCloudFormationWriter(mockFileManager, _directoryManager, templateFormat, _diagnosticReporter);

            var lambdaFunctionModel = GetLambdaFunctionModel("MyAssembly::MyNamespace.MyType::Handler", "TestMethod", 45, 512, null, null);
            var report = GetAnnotationReport(new List<ILambdaFunctionSerializable> { lambdaFunctionModel }, isTelemetrySuppressed: isTelemetrySuppressed);

            cloudFormationWriter.ApplyReport(report);

            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));

            if (isTelemetrySuppressed)
            {
                Assert.False(templateWriter.Exists("Description"));
            }
            else
            {
                Assert.True(templateWriter.Exists("Description"));
                Assert.Equal(CloudFormationWriter.CurrentDescriptionSuffix, templateWriter.GetToken<string>("Description"));
            }
        }

        /// <summary>
        /// Tests that the CloudFormation template's "Description" field is set
        /// correctly for an existing template without a Description field.
        /// </summary>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void TemplateDescription_ExistingTemplateNoDescription_Json(bool isTelemetrySuppressed)
        {
            const string content = @"{
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

            var templateWriter = new JsonWriter();
            var mockFileManager = GetMockFileManager(content);
            var cloudFormationWriter = GetCloudFormationWriter(mockFileManager, _directoryManager, CloudFormationTemplateFormat.Json, _diagnosticReporter);

            var lambdaFunctionModel = GetLambdaFunctionModel("MyAssembly::MyNamespace.MyType::Handler", "TestMethod", 45, 512, null, null);
            var report = GetAnnotationReport(new List<ILambdaFunctionSerializable> { lambdaFunctionModel }, isTelemetrySuppressed: isTelemetrySuppressed);

            cloudFormationWriter.ApplyReport(report);

            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));

            if (isTelemetrySuppressed)
            {
                Assert.False(templateWriter.Exists("Description"));
            }
            else
            {
                Assert.True(templateWriter.Exists("Description"));
                Assert.Equal(CloudFormationWriter.CurrentDescriptionSuffix, templateWriter.GetToken<string>("Description"));
            }
        }

        /// <summary>
        /// Tests that the CloudFormation template's "Description" field is set
        /// correctly for an existing template without a Description field.
        /// </summary>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void TemplateDescription_ExistingTemplateNoDescription_Yaml(bool isTelemetrySuppressed)
        {
            const string content = @"
                        AWSTemplateFormatVersion: '2010-09-09'
                        Transform: AWS::Serverless-2016-10-31
                        Resources:
                          MethodNotCreatedFromAnnotationsPackage:
                            Type: AWS::Serverless::Function
                            Properties:
                              Runtime: dotnetcore3.1
                              CodeUri: ''
                              MemorySize: 128
                              Timeout: 100
                              Policies:
                                - AWSLambdaBasicExecutionRole
                              Handler: MyAssembly::MyNamespace.MyType::Handler
                                ";

            var templateWriter = new YamlWriter();
            var mockFileManager = GetMockFileManager(content);
            var cloudFormationWriter = GetCloudFormationWriter(mockFileManager, _directoryManager, CloudFormationTemplateFormat.Yaml, _diagnosticReporter);

            var lambdaFunctionModel = GetLambdaFunctionModel("MyAssembly::MyNamespace.MyType::Handler", "TestMethod", 45, 512, null, null);
            var report = GetAnnotationReport(new List<ILambdaFunctionSerializable> { lambdaFunctionModel }, isTelemetrySuppressed: isTelemetrySuppressed);

            cloudFormationWriter.ApplyReport(report);

            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));

            if (isTelemetrySuppressed)
            {
                Assert.False(templateWriter.Exists("Description"));
            }
            else
            {
                Assert.True(templateWriter.Exists("Description"));
                Assert.Equal(CloudFormationWriter.CurrentDescriptionSuffix, templateWriter.GetToken<string>("Description"));
            }
        }

        /// <summary>
        /// Test cases for manipulating the CloudFormation template description if it already has a value
        /// </summary>
        public static IEnumerable<object[]> CloudFormationDescriptionCases
            => new List<object[]> {

                /*
                 * This first set are without the opt-out flag
                 */

                // A blank description should be transformed to just our suffix
                new object[] { "", false, CloudFormationWriter.CurrentDescriptionSuffix },

                // An existing description that is entirely our suffix should be replaced by the current version
                new object[] { "This template is partially managed by Amazon.Lambda.Annotations (v0.1).",
                    false, CloudFormationWriter.CurrentDescriptionSuffix },

                // An existing description should have our version appended to it
                new object[] { "Existing description before",
                    false, $"Existing description before {CloudFormationWriter.CurrentDescriptionSuffix}" },

                // An existing description with our version in the front should be replaced
                new object[] { "This template is partially managed by Amazon.Lambda.Annotations (v0.1). Existing description.",
                     false, $"{CloudFormationWriter.CurrentDescriptionSuffix} Existing description." },

                // An existing description with our version in the front should be replaced
                new object[] { "PREFIX This template is partially managed by Amazon.Lambda.Annotations (v0.1). SUFFIX",
                     false, $"PREFIX {CloudFormationWriter.CurrentDescriptionSuffix} SUFFIX" },

                // This would exceed CloudFormation's current limit on the description field, so should not be modified
                new object[] { new string('-', 1000), false, new string('-', 1000)},

                /*
                 * The remaining cases are with the opt-out flag set to true, which should remove any version descriptions
                 */

                // A blank description should be left alone
                new object[] { "", true, "" },

                // A non-blank description without our version description should be left alone
                new object[] { "An AWS Serverless Application.", true, "An AWS Serverless Application." },

                // An existing description that is entirely our suffix should be cleared
                new object[] { "This template is partially managed by Amazon.Lambda.Annotations (v0.1).", true, "" },

                // An existing description with our version suffix should have it removed
                new object[] { "Existing description. This template is partially managed by Amazon.Lambda.Annotations (v0.1).",
                     true, "Existing description. " },

                // An existing description with our version in the front should have it removed
                new object[] { "This template is partially managed by Amazon.Lambda.Annotations (v0.1). Existing description.",
                     true, " Existing description." },

                // An existing description with our version in the front should be replaced
                new object[] { "PREFIX This template is partially managed by Amazon.Lambda.Annotations (v0.1). SUFFIX",
                     true, $"PREFIX  SUFFIX" }
        };

        /// <summary>
        /// Tests that the CloudFormation template's "Description" field is set
        /// correctly for an existing template without a Description field.
        /// </summary>
        [Theory]
        [MemberData(nameof(CloudFormationDescriptionCases))]
        public void TemplateDescription_ExistingDescription_Json(string originalDescription, bool isTelemetrySuppressed, string expectedDescription)
        {
            string content = @"{
                              'AWSTemplateFormatVersion': '2010-09-09',
                              'Transform': 'AWS::Serverless-2016-10-31',
                              'Description': '" + originalDescription + @"',
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

            var templateWriter = new JsonWriter();
            var mockFileManager = GetMockFileManager(content);
            var cloudFormationWriter = GetCloudFormationWriter(mockFileManager, _directoryManager, CloudFormationTemplateFormat.Json, _diagnosticReporter);

            var lambdaFunctionModel = GetLambdaFunctionModel("MyAssembly::MyNamespace.MyType::Handler", "TestMethod", 45, 512, null, null);
            var report = GetAnnotationReport(new List<ILambdaFunctionSerializable> { lambdaFunctionModel }, isTelemetrySuppressed: isTelemetrySuppressed);

            cloudFormationWriter.ApplyReport(report);

            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));

            Assert.True(templateWriter.Exists("Description"));
            Assert.Equal(expectedDescription, templateWriter.GetToken<string>("Description"));
        }

        /// <summary>
        /// Tests that the CloudFormation template's "Description" field is set
        /// correctly for an existing template without a Description field.
        /// </summary>
        [Theory]
        [MemberData(nameof(CloudFormationDescriptionCases))]
        public void TemplateDescription_ExistingDescription_Yaml(string originalDescription, bool isTelemetrySuppressed, string expectedDescription)
        {
            // ARRANGE
            string content = @"
                        AWSTemplateFormatVersion: '2010-09-09'
                        Transform: AWS::Serverless-2016-10-31
                        Description: " + originalDescription + @"
                        Resources:
                          MethodNotCreatedFromAnnotationsPackage:
                            Type: AWS::Serverless::Function
                            Properties:
                              Runtime: dotnetcore3.1
                              CodeUri: ''
                              MemorySize: 128
                              Timeout: 100
                              Policies:
                                - AWSLambdaBasicExecutionRole
                              Handler: MyAssembly::MyNamespace.MyType::Handler
                                ";

            var templateWriter = new YamlWriter();
            var mockFileManager = GetMockFileManager(content);
            var cloudFormationWriter = GetCloudFormationWriter(mockFileManager, _directoryManager, CloudFormationTemplateFormat.Yaml, _diagnosticReporter);

            var lambdaFunctionModel = GetLambdaFunctionModel("MyAssembly::MyNamespace.MyType::Handler", "TestMethod", 45, 512, null, null);
            var report = GetAnnotationReport(new List<ILambdaFunctionSerializable> { lambdaFunctionModel }, isTelemetrySuppressed: isTelemetrySuppressed);

            cloudFormationWriter.ApplyReport(report);

            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));

            Assert.True(templateWriter.Exists("Description"));
            Assert.Equal(expectedDescription, templateWriter.GetToken<string>("Description"));
        }

        #endregion

        private IFileManager GetMockFileManager(string originalContent)
        {
            var mockFileManager = new InMemoryFileManager();
            mockFileManager.WriteAllText(ServerlessTemplateFilePath, originalContent);
            return mockFileManager;
        }

        private LambdaFunctionModelTest GetLambdaFunctionModel(string handler = "MyAssembly::MyNamespace.MyType::Handler", 
            string resourceName = "TestMethod",
            uint timeout = 30,
            uint memorySize = 512, 
            string? role = null, 
            string? policies = "AWSLambdaBasicExecutionRole")
        {
            return new LambdaFunctionModelTest
            {
                Handler = handler,
                ResourceName = resourceName,
                MemorySize = memorySize,
                Timeout = timeout,
                Policies = policies,
                Role = role
            };
        }

        private AnnotationReport GetAnnotationReport(List<ILambdaFunctionSerializable> lambdaFunctionModels,
            string projectRootDirectory = ProjectRootDirectory, string cloudFormationTemplatePath = ServerlessTemplateFilePath, bool isTelemetrySuppressed = false)
        {
            var annotationReport = new AnnotationReport
            {
                CloudFormationTemplatePath = cloudFormationTemplatePath,
                ProjectRootDirectory = projectRootDirectory,
                IsTelemetrySuppressed = isTelemetrySuppressed
            };
            foreach (var model in lambdaFunctionModels)
            {
                annotationReport.LambdaFunctions.Add(model);
            }

            return annotationReport;
        }

        private CloudFormationWriter GetCloudFormationWriter(IFileManager fileManager, IDirectoryManager directoryManager, CloudFormationTemplateFormat templateFormat, IDiagnosticReporter diagnosticReporter)
        {
            ITemplateWriter templateWriter = templateFormat == CloudFormationTemplateFormat.Json ? new JsonWriter() : new YamlWriter();
            return new CloudFormationWriter(fileManager, directoryManager, templateWriter, diagnosticReporter);
        }

        public class SqsEventsTestData : TheoryData<CloudFormationTemplateFormat, List<SQSEventAttribute>, string>
        {
            const string queueArn1 = "arn:aws:sqs:us-east-2:444455556666:queue1";
            const string queueArn2 = "arn:aws:sqs:us-east-2:444455556666:queue2";

            public SqsEventsTestData()
            {
                foreach (var templateFormat in new List<CloudFormationTemplateFormat> { CloudFormationTemplateFormat.Json, CloudFormationTemplateFormat.Yaml })
                {
                    // Simple attribute
                    Add(templateFormat, [new(queueArn1)], "void");

                    // Report batch failure items.
                    Add(templateFormat, [new(queueArn1)], TypeFullNames.SQSBatchResponse);

                    // Mutliple SQSEvent attributes
                    Add(templateFormat, [new(queueArn1), new(queueArn2)], TypeFullNames.SQSBatchResponse);

                    // Use queue reference
                    Add(templateFormat, [new("@MyQueue")], TypeFullNames.SQSBatchResponse);

                    // Use both ARN and queue reference
                    Add(templateFormat, [new(queueArn1), new("@MyQueue")], "void");

                    // Specify filters
                    Add(templateFormat, [new(queueArn1) { Filters = "SOME-FILTER1; SOME-FILTER2"}, ], "void");

                    // Explicitly specify all properties
                    Add(templateFormat, 
                        [new(queueArn1) 
                        {
                            BatchSize = 10,
                            MaximumConcurrency = 30,
                            Filters = "SOME-FILTER1; SOME-FILTER2",
                            MaximumBatchingWindowInSeconds = 15,
                            Enabled = false
                        }],
                        TypeFullNames.SQSBatchResponse);
                }
            }
        }

        public class LambdaFunctionModelTest : ILambdaFunctionSerializable
        {
            public string MethodName { get; set; }
            public string Handler { get; set; }
            public bool IsExecutable { get; set; }
            public string ResourceName { get; set; }
            public uint? Timeout { get; set; }
            public uint? MemorySize { get; set; }
            public string Role { get; set; }
            public string Policies { get; set; }
            public string Runtime { get; set; }
            public IList<AttributeModel> Attributes { get; set; } = new List<AttributeModel>();
            public string SourceGeneratorVersion { get; set; }
            public LambdaPackageType PackageType { get; set; } = LambdaPackageType.Zip;
            public string ReturnTypeFullName { get; set; } = "void";
        }
    }
}