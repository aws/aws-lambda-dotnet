// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Linq;
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.SourceGenerator;
using Amazon.Lambda.Annotations.SourceGenerator.Models;
using Amazon.Lambda.Annotations.SourceGenerator.Models.Attributes;
using Amazon.Lambda.Annotations.SourceGenerator.Writers;
using Xunit;

namespace Amazon.Lambda.Annotations.SourceGenerators.Tests.WriterTests
{
    public partial class CloudFormationWriterTests
    {
        private static AttributeModel<DurableExecutionAttribute> DurableAttribute(int? retentionDays = null, int? executionTimeout = null)
        {
            var data = new DurableExecutionAttribute();
            if (retentionDays.HasValue) data.RetentionPeriodInDays = retentionDays.Value;
            if (executionTimeout.HasValue) data.ExecutionTimeout = executionTimeout.Value;
            return new AttributeModel<DurableExecutionAttribute> { Data = data };
        }

        [Theory]
        [InlineData(CloudFormationTemplateFormat.Json)]
        [InlineData(CloudFormationTemplateFormat.Yaml)]
        public void DurableExecution_WritesDurableConfig(CloudFormationTemplateFormat templateFormat)
        {
            var mockFileManager = GetMockFileManager(string.Empty);
            var lambdaFunctionModel = GetLambdaFunctionModel("MyAssembly::MyNamespace.MyType::Handler", "TestMethod", 30, 512, null, "AWSLambdaBasicExecutionRole");
            lambdaFunctionModel.Attributes = new List<AttributeModel> { DurableAttribute(retentionDays: 7, executionTimeout: 300) };
            var cloudFormationWriter = GetCloudFormationWriter(mockFileManager, _directoryManager, templateFormat, _diagnosticReporter);
            var report = GetAnnotationReport(new List<ILambdaFunctionSerializable> { lambdaFunctionModel });
            ITemplateWriter templateWriter = templateFormat == CloudFormationTemplateFormat.Json ? new JsonWriter() : new YamlWriter();

            cloudFormationWriter.ApplyReport(report);

            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));
            Assert.Equal(7, templateWriter.GetToken<int>("Resources.TestMethod.Properties.DurableConfig.RetentionPeriodInDays"));
            Assert.Equal(300, templateWriter.GetToken<int>("Resources.TestMethod.Properties.DurableConfig.ExecutionTimeout"));
            Assert.True(templateWriter.GetToken<bool>("Resources.TestMethod.Metadata.SyncedDurableConfig", false));
            // Durable is not an event source.
            Assert.False(templateWriter.Exists("Resources.TestMethod.Properties.Events"));
        }

        [Theory]
        [InlineData(CloudFormationTemplateFormat.Json)]
        [InlineData(CloudFormationTemplateFormat.Yaml)]
        public void DurableExecution_OmitsUnsetProperties(CloudFormationTemplateFormat templateFormat)
        {
            var mockFileManager = GetMockFileManager(string.Empty);
            var lambdaFunctionModel = GetLambdaFunctionModel("MyAssembly::MyNamespace.MyType::Handler", "TestMethod", 30, 512, null, "AWSLambdaBasicExecutionRole");
            lambdaFunctionModel.Attributes = new List<AttributeModel> { DurableAttribute(retentionDays: 7) };
            var cloudFormationWriter = GetCloudFormationWriter(mockFileManager, _directoryManager, templateFormat, _diagnosticReporter);
            var report = GetAnnotationReport(new List<ILambdaFunctionSerializable> { lambdaFunctionModel });
            ITemplateWriter templateWriter = templateFormat == CloudFormationTemplateFormat.Json ? new JsonWriter() : new YamlWriter();

            cloudFormationWriter.ApplyReport(report);

            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));
            Assert.Equal(7, templateWriter.GetToken<int>("Resources.TestMethod.Properties.DurableConfig.RetentionPeriodInDays"));
            Assert.False(templateWriter.Exists("Resources.TestMethod.Properties.DurableConfig.ExecutionTimeout"));
        }

        [Theory]
        [InlineData(CloudFormationTemplateFormat.Json)]
        [InlineData(CloudFormationTemplateFormat.Yaml)]
        public void DurableExecution_InjectsCheckpointPolicy_AsMixedArray(CloudFormationTemplateFormat templateFormat)
        {
            var mockFileManager = GetMockFileManager(string.Empty);
            var lambdaFunctionModel = GetLambdaFunctionModel("MyAssembly::MyNamespace.MyType::Handler", "TestMethod", 30, 512, null, "AWSLambdaBasicExecutionRole");
            lambdaFunctionModel.Attributes = new List<AttributeModel> { DurableAttribute(retentionDays: 7) };
            var cloudFormationWriter = GetCloudFormationWriter(mockFileManager, _directoryManager, templateFormat, _diagnosticReporter);
            var report = GetAnnotationReport(new List<ILambdaFunctionSerializable> { lambdaFunctionModel });
            ITemplateWriter templateWriter = templateFormat == CloudFormationTemplateFormat.Json ? new JsonWriter() : new YamlWriter();

            cloudFormationWriter.ApplyReport(report);

            // The mixed string/object Policies array must round-trip through both writers.
            var content = mockFileManager.ReadAllText(ServerlessTemplateFilePath);
            templateWriter.Parse(content);
            Assert.True(templateWriter.GetToken<bool>("Resources.TestMethod.Metadata.SyncedDurablePolicy", false));
            // The managed policy string is preserved alongside the injected statement object.
            Assert.Contains("AWSLambdaBasicExecutionRole", content);
            Assert.Contains("lambda:CheckpointDurableExecution", content);
            Assert.Contains("lambda:GetDurableExecutionState", content);
        }

        [Theory]
        [InlineData(CloudFormationTemplateFormat.Json)]
        [InlineData(CloudFormationTemplateFormat.Yaml)]
        public void DurableExecution_PolicyInjectionIsIdempotent(CloudFormationTemplateFormat templateFormat)
        {
            var mockFileManager = GetMockFileManager(string.Empty);
            var lambdaFunctionModel = GetLambdaFunctionModel("MyAssembly::MyNamespace.MyType::Handler", "TestMethod", 30, 512, null, "AWSLambdaBasicExecutionRole");
            lambdaFunctionModel.Attributes = new List<AttributeModel> { DurableAttribute(retentionDays: 7) };
            var report = GetAnnotationReport(new List<ILambdaFunctionSerializable> { lambdaFunctionModel });

            // First pass.
            GetCloudFormationWriter(mockFileManager, _directoryManager, templateFormat, _diagnosticReporter).ApplyReport(report);
            // Second pass over the same (now-populated) template.
            var report2 = GetAnnotationReport(new List<ILambdaFunctionSerializable> { lambdaFunctionModel });
            GetCloudFormationWriter(mockFileManager, _directoryManager, templateFormat, _diagnosticReporter).ApplyReport(report2);

            var content = mockFileManager.ReadAllText(ServerlessTemplateFilePath);
            // The checkpoint action should appear once, not duplicated across passes.
            var occurrences = content.Split(new[] { "lambda:CheckpointDurableExecution" }, System.StringSplitOptions.None).Length - 1;
            Assert.Equal(1, occurrences);
        }

        [Theory]
        [InlineData(CloudFormationTemplateFormat.Json)]
        [InlineData(CloudFormationTemplateFormat.Yaml)]
        public void DurableExecution_OrphanRemoval_StripsConfigAndPolicy(CloudFormationTemplateFormat templateFormat)
        {
            var mockFileManager = GetMockFileManager(string.Empty);

            // First pass: durable function.
            var durableModel = GetLambdaFunctionModel("MyAssembly::MyNamespace.MyType::Handler", "TestMethod", 30, 512, null, "AWSLambdaBasicExecutionRole");
            durableModel.Attributes = new List<AttributeModel> { DurableAttribute(retentionDays: 7) };
            GetCloudFormationWriter(mockFileManager, _directoryManager, templateFormat, _diagnosticReporter)
                .ApplyReport(GetAnnotationReport(new List<ILambdaFunctionSerializable> { durableModel }));

            // Second pass: same function, durable attribute removed.
            var plainModel = GetLambdaFunctionModel("MyAssembly::MyNamespace.MyType::Handler", "TestMethod", 30, 512, null, "AWSLambdaBasicExecutionRole");
            plainModel.Attributes = new List<AttributeModel>();
            GetCloudFormationWriter(mockFileManager, _directoryManager, templateFormat, _diagnosticReporter)
                .ApplyReport(GetAnnotationReport(new List<ILambdaFunctionSerializable> { plainModel }));

            var content = mockFileManager.ReadAllText(ServerlessTemplateFilePath);
            ITemplateWriter templateWriter = templateFormat == CloudFormationTemplateFormat.Json ? new JsonWriter() : new YamlWriter();
            templateWriter.Parse(content);

            Assert.False(templateWriter.Exists("Resources.TestMethod.Properties.DurableConfig"));
            Assert.False(templateWriter.Exists("Resources.TestMethod.Metadata.SyncedDurableConfig"));
            Assert.DoesNotContain("lambda:CheckpointDurableExecution", content);
            // The managed policy that pre-existed must remain.
            Assert.Contains("AWSLambdaBasicExecutionRole", content);
        }

        [Theory]
        [InlineData(CloudFormationTemplateFormat.Json)]
        [InlineData(CloudFormationTemplateFormat.Yaml)]
        public void DurableExecution_WithExplicitRole_DoesNotInjectPolicy(CloudFormationTemplateFormat templateFormat)
        {
            var mockFileManager = GetMockFileManager(string.Empty);
            // Explicit Role -> ProcessLambdaFunctionProperties removes Policies; durable must not re-add them.
            var lambdaFunctionModel = GetLambdaFunctionModel("MyAssembly::MyNamespace.MyType::Handler", "TestMethod", 30, 512, "arn:aws:iam::123456789012:role/MyRole", null);
            lambdaFunctionModel.Attributes = new List<AttributeModel> { DurableAttribute(retentionDays: 7) };
            var cloudFormationWriter = GetCloudFormationWriter(mockFileManager, _directoryManager, templateFormat, _diagnosticReporter);
            var report = GetAnnotationReport(new List<ILambdaFunctionSerializable> { lambdaFunctionModel });
            ITemplateWriter templateWriter = templateFormat == CloudFormationTemplateFormat.Json ? new JsonWriter() : new YamlWriter();

            cloudFormationWriter.ApplyReport(report);

            var content = mockFileManager.ReadAllText(ServerlessTemplateFilePath);
            templateWriter.Parse(content);
            // DurableConfig is still written, but no checkpoint policy and no policy marker.
            Assert.True(templateWriter.Exists("Resources.TestMethod.Properties.DurableConfig"));
            Assert.False(templateWriter.Exists("Resources.TestMethod.Properties.Policies"));
            Assert.False(templateWriter.GetToken<bool>("Resources.TestMethod.Metadata.SyncedDurablePolicy", false));
            Assert.DoesNotContain("lambda:CheckpointDurableExecution", content);
        }
    }
}
