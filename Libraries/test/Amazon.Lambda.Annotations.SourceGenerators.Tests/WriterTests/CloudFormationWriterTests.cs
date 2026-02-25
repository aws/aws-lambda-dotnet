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
    public partial class CloudFormationWriterTests
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
            string role = null, 
            string policies = "AWSLambdaBasicExecutionRole")
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
            return GetAnnotationReport(lambdaFunctionModels, new List<AuthorizerModel>(), projectRootDirectory, cloudFormationTemplatePath, isTelemetrySuppressed);
        }

        private AnnotationReport GetAnnotationReport(List<ILambdaFunctionSerializable> lambdaFunctionModels,
            List<AuthorizerModel> authorizers,
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
            foreach (var authorizer in authorizers)
            {
                annotationReport.Authorizers.Add(authorizer);
            }

            return annotationReport;
        }

        private CloudFormationWriter GetCloudFormationWriter(IFileManager fileManager, IDirectoryManager directoryManager, CloudFormationTemplateFormat templateFormat, IDiagnosticReporter diagnosticReporter)
        {
            ITemplateWriter templateWriter = templateFormat == CloudFormationTemplateFormat.Json ? new JsonWriter() : new YamlWriter();
            return new CloudFormationWriter(fileManager, directoryManager, templateWriter, diagnosticReporter);
        }

        #region Authorizer Tests

        [Theory]
        [InlineData(CloudFormationTemplateFormat.Json)]
        [InlineData(CloudFormationTemplateFormat.Yaml)]
        public void HttpApiAuthorizerProcessing(CloudFormationTemplateFormat templateFormat)
        {
            // ARRANGE
            var mockFileManager = GetMockFileManager(string.Empty);
            var lambdaFunctionModel = GetLambdaFunctionModel("MyAssembly::MyNamespace.MyType::Authorize",
                "AuthorizerFunction", 30, 512, null, null);
            var authorizer = new AuthorizerModel
            {
                Name = "MyHttpAuthorizer",
                LambdaResourceName = "AuthorizerFunction",
                AuthorizerType = AuthorizerType.HttpApi,
                IdentityHeader = "Authorization",
                ResultTtlInSeconds = 0,
                EnableSimpleResponses = true,
                PayloadFormatVersion = "2.0"
            };
            var cloudFormationWriter = GetCloudFormationWriter(mockFileManager, _directoryManager, templateFormat, _diagnosticReporter);
            var report = GetAnnotationReport(
                new List<ILambdaFunctionSerializable> { lambdaFunctionModel },
                new List<AuthorizerModel> { authorizer });
            ITemplateWriter templateWriter = templateFormat == CloudFormationTemplateFormat.Json ? new JsonWriter() : new YamlWriter();

            // ACT
            cloudFormationWriter.ApplyReport(report);

            // ASSERT
            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));

            // Verify AnnotationsHttpApi resource was created
            const string httpApiPath = "Resources.AnnotationsHttpApi";
            Assert.True(templateWriter.Exists(httpApiPath));
            Assert.Equal("AWS::Serverless::HttpApi", templateWriter.GetToken<string>($"{httpApiPath}.Type"));
            Assert.Equal("Amazon.Lambda.Annotations", templateWriter.GetToken<string>($"{httpApiPath}.Metadata.Tool"));

            // Verify authorizer configuration
            const string authorizerPath = "Resources.AnnotationsHttpApi.Properties.Auth.Authorizers.MyHttpAuthorizer";
            Assert.True(templateWriter.Exists(authorizerPath));
            Assert.Equal(new List<string> { "AuthorizerFunction", "Arn" }, templateWriter.GetToken<List<string>>($"{authorizerPath}.FunctionArn.Fn::GetAtt"));
            Assert.Equal("2.0", templateWriter.GetToken<string>($"{authorizerPath}.AuthorizerPayloadFormatVersion"));
            Assert.True(templateWriter.GetToken<bool>($"{authorizerPath}.EnableSimpleResponses"));
            Assert.Equal(new List<string> { "Authorization" }, templateWriter.GetToken<List<string>>($"{authorizerPath}.Identity.Headers"));
            Assert.True(templateWriter.GetToken<bool>($"{authorizerPath}.EnableFunctionDefaultPermissions"));
        }

        [Theory]
        [InlineData(CloudFormationTemplateFormat.Json)]
        [InlineData(CloudFormationTemplateFormat.Yaml)]
        public void RestApiAuthorizerProcessing(CloudFormationTemplateFormat templateFormat)
        {
            // ARRANGE
            var mockFileManager = GetMockFileManager(string.Empty);
            var lambdaFunctionModel = GetLambdaFunctionModel("MyAssembly::MyNamespace.MyType::Authorize",
                "AuthorizerFunction", 30, 512, null, null);
            var authorizer = new AuthorizerModel
            {
                Name = "MyRestAuthorizer",
                LambdaResourceName = "AuthorizerFunction",
                AuthorizerType = AuthorizerType.RestApi,
                IdentityHeader = "Authorization",
                ResultTtlInSeconds = 0,
                RestApiAuthorizerType = RestApiAuthorizerType.Token
            };
            var cloudFormationWriter = GetCloudFormationWriter(mockFileManager, _directoryManager, templateFormat, _diagnosticReporter);
            var report = GetAnnotationReport(
                new List<ILambdaFunctionSerializable> { lambdaFunctionModel },
                new List<AuthorizerModel> { authorizer });
            ITemplateWriter templateWriter = templateFormat == CloudFormationTemplateFormat.Json ? new JsonWriter() : new YamlWriter();

            // ACT
            cloudFormationWriter.ApplyReport(report);

            // ASSERT
            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));

            // Verify AnnotationsRestApi resource was created
            const string restApiPath = "Resources.AnnotationsRestApi";
            Assert.True(templateWriter.Exists(restApiPath));
            Assert.Equal("AWS::Serverless::Api", templateWriter.GetToken<string>($"{restApiPath}.Type"));
            Assert.Equal("Amazon.Lambda.Annotations", templateWriter.GetToken<string>($"{restApiPath}.Metadata.Tool"));
            Assert.Equal("Prod", templateWriter.GetToken<string>($"{restApiPath}.Properties.StageName"));

            // Verify authorizer configuration
            const string authorizerPath = "Resources.AnnotationsRestApi.Properties.Auth.Authorizers.MyRestAuthorizer";
            Assert.True(templateWriter.Exists(authorizerPath));
            Assert.Equal(new List<string> { "AuthorizerFunction", "Arn" }, templateWriter.GetToken<List<string>>($"{authorizerPath}.FunctionArn.Fn::GetAtt"));
            Assert.Equal("Authorization", templateWriter.GetToken<string>($"{authorizerPath}.Identity.Header"));
            Assert.Equal("TOKEN", templateWriter.GetToken<string>($"{authorizerPath}.FunctionPayloadType"));
        }

        [Theory]
        [InlineData(CloudFormationTemplateFormat.Json)]
        [InlineData(CloudFormationTemplateFormat.Yaml)]
        public void RestApiRequestAuthorizerProcessing(CloudFormationTemplateFormat templateFormat)
        {
            // ARRANGE
            var mockFileManager = GetMockFileManager(string.Empty);
            var lambdaFunctionModel = GetLambdaFunctionModel("MyAssembly::MyNamespace.MyType::Authorize",
                "AuthorizerFunction", 30, 512, null, null);
            var authorizer = new AuthorizerModel
            {
                Name = "MyRequestAuthorizer",
                LambdaResourceName = "AuthorizerFunction",
                AuthorizerType = AuthorizerType.RestApi,
                IdentityHeader = "X-Custom-Header",
                ResultTtlInSeconds = 0,
                RestApiAuthorizerType = RestApiAuthorizerType.Request
            };
            var cloudFormationWriter = GetCloudFormationWriter(mockFileManager, _directoryManager, templateFormat, _diagnosticReporter);
            var report = GetAnnotationReport(
                new List<ILambdaFunctionSerializable> { lambdaFunctionModel },
                new List<AuthorizerModel> { authorizer });
            ITemplateWriter templateWriter = templateFormat == CloudFormationTemplateFormat.Json ? new JsonWriter() : new YamlWriter();

            // ACT
            cloudFormationWriter.ApplyReport(report);

            // ASSERT
            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));

            const string authorizerPath = "Resources.AnnotationsRestApi.Properties.Auth.Authorizers.MyRequestAuthorizer";
            Assert.True(templateWriter.Exists(authorizerPath));
            Assert.Equal("REQUEST", templateWriter.GetToken<string>($"{authorizerPath}.FunctionPayloadType"));
            Assert.Equal("X-Custom-Header", templateWriter.GetToken<string>($"{authorizerPath}.Identity.Header"));
        }

        [Theory]
        [InlineData(CloudFormationTemplateFormat.Json)]
        [InlineData(CloudFormationTemplateFormat.Yaml)]
        public void HttpApiWithAuthorizerReference(CloudFormationTemplateFormat templateFormat)
        {
            // ARRANGE
            var mockFileManager = GetMockFileManager(string.Empty);

            // Create the authorizer function
            var authorizerFunctionModel = GetLambdaFunctionModel("MyAssembly::MyNamespace.MyType::Authorize",
                "AuthorizerFunction", 30, 512, null, null);

            // Create the protected function with HttpApi attribute referencing the authorizer
            var protectedFunctionModel = GetLambdaFunctionModel("MyAssembly::MyNamespace.MyType::Protected",
                "ProtectedFunction", 30, 512, null, null);
            var httpApiAttribute = new AttributeModel<HttpApiAttribute>()
            {
                Data = new HttpApiAttribute(LambdaHttpMethod.Get, "/api/protected")
                {
                    Authorizer = "MyHttpAuthorizer"
                }
            };
            protectedFunctionModel.Attributes = new List<AttributeModel> { httpApiAttribute };
            protectedFunctionModel.Authorizer = "MyHttpAuthorizer";

            var authorizer = new AuthorizerModel
            {
                Name = "MyHttpAuthorizer",
                LambdaResourceName = "AuthorizerFunction",
                AuthorizerType = AuthorizerType.HttpApi,
                IdentityHeader = "Authorization",
                ResultTtlInSeconds = 0,
                EnableSimpleResponses = true,
                PayloadFormatVersion = "2.0"
            };

            var cloudFormationWriter = GetCloudFormationWriter(mockFileManager, _directoryManager, templateFormat, _diagnosticReporter);
            var report = GetAnnotationReport(
                new List<ILambdaFunctionSerializable> { authorizerFunctionModel, protectedFunctionModel },
                new List<AuthorizerModel> { authorizer });
            ITemplateWriter templateWriter = templateFormat == CloudFormationTemplateFormat.Json ? new JsonWriter() : new YamlWriter();

            // ACT
            cloudFormationWriter.ApplyReport(report);

            // ASSERT
            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));

            // Verify the protected function's event has Auth configuration
            const string eventPath = "Resources.ProtectedFunction.Properties.Events.RootGet";
            Assert.True(templateWriter.Exists(eventPath));
            Assert.Equal("HttpApi", templateWriter.GetToken<string>($"{eventPath}.Type"));
            Assert.Equal("/api/protected", templateWriter.GetToken<string>($"{eventPath}.Properties.Path"));
            Assert.Equal("GET", templateWriter.GetToken<string>($"{eventPath}.Properties.Method"));
            Assert.Equal("MyHttpAuthorizer", templateWriter.GetToken<string>($"{eventPath}.Properties.Auth.Authorizer"));
            Assert.Equal("AnnotationsHttpApi", templateWriter.GetToken<string>($"{eventPath}.Properties.ApiId.Ref"));
        }

        [Theory]
        [InlineData(CloudFormationTemplateFormat.Json)]
        [InlineData(CloudFormationTemplateFormat.Yaml)]
        public void RestApiWithAuthorizerReference(CloudFormationTemplateFormat templateFormat)
        {
            // ARRANGE
            var mockFileManager = GetMockFileManager(string.Empty);

            // Create the authorizer function
            var authorizerFunctionModel = GetLambdaFunctionModel("MyAssembly::MyNamespace.MyType::Authorize",
                "AuthorizerFunction", 30, 512, null, null);

            // Create the protected function with RestApi attribute referencing the authorizer
            var protectedFunctionModel = GetLambdaFunctionModel("MyAssembly::MyNamespace.MyType::Protected",
                "ProtectedFunction", 30, 512, null, null);
            var restApiAttribute = new AttributeModel<RestApiAttribute>()
            {
                Data = new RestApiAttribute(LambdaHttpMethod.Get, "/api/protected")
                {
                    Authorizer = "MyRestAuthorizer"
                }
            };
            protectedFunctionModel.Attributes = new List<AttributeModel> { restApiAttribute };
            protectedFunctionModel.Authorizer = "MyRestAuthorizer";

            var authorizer = new AuthorizerModel
            {
                Name = "MyRestAuthorizer",
                LambdaResourceName = "AuthorizerFunction",
                AuthorizerType = AuthorizerType.RestApi,
                IdentityHeader = "Authorization",
                ResultTtlInSeconds = 0,
                RestApiAuthorizerType = RestApiAuthorizerType.Token
            };

            var cloudFormationWriter = GetCloudFormationWriter(mockFileManager, _directoryManager, templateFormat, _diagnosticReporter);
            var report = GetAnnotationReport(
                new List<ILambdaFunctionSerializable> { authorizerFunctionModel, protectedFunctionModel },
                new List<AuthorizerModel> { authorizer });
            ITemplateWriter templateWriter = templateFormat == CloudFormationTemplateFormat.Json ? new JsonWriter() : new YamlWriter();

            // ACT
            cloudFormationWriter.ApplyReport(report);

            // ASSERT
            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));

            // Verify the protected function's event has Auth configuration
            const string eventPath = "Resources.ProtectedFunction.Properties.Events.RootGet";
            Assert.True(templateWriter.Exists(eventPath));
            Assert.Equal("Api", templateWriter.GetToken<string>($"{eventPath}.Type"));
            Assert.Equal("/api/protected", templateWriter.GetToken<string>($"{eventPath}.Properties.Path"));
            Assert.Equal("GET", templateWriter.GetToken<string>($"{eventPath}.Properties.Method"));
            Assert.Equal("MyRestAuthorizer", templateWriter.GetToken<string>($"{eventPath}.Properties.Auth.Authorizer"));
            Assert.Equal("AnnotationsRestApi", templateWriter.GetToken<string>($"{eventPath}.Properties.RestApiId.Ref"));
        }

        [Theory]
        [InlineData(CloudFormationTemplateFormat.Json)]
        [InlineData(CloudFormationTemplateFormat.Yaml)]
        public void HttpApiAuthorizerWithCustomTtl(CloudFormationTemplateFormat templateFormat)
        {
            // ARRANGE
            var mockFileManager = GetMockFileManager(string.Empty);
            var lambdaFunctionModel = GetLambdaFunctionModel("MyAssembly::MyNamespace.MyType::Authorize",
                "AuthorizerFunction", 30, 512, null, null);
            var authorizer = new AuthorizerModel
            {
                Name = "CachedAuthorizer",
                LambdaResourceName = "AuthorizerFunction",
                AuthorizerType = AuthorizerType.HttpApi,
                IdentityHeader = "Authorization",
                ResultTtlInSeconds = 300,
                EnableSimpleResponses = true,
                PayloadFormatVersion = "2.0"
            };
            var cloudFormationWriter = GetCloudFormationWriter(mockFileManager, _directoryManager, templateFormat, _diagnosticReporter);
            var report = GetAnnotationReport(
                new List<ILambdaFunctionSerializable> { lambdaFunctionModel },
                new List<AuthorizerModel> { authorizer });
            ITemplateWriter templateWriter = templateFormat == CloudFormationTemplateFormat.Json ? new JsonWriter() : new YamlWriter();

            // ACT
            cloudFormationWriter.ApplyReport(report);

            // ASSERT
            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));

            const string authorizerPath = "Resources.AnnotationsHttpApi.Properties.Auth.Authorizers.CachedAuthorizer";
            Assert.True(templateWriter.Exists(authorizerPath));

            // When TTL > 0, FunctionInvokeRole is set (to null, signaling SAM to auto-generate the role for caching)
            // Verify the authorizer was created with expected properties
            Assert.Equal(new List<string> { "AuthorizerFunction", "Arn" }, templateWriter.GetToken<List<string>>($"{authorizerPath}.FunctionArn.Fn::GetAtt"));
            Assert.Equal("2.0", templateWriter.GetToken<string>($"{authorizerPath}.AuthorizerPayloadFormatVersion"));
            Assert.Equal(300, templateWriter.GetToken<int>($"{authorizerPath}.AuthorizerResultTtlInSeconds"));
        }

        [Theory]
        [InlineData(CloudFormationTemplateFormat.Json)]
        [InlineData(CloudFormationTemplateFormat.Yaml)]
        public void HttpApiAuthorizerWithNoTtl_DoesNotSetFunctionInvokeRole(CloudFormationTemplateFormat templateFormat)
        {
            // ARRANGE
            var mockFileManager = GetMockFileManager(string.Empty);
            var lambdaFunctionModel = GetLambdaFunctionModel("MyAssembly::MyNamespace.MyType::Authorize",
                "AuthorizerFunction", 30, 512, null, null);
            var authorizer = new AuthorizerModel
            {
                Name = "NoCacheAuthorizer",
                LambdaResourceName = "AuthorizerFunction",
                AuthorizerType = AuthorizerType.HttpApi,
                IdentityHeader = "Authorization",
                ResultTtlInSeconds = 0,
                EnableSimpleResponses = true,
                PayloadFormatVersion = "2.0"
            };
            var cloudFormationWriter = GetCloudFormationWriter(mockFileManager, _directoryManager, templateFormat, _diagnosticReporter);
            var report = GetAnnotationReport(
                new List<ILambdaFunctionSerializable> { lambdaFunctionModel },
                new List<AuthorizerModel> { authorizer });
            ITemplateWriter templateWriter = templateFormat == CloudFormationTemplateFormat.Json ? new JsonWriter() : new YamlWriter();

            // ACT
            cloudFormationWriter.ApplyReport(report);

            // ASSERT
            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));

            const string authorizerPath = "Resources.AnnotationsHttpApi.Properties.Auth.Authorizers.NoCacheAuthorizer";
            Assert.True(templateWriter.Exists(authorizerPath));

            // When TTL = 0, FunctionInvokeRole should NOT be set
            Assert.False(templateWriter.Exists($"{authorizerPath}.FunctionInvokeRole"));
        }

        [Theory]
        [InlineData(CloudFormationTemplateFormat.Json)]
        [InlineData(CloudFormationTemplateFormat.Yaml)]
        public void RemoveOrphanedHttpApiAuthorizer(CloudFormationTemplateFormat templateFormat)
        {
            // ARRANGE - Start with an authorizer
            var mockFileManager = GetMockFileManager(string.Empty);
            var lambdaFunctionModel = GetLambdaFunctionModel("MyAssembly::MyNamespace.MyType::Authorize",
                "AuthorizerFunction", 30, 512, null, null);
            var authorizer = new AuthorizerModel
            {
                Name = "OldAuthorizer",
                LambdaResourceName = "AuthorizerFunction",
                AuthorizerType = AuthorizerType.HttpApi,
                IdentityHeader = "Authorization",
                ResultTtlInSeconds = 0,
                EnableSimpleResponses = true,
                PayloadFormatVersion = "2.0"
            };
            var cloudFormationWriter = GetCloudFormationWriter(mockFileManager, _directoryManager, templateFormat, _diagnosticReporter);
            var report = GetAnnotationReport(
                new List<ILambdaFunctionSerializable> { lambdaFunctionModel },
                new List<AuthorizerModel> { authorizer });
            ITemplateWriter templateWriter = templateFormat == CloudFormationTemplateFormat.Json ? new JsonWriter() : new YamlWriter();

            // ACT - First pass: create the authorizer
            cloudFormationWriter.ApplyReport(report);

            // ASSERT - Authorizer exists
            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));
            Assert.True(templateWriter.Exists("Resources.AnnotationsHttpApi.Properties.Auth.Authorizers.OldAuthorizer"));

            // ACT - Second pass: remove the authorizer (empty authorizer list)
            var reportWithoutAuthorizer = GetAnnotationReport(
                new List<ILambdaFunctionSerializable> { lambdaFunctionModel },
                new List<AuthorizerModel>());
            cloudFormationWriter.ApplyReport(reportWithoutAuthorizer);

            // ASSERT - Authorizer is removed
            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));
            Assert.False(templateWriter.Exists("Resources.AnnotationsHttpApi.Properties.Auth.Authorizers.OldAuthorizer"));
        }

        [Theory]
        [InlineData(CloudFormationTemplateFormat.Json)]
        [InlineData(CloudFormationTemplateFormat.Yaml)]
        public void RemoveOrphanedRestApiAuthorizer(CloudFormationTemplateFormat templateFormat)
        {
            // ARRANGE - Start with an authorizer
            var mockFileManager = GetMockFileManager(string.Empty);
            var lambdaFunctionModel = GetLambdaFunctionModel("MyAssembly::MyNamespace.MyType::Authorize",
                "AuthorizerFunction", 30, 512, null, null);
            var authorizer = new AuthorizerModel
            {
                Name = "OldRestAuthorizer",
                LambdaResourceName = "AuthorizerFunction",
                AuthorizerType = AuthorizerType.RestApi,
                IdentityHeader = "Authorization",
                ResultTtlInSeconds = 0,
                RestApiAuthorizerType = RestApiAuthorizerType.Token
            };
            var cloudFormationWriter = GetCloudFormationWriter(mockFileManager, _directoryManager, templateFormat, _diagnosticReporter);
            var report = GetAnnotationReport(
                new List<ILambdaFunctionSerializable> { lambdaFunctionModel },
                new List<AuthorizerModel> { authorizer });
            ITemplateWriter templateWriter = templateFormat == CloudFormationTemplateFormat.Json ? new JsonWriter() : new YamlWriter();

            // ACT - First pass: create the authorizer
            cloudFormationWriter.ApplyReport(report);

            // ASSERT - Authorizer exists
            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));
            Assert.True(templateWriter.Exists("Resources.AnnotationsRestApi.Properties.Auth.Authorizers.OldRestAuthorizer"));

            // ACT - Second pass: remove the authorizer
            var reportWithoutAuthorizer = GetAnnotationReport(
                new List<ILambdaFunctionSerializable> { lambdaFunctionModel },
                new List<AuthorizerModel>());
            cloudFormationWriter.ApplyReport(reportWithoutAuthorizer);

            // ASSERT - Authorizer is removed
            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));
            Assert.False(templateWriter.Exists("Resources.AnnotationsRestApi.Properties.Auth.Authorizers.OldRestAuthorizer"));
        }

        [Theory]
        [InlineData(CloudFormationTemplateFormat.Json)]
        [InlineData(CloudFormationTemplateFormat.Yaml)]
        public void CombinedAuthorizerAndProtectedFunction(CloudFormationTemplateFormat templateFormat)
        {
            // ARRANGE
            var mockFileManager = GetMockFileManager(string.Empty);

            // Create the authorizer function
            var authorizerFunctionModel = GetLambdaFunctionModel("MyAssembly::MyNamespace.Auth::Authorize",
                "MyAuthorizerFunction", 30, 512, null, null);

            // Create a protected function
            var protectedFunctionModel = GetLambdaFunctionModel("MyAssembly::MyNamespace.Api::GetData",
                "GetDataFunction", 30, 512, null, null);
            var httpApiAttribute = new AttributeModel<HttpApiAttribute>()
            {
                Data = new HttpApiAttribute(LambdaHttpMethod.Get, "/data")
                {
                    Authorizer = "LambdaAuthorizer"
                }
            };
            protectedFunctionModel.Attributes = new List<AttributeModel> { httpApiAttribute };
            protectedFunctionModel.Authorizer = "LambdaAuthorizer";

            // Create an unprotected function
            var publicFunctionModel = GetLambdaFunctionModel("MyAssembly::MyNamespace.Api::Health",
                "HealthFunction", 30, 512, null, null);
            var publicHttpApiAttribute = new AttributeModel<HttpApiAttribute>()
            {
                Data = new HttpApiAttribute(LambdaHttpMethod.Get, "/health")
            };
            publicFunctionModel.Attributes = new List<AttributeModel> { publicHttpApiAttribute };

            var authorizer = new AuthorizerModel
            {
                Name = "LambdaAuthorizer",
                LambdaResourceName = "MyAuthorizerFunction",
                AuthorizerType = AuthorizerType.HttpApi,
                IdentityHeader = "Authorization",
                ResultTtlInSeconds = 0,
                EnableSimpleResponses = true,
                PayloadFormatVersion = "2.0"
            };

            var cloudFormationWriter = GetCloudFormationWriter(mockFileManager, _directoryManager, templateFormat, _diagnosticReporter);
            var report = GetAnnotationReport(
                new List<ILambdaFunctionSerializable> { authorizerFunctionModel, protectedFunctionModel, publicFunctionModel },
                new List<AuthorizerModel> { authorizer });
            ITemplateWriter templateWriter = templateFormat == CloudFormationTemplateFormat.Json ? new JsonWriter() : new YamlWriter();

            // ACT
            cloudFormationWriter.ApplyReport(report);

            // ASSERT
            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));

            // Verify all three Lambda functions exist
            Assert.True(templateWriter.Exists("Resources.MyAuthorizerFunction"));
            Assert.True(templateWriter.Exists("Resources.GetDataFunction"));
            Assert.True(templateWriter.Exists("Resources.HealthFunction"));

            // Verify AnnotationsHttpApi resource with authorizer
            Assert.True(templateWriter.Exists("Resources.AnnotationsHttpApi"));
            Assert.Equal("AWS::Serverless::HttpApi", templateWriter.GetToken<string>("Resources.AnnotationsHttpApi.Type"));
            Assert.True(templateWriter.Exists("Resources.AnnotationsHttpApi.Properties.Auth.Authorizers.LambdaAuthorizer"));
            Assert.Equal(new List<string> { "MyAuthorizerFunction", "Arn" },
                templateWriter.GetToken<List<string>>("Resources.AnnotationsHttpApi.Properties.Auth.Authorizers.LambdaAuthorizer.FunctionArn.Fn::GetAtt"));

            // Verify protected function references authorizer
            const string protectedEventPath = "Resources.GetDataFunction.Properties.Events.RootGet";
            Assert.Equal("LambdaAuthorizer", templateWriter.GetToken<string>($"{protectedEventPath}.Properties.Auth.Authorizer"));
            Assert.Equal("AnnotationsHttpApi", templateWriter.GetToken<string>($"{protectedEventPath}.Properties.ApiId.Ref"));

            // Verify public function does NOT reference authorizer
            const string publicEventPath = "Resources.HealthFunction.Properties.Events.RootGet";
            Assert.True(templateWriter.Exists(publicEventPath));
            Assert.False(templateWriter.Exists($"{publicEventPath}.Properties.Auth"));
            Assert.False(templateWriter.Exists($"{publicEventPath}.Properties.ApiId"));
        }

        [Theory]
        [InlineData(CloudFormationTemplateFormat.Json)]
        [InlineData(CloudFormationTemplateFormat.Yaml)]
        public void HttpApiAuthorizerWithCustomSettings(CloudFormationTemplateFormat templateFormat)
        {
            // ARRANGE - Test non-default authorizer settings (V1 payload, simple responses disabled, custom header)
            var mockFileManager = GetMockFileManager(string.Empty);
            var lambdaFunctionModel = GetLambdaFunctionModel("MyAssembly::MyNamespace.MyType::Authorize",
                "AuthorizerFunction", 30, 512, null, null);
            var authorizer = new AuthorizerModel
            {
                Name = "CustomAuthorizer",
                LambdaResourceName = "AuthorizerFunction",
                AuthorizerType = AuthorizerType.HttpApi,
                IdentityHeader = "X-Api-Key",
                ResultTtlInSeconds = 0,
                EnableSimpleResponses = false,
                PayloadFormatVersion = "1.0"
            };
            var cloudFormationWriter = GetCloudFormationWriter(mockFileManager, _directoryManager, templateFormat, _diagnosticReporter);
            var report = GetAnnotationReport(
                new List<ILambdaFunctionSerializable> { lambdaFunctionModel },
                new List<AuthorizerModel> { authorizer });
            ITemplateWriter templateWriter = templateFormat == CloudFormationTemplateFormat.Json ? new JsonWriter() : new YamlWriter();

            // ACT
            cloudFormationWriter.ApplyReport(report);

            // ASSERT
            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));

            const string authorizerPath = "Resources.AnnotationsHttpApi.Properties.Auth.Authorizers.CustomAuthorizer";
            Assert.True(templateWriter.Exists(authorizerPath));
            Assert.Equal("1.0", templateWriter.GetToken<string>($"{authorizerPath}.AuthorizerPayloadFormatVersion"));
            Assert.False(templateWriter.GetToken<bool>($"{authorizerPath}.EnableSimpleResponses"));
            Assert.Equal(new List<string> { "X-Api-Key" },
                templateWriter.GetToken<List<string>>($"{authorizerPath}.Identity.Headers"));
            Assert.True(templateWriter.GetToken<bool>($"{authorizerPath}.EnableFunctionDefaultPermissions"));
        }

        #endregion
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
        public string Authorizer { get; set; }
    }
}
