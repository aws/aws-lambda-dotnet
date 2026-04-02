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
            // When MultiValueHeaders is false (default), no TargetGroupAttributes should be present
            Assert.False(templateWriter.Exists("Resources.HelloWorldALBTargetGroup.Properties.TargetGroupAttributes"));

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
        public void ALBApiAttribute_WithMultiValueHeaders_SetsTargetGroupAttributes(CloudFormationTemplateFormat templateFormat)
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
            // When MultiValueHeaders is true, TargetGroupAttributes should contain the lambda.multi_value_headers.enabled attribute
            Assert.True(templateWriter.Exists("Resources.MyFunctionALBTargetGroup.Properties.TargetGroupAttributes"));
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

        [Theory]
        [InlineData(CloudFormationTemplateFormat.Json)]
        [InlineData(CloudFormationTemplateFormat.Yaml)]
        public void ALBApiAttribute_TracksSyncedAlbResourcesInMetadata(CloudFormationTemplateFormat templateFormat)
        {
            // ARRANGE
            var mockFileManager = GetMockFileManager(string.Empty);
            var lambdaFunctionModel = GetLambdaFunctionModel("MyAssembly::MyNamespace.MyType::Handler",
                "HelloWorld", 30, 256, null, null);
            lambdaFunctionModel.PackageType = LambdaPackageType.Zip;

            var albAttribute = new ALBApiAttribute("@MyListener", "/hello", 1);
            lambdaFunctionModel.Attributes.Add(new AttributeModel<ALBApiAttribute> { Data = albAttribute });

            var cloudFormationWriter = GetCloudFormationWriter(mockFileManager, _directoryManager, templateFormat, _diagnosticReporter);
            var report = GetAnnotationReport(new List<ILambdaFunctionSerializable> { lambdaFunctionModel });
            ITemplateWriter templateWriter = templateFormat == CloudFormationTemplateFormat.Json ? new JsonWriter() : new YamlWriter();

            // ACT
            cloudFormationWriter.ApplyReport(report);

            // ASSERT
            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));

            // Verify SyncedAlbResources metadata is persisted on the Lambda function
            Assert.True(templateWriter.Exists("Resources.HelloWorld.Metadata.SyncedAlbResources"));
            var syncedAlbResources = templateWriter.GetToken<List<string>>("Resources.HelloWorld.Metadata.SyncedAlbResources");
            Assert.Contains("HelloWorldALBPermission", syncedAlbResources);
            Assert.Contains("HelloWorldALBTargetGroup", syncedAlbResources);
            Assert.Contains("HelloWorldALBListenerRule", syncedAlbResources);
        }

        [Theory]
        [InlineData(CloudFormationTemplateFormat.Json)]
        [InlineData(CloudFormationTemplateFormat.Yaml)]
        public void ALBApiAttribute_RemovesOrphanedResourcesWhenAttributeRemoved(CloudFormationTemplateFormat templateFormat)
        {
            // ARRANGE - First, create a function with ALB attribute
            var mockFileManager = GetMockFileManager(string.Empty);
            var lambdaFunctionModel = GetLambdaFunctionModel("MyAssembly::MyNamespace.MyType::Handler",
                "HelloWorld", 30, 256, null, null);
            lambdaFunctionModel.PackageType = LambdaPackageType.Zip;

            var albAttribute = new ALBApiAttribute("@MyListener", "/hello", 1);
            lambdaFunctionModel.Attributes.Add(new AttributeModel<ALBApiAttribute> { Data = albAttribute });

            var cloudFormationWriter = GetCloudFormationWriter(mockFileManager, _directoryManager, templateFormat, _diagnosticReporter);
            var report = GetAnnotationReport(new List<ILambdaFunctionSerializable> { lambdaFunctionModel });

            // First pass - creates ALB resources
            cloudFormationWriter.ApplyReport(report);

            // Verify resources exist after first pass
            ITemplateWriter templateWriter = templateFormat == CloudFormationTemplateFormat.Json ? new JsonWriter() : new YamlWriter();
            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));
            Assert.True(templateWriter.Exists("Resources.HelloWorldALBPermission"));
            Assert.True(templateWriter.Exists("Resources.HelloWorldALBTargetGroup"));
            Assert.True(templateWriter.Exists("Resources.HelloWorldALBListenerRule"));

            // ACT - Second pass: remove the ALB attribute (function still exists, but no ALB)
            var lambdaFunctionModelNoAlb = GetLambdaFunctionModel("MyAssembly::MyNamespace.MyType::Handler",
                "HelloWorld", 30, 256, null, null);
            lambdaFunctionModelNoAlb.PackageType = LambdaPackageType.Zip;
            // No ALB attribute added

            var cloudFormationWriter2 = GetCloudFormationWriter(mockFileManager, _directoryManager, templateFormat, _diagnosticReporter);
            var report2 = GetAnnotationReport(new List<ILambdaFunctionSerializable> { lambdaFunctionModelNoAlb });
            cloudFormationWriter2.ApplyReport(report2);

            // ASSERT - ALB resources should be removed
            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));
            Assert.False(templateWriter.Exists("Resources.HelloWorldALBPermission"));
            Assert.False(templateWriter.Exists("Resources.HelloWorldALBTargetGroup"));
            Assert.False(templateWriter.Exists("Resources.HelloWorldALBListenerRule"));

            // Lambda function should still exist
            Assert.True(templateWriter.Exists("Resources.HelloWorld"));

            // SyncedAlbResources metadata should be cleared
            Assert.False(templateWriter.Exists("Resources.HelloWorld.Metadata.SyncedAlbResources"));
        }

        [Theory]
        [InlineData(CloudFormationTemplateFormat.Json)]
        [InlineData(CloudFormationTemplateFormat.Yaml)]
        public void ALBApiAttribute_RemovesOldResourcesWhenResourceNameChanges(CloudFormationTemplateFormat templateFormat)
        {
            // ARRANGE - First, create a function with ALB attribute using custom ResourceName
            var mockFileManager = GetMockFileManager(string.Empty);
            var lambdaFunctionModel = GetLambdaFunctionModel("MyAssembly::MyNamespace.MyType::Handler",
                "HelloWorld", 30, 256, null, null);
            lambdaFunctionModel.PackageType = LambdaPackageType.Zip;

            var albAttribute = new ALBApiAttribute("@MyListener", "/hello", 1)
            {
                ResourceName = "OldALB"
            };
            lambdaFunctionModel.Attributes.Add(new AttributeModel<ALBApiAttribute> { Data = albAttribute });

            var cloudFormationWriter = GetCloudFormationWriter(mockFileManager, _directoryManager, templateFormat, _diagnosticReporter);
            var report = GetAnnotationReport(new List<ILambdaFunctionSerializable> { lambdaFunctionModel });

            // First pass - creates resources with "OldALB" prefix
            cloudFormationWriter.ApplyReport(report);

            ITemplateWriter templateWriter = templateFormat == CloudFormationTemplateFormat.Json ? new JsonWriter() : new YamlWriter();
            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));
            Assert.True(templateWriter.Exists("Resources.OldALBPermission"));
            Assert.True(templateWriter.Exists("Resources.OldALBTargetGroup"));
            Assert.True(templateWriter.Exists("Resources.OldALBListenerRule"));

            // ACT - Second pass: change ResourceName to "NewALB"
            var lambdaFunctionModelNew = GetLambdaFunctionModel("MyAssembly::MyNamespace.MyType::Handler",
                "HelloWorld", 30, 256, null, null);
            lambdaFunctionModelNew.PackageType = LambdaPackageType.Zip;

            var albAttributeNew = new ALBApiAttribute("@MyListener", "/hello", 1)
            {
                ResourceName = "NewALB"
            };
            lambdaFunctionModelNew.Attributes.Add(new AttributeModel<ALBApiAttribute> { Data = albAttributeNew });

            var cloudFormationWriter2 = GetCloudFormationWriter(mockFileManager, _directoryManager, templateFormat, _diagnosticReporter);
            var report2 = GetAnnotationReport(new List<ILambdaFunctionSerializable> { lambdaFunctionModelNew });
            cloudFormationWriter2.ApplyReport(report2);

            // ASSERT - Old resources should be removed
            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));
            Assert.False(templateWriter.Exists("Resources.OldALBPermission"));
            Assert.False(templateWriter.Exists("Resources.OldALBTargetGroup"));
            Assert.False(templateWriter.Exists("Resources.OldALBListenerRule"));

            // New resources should exist
            Assert.True(templateWriter.Exists("Resources.NewALBPermission"));
            Assert.True(templateWriter.Exists("Resources.NewALBTargetGroup"));
            Assert.True(templateWriter.Exists("Resources.NewALBListenerRule"));

            // SyncedAlbResources should reflect new names
            var syncedAlbResources = templateWriter.GetToken<List<string>>("Resources.HelloWorld.Metadata.SyncedAlbResources");
            Assert.Contains("NewALBPermission", syncedAlbResources);
            Assert.DoesNotContain("OldALBPermission", syncedAlbResources);
        }
    }
}
