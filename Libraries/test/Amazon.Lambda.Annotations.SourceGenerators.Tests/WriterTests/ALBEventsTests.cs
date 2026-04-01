using Amazon.Lambda.Annotations.ALB;
using Amazon.Lambda.Annotations.SourceGenerator;
using Amazon.Lambda.Annotations.SourceGenerator.Models;
using Amazon.Lambda.Annotations.SourceGenerator.Models.Attributes;
using Amazon.Lambda.Annotations.SourceGenerator.Writers;
using System.Collections.Generic;
using Xunit;

namespace Amazon.Lambda.Annotations.SourceGenerators.Tests.WriterTests
{
    public partial class CloudFormationWriterTests
    {
        [Theory]
        [InlineData(CloudFormationTemplateFormat.Json)]
        [InlineData(CloudFormationTemplateFormat.Yaml)]
        public void ALBApiAttribute_GeneratesTargetGroupListenerRuleAndPermission(CloudFormationTemplateFormat templateFormat)
        {
            // ARRANGE
            var mockFileManager = GetMockFileManager(string.Empty);
            var lambdaFunctionModel = GetLambdaFunctionModel("MyAssembly::MyNamespace.MyType::Handler",
                "HelloWorld", 30, 256, null, null);
            lambdaFunctionModel.PackageType = LambdaPackageType.Zip;

            var albAttribute = new ALBApiAttribute(
                "arn:aws:elasticloadbalancing:us-east-1:123456789012:listener/app/my-alb/abc/def",
                "/api/hello",
                1);

            lambdaFunctionModel.Attributes.Add(new AttributeModel<ALBApiAttribute> { Data = albAttribute });
            var cloudFormationWriter = GetCloudFormationWriter(mockFileManager, _directoryManager, templateFormat, _diagnosticReporter);
            var report = GetAnnotationReport(new List<ILambdaFunctionSerializable> { lambdaFunctionModel });
            ITemplateWriter templateWriter = templateFormat == CloudFormationTemplateFormat.Json ? new JsonWriter() : new YamlWriter();

            // ACT
            cloudFormationWriter.ApplyReport(report);

            // ASSERT
            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));

            // Verify Lambda function exists
            Assert.True(templateWriter.Exists("Resources.HelloWorld"));
            Assert.Equal("AWS::Serverless::Function", templateWriter.GetToken<string>("Resources.HelloWorld.Type"));

            // Verify Permission resource
            Assert.True(templateWriter.Exists("Resources.HelloWorldALBPermission"));
            Assert.Equal("AWS::Lambda::Permission", templateWriter.GetToken<string>("Resources.HelloWorldALBPermission.Type"));
            Assert.Equal("Amazon.Lambda.Annotations", templateWriter.GetToken<string>("Resources.HelloWorldALBPermission.Metadata.Tool"));
            Assert.Equal("lambda:InvokeFunction", templateWriter.GetToken<string>("Resources.HelloWorldALBPermission.Properties.Action"));
            Assert.Equal("elasticloadbalancing.amazonaws.com", templateWriter.GetToken<string>("Resources.HelloWorldALBPermission.Properties.Principal"));
            Assert.Equal(new List<string> { "HelloWorld", "Arn" },
                templateWriter.GetToken<List<string>>("Resources.HelloWorldALBPermission.Properties.FunctionName.Fn::GetAtt"));

            // Verify TargetGroup resource
            Assert.True(templateWriter.Exists("Resources.HelloWorldALBTargetGroup"));
            Assert.Equal("AWS::ElasticLoadBalancingV2::TargetGroup", templateWriter.GetToken<string>("Resources.HelloWorldALBTargetGroup.Type"));
            Assert.Equal("Amazon.Lambda.Annotations", templateWriter.GetToken<string>("Resources.HelloWorldALBTargetGroup.Metadata.Tool"));
            Assert.Equal("HelloWorldALBPermission", templateWriter.GetToken<string>("Resources.HelloWorldALBTargetGroup.DependsOn"));
            Assert.Equal("lambda", templateWriter.GetToken<string>("Resources.HelloWorldALBTargetGroup.Properties.TargetType"));
            Assert.False(templateWriter.GetToken<bool>("Resources.HelloWorldALBTargetGroup.Properties.MultiValueHeadersEnabled"));

            // Verify ListenerRule resource
            Assert.True(templateWriter.Exists("Resources.HelloWorldALBListenerRule"));
            Assert.Equal("AWS::ElasticLoadBalancingV2::ListenerRule", templateWriter.GetToken<string>("Resources.HelloWorldALBListenerRule.Type"));
            Assert.Equal("Amazon.Lambda.Annotations", templateWriter.GetToken<string>("Resources.HelloWorldALBListenerRule.Metadata.Tool"));
            Assert.Equal("arn:aws:elasticloadbalancing:us-east-1:123456789012:listener/app/my-alb/abc/def",
                templateWriter.GetToken<string>("Resources.HelloWorldALBListenerRule.Properties.ListenerArn"));
            Assert.Equal(1, templateWriter.GetToken<int>("Resources.HelloWorldALBListenerRule.Properties.Priority"));
        }

        [Theory]
        [InlineData(CloudFormationTemplateFormat.Json)]
        [InlineData(CloudFormationTemplateFormat.Yaml)]
        public void ALBApiAttribute_WithTemplateReference_UsesRef(CloudFormationTemplateFormat templateFormat)
        {
            // ARRANGE
            var mockFileManager = GetMockFileManager(string.Empty);
            var lambdaFunctionModel = GetLambdaFunctionModel("MyAssembly::MyNamespace.MyType::Handler",
                "MyFunction", 30, 256, null, null);
            lambdaFunctionModel.PackageType = LambdaPackageType.Zip;

            var albAttribute = new ALBApiAttribute("@MyALBListener", "/api/*", 5);
            lambdaFunctionModel.Attributes.Add(new AttributeModel<ALBApiAttribute> { Data = albAttribute });

            var cloudFormationWriter = GetCloudFormationWriter(mockFileManager, _directoryManager, templateFormat, _diagnosticReporter);
            var report = GetAnnotationReport(new List<ILambdaFunctionSerializable> { lambdaFunctionModel });
            ITemplateWriter templateWriter = templateFormat == CloudFormationTemplateFormat.Json ? new JsonWriter() : new YamlWriter();

            // ACT
            cloudFormationWriter.ApplyReport(report);

            // ASSERT
            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));

            // Verify ListenerArn uses Ref
            Assert.Equal("MyALBListener",
                templateWriter.GetToken<string>("Resources.MyFunctionALBListenerRule.Properties.ListenerArn.Ref"));
        }

        [Theory]
        [InlineData(CloudFormationTemplateFormat.Json)]
        [InlineData(CloudFormationTemplateFormat.Yaml)]
        public void ALBApiAttribute_WithMultiValueHeaders_SetsEnabled(CloudFormationTemplateFormat templateFormat)
        {
            // ARRANGE
            var mockFileManager = GetMockFileManager(string.Empty);
            var lambdaFunctionModel = GetLambdaFunctionModel("MyAssembly::MyNamespace.MyType::Handler",
                "MyFunction", 30, 256, null, null);
            lambdaFunctionModel.PackageType = LambdaPackageType.Zip;

            var albAttribute = new ALBApiAttribute("@MyListener", "/api/*", 1)
            {
                MultiValueHeaders = true
            };
            lambdaFunctionModel.Attributes.Add(new AttributeModel<ALBApiAttribute> { Data = albAttribute });

            var cloudFormationWriter = GetCloudFormationWriter(mockFileManager, _directoryManager, templateFormat, _diagnosticReporter);
            var report = GetAnnotationReport(new List<ILambdaFunctionSerializable> { lambdaFunctionModel });
            ITemplateWriter templateWriter = templateFormat == CloudFormationTemplateFormat.Json ? new JsonWriter() : new YamlWriter();

            // ACT
            cloudFormationWriter.ApplyReport(report);

            // ASSERT
            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));
            Assert.True(templateWriter.GetToken<bool>("Resources.MyFunctionALBTargetGroup.Properties.MultiValueHeadersEnabled"));
        }

        [Theory]
        [InlineData(CloudFormationTemplateFormat.Json)]
        [InlineData(CloudFormationTemplateFormat.Yaml)]
        public void ALBApiAttribute_WithCustomResourceName_UsesCustomPrefix(CloudFormationTemplateFormat templateFormat)
        {
            // ARRANGE
            var mockFileManager = GetMockFileManager(string.Empty);
            var lambdaFunctionModel = GetLambdaFunctionModel("MyAssembly::MyNamespace.MyType::Handler",
                "MyFunction", 30, 256, null, null);
            lambdaFunctionModel.PackageType = LambdaPackageType.Zip;

            var albAttribute = new ALBApiAttribute("@MyListener", "/api/*", 1)
            {
                ResourceName = "CustomALB"
            };
            lambdaFunctionModel.Attributes.Add(new AttributeModel<ALBApiAttribute> { Data = albAttribute });

            var cloudFormationWriter = GetCloudFormationWriter(mockFileManager, _directoryManager, templateFormat, _diagnosticReporter);
            var report = GetAnnotationReport(new List<ILambdaFunctionSerializable> { lambdaFunctionModel });
            ITemplateWriter templateWriter = templateFormat == CloudFormationTemplateFormat.Json ? new JsonWriter() : new YamlWriter();

            // ACT
            cloudFormationWriter.ApplyReport(report);

            // ASSERT
            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));

            // Verify custom resource names are used
            Assert.True(templateWriter.Exists("Resources.CustomALBPermission"));
            Assert.True(templateWriter.Exists("Resources.CustomALBTargetGroup"));
            Assert.True(templateWriter.Exists("Resources.CustomALBListenerRule"));

            // Verify default names are NOT used
            Assert.False(templateWriter.Exists("Resources.MyFunctionALBPermission"));
            Assert.False(templateWriter.Exists("Resources.MyFunctionALBTargetGroup"));
            Assert.False(templateWriter.Exists("Resources.MyFunctionALBListenerRule"));
        }

        [Theory]
        [InlineData(CloudFormationTemplateFormat.Json)]
        [InlineData(CloudFormationTemplateFormat.Yaml)]
        public void ALBApiAttribute_WithHostHeaderCondition_AddsCondition(CloudFormationTemplateFormat templateFormat)
        {
            // ARRANGE
            var mockFileManager = GetMockFileManager(string.Empty);
            var lambdaFunctionModel = GetLambdaFunctionModel("MyAssembly::MyNamespace.MyType::Handler",
                "MyFunction", 30, 256, null, null);
            lambdaFunctionModel.PackageType = LambdaPackageType.Zip;

            var albAttribute = new ALBApiAttribute("@MyListener", "/api/*", 10)
            {
                HostHeader = "api.example.com"
            };
            lambdaFunctionModel.Attributes.Add(new AttributeModel<ALBApiAttribute> { Data = albAttribute });

            var cloudFormationWriter = GetCloudFormationWriter(mockFileManager, _directoryManager, templateFormat, _diagnosticReporter);
            var report = GetAnnotationReport(new List<ILambdaFunctionSerializable> { lambdaFunctionModel });
            ITemplateWriter templateWriter = templateFormat == CloudFormationTemplateFormat.Json ? new JsonWriter() : new YamlWriter();

            // ACT
            cloudFormationWriter.ApplyReport(report);

            // ASSERT
            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));

            // Verify conditions exist (path-pattern + host-header = 2 conditions)
            Assert.True(templateWriter.Exists("Resources.MyFunctionALBListenerRule.Properties.Conditions"));
        }

        [Theory]
        [InlineData(CloudFormationTemplateFormat.Json)]
        [InlineData(CloudFormationTemplateFormat.Yaml)]
        public void ALBApiAttribute_WithHttpMethodCondition_AddsCondition(CloudFormationTemplateFormat templateFormat)
        {
            // ARRANGE
            var mockFileManager = GetMockFileManager(string.Empty);
            var lambdaFunctionModel = GetLambdaFunctionModel("MyAssembly::MyNamespace.MyType::Handler",
                "MyFunction", 30, 256, null, null);
            lambdaFunctionModel.PackageType = LambdaPackageType.Zip;

            var albAttribute = new ALBApiAttribute("@MyListener", "/api/*", 10)
            {
                HttpMethod = "POST"
            };
            lambdaFunctionModel.Attributes.Add(new AttributeModel<ALBApiAttribute> { Data = albAttribute });

            var cloudFormationWriter = GetCloudFormationWriter(mockFileManager, _directoryManager, templateFormat, _diagnosticReporter);
            var report = GetAnnotationReport(new List<ILambdaFunctionSerializable> { lambdaFunctionModel });
            ITemplateWriter templateWriter = templateFormat == CloudFormationTemplateFormat.Json ? new JsonWriter() : new YamlWriter();

            // ACT
            cloudFormationWriter.ApplyReport(report);

            // ASSERT
            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));

            // Verify conditions and priority
            Assert.True(templateWriter.Exists("Resources.MyFunctionALBListenerRule.Properties.Conditions"));
            Assert.Equal(10, templateWriter.GetToken<int>("Resources.MyFunctionALBListenerRule.Properties.Priority"));
        }
    }
}
