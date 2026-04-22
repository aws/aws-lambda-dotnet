// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.Annotations.SourceGenerator;
using Amazon.Lambda.Annotations.SourceGenerator.FileIO;
using Amazon.Lambda.Annotations.SourceGenerator.Models;
using Amazon.Lambda.Annotations.SourceGenerator.Models.Attributes;
using Amazon.Lambda.Annotations.SourceGenerator.Writers;
using Amazon.Lambda.Annotations.Schedule;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace Amazon.Lambda.Annotations.SourceGenerators.Tests.WriterTests
{
    public partial class CloudFormationWriterTests
    {
        [Theory]
        [InlineData(CloudFormationTemplateFormat.Json)]
        [InlineData(CloudFormationTemplateFormat.Yaml)]
        public void VerifyScheduleEventAttributes_AreCorrectlyApplied(CloudFormationTemplateFormat templateFormat)
        {
            // ARRANGE
            var mockFileManager = GetMockFileManager(string.Empty);
            var lambdaFunctionModel = GetLambdaFunctionModel();
            lambdaFunctionModel.PackageType = LambdaPackageType.Zip;

            var att = new ScheduleEventAttribute("rate(5 minutes)")
            {
                ResourceName = "MySchedule",
                Description = "Process every 5 minutes",
                Input = "{\"key\": \"value\"}",
                Enabled = true
            };
            lambdaFunctionModel.Attributes.Add(new AttributeModel<ScheduleEventAttribute> { Data = att });
            var cloudFormationWriter = GetCloudFormationWriter(mockFileManager, _directoryManager, templateFormat, _diagnosticReporter);
            var report = GetAnnotationReport([lambdaFunctionModel]);

            // ACT
            cloudFormationWriter.ApplyReport(report);

            // ASSERT
            ITemplateWriter templateWriter = templateFormat == CloudFormationTemplateFormat.Json ? new JsonWriter() : new YamlWriter();
            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));

            var eventPath = $"Resources.{lambdaFunctionModel.ResourceName}.Properties.Events.MySchedule";
            var eventPropertiesPath = $"{eventPath}.Properties";

            Assert.True(templateWriter.Exists(eventPath));
            Assert.Equal("Schedule", templateWriter.GetToken<string>($"{eventPath}.Type"));
            Assert.Equal("rate(5 minutes)", templateWriter.GetToken<string>($"{eventPropertiesPath}.Schedule"));
            Assert.Equal("Process every 5 minutes", templateWriter.GetToken<string>($"{eventPropertiesPath}.Description"));
            Assert.Equal("{\"key\": \"value\"}", templateWriter.GetToken<string>($"{eventPropertiesPath}.Input"));
            Assert.True(templateWriter.GetToken<bool>($"{eventPropertiesPath}.Enabled"));
        }

        [Theory]
        [InlineData(CloudFormationTemplateFormat.Json)]
        [InlineData(CloudFormationTemplateFormat.Yaml)]
        public void VerifyScheduleEventProperties_AreSyncedCorrectly(CloudFormationTemplateFormat templateFormat)
        {
            // ARRANGE
            var mockFileManager = GetMockFileManager(string.Empty);
            var lambdaFunctionModel = GetLambdaFunctionModel();
            lambdaFunctionModel.PackageType = LambdaPackageType.Zip;
            var eventResourceName = "MySchedule";
            var eventPropertiesPath = $"Resources.{lambdaFunctionModel.ResourceName}.Properties.Events.{eventResourceName}.Properties";
            var syncedEventPropertiesPath = $"Resources.{lambdaFunctionModel.ResourceName}.Metadata.SyncedEventProperties";

            var initialAttribute = new ScheduleEventAttribute("rate(5 minutes)")
            {
                ResourceName = eventResourceName,
                Description = "Every 5 minutes"
            };
            lambdaFunctionModel.Attributes = [new AttributeModel<ScheduleEventAttribute> { Data = initialAttribute }];
            var cloudFormationWriter = GetCloudFormationWriter(mockFileManager, _directoryManager, templateFormat, _diagnosticReporter);
            var report = GetAnnotationReport([lambdaFunctionModel]);

            // ACT
            cloudFormationWriter.ApplyReport(report);

            // ASSERT
            ITemplateWriter templateWriter = templateFormat == CloudFormationTemplateFormat.Json ? new JsonWriter() : new YamlWriter();
            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));

            Assert.Equal("rate(5 minutes)", templateWriter.GetToken<string>($"{eventPropertiesPath}.Schedule"));
            Assert.Equal("Every 5 minutes", templateWriter.GetToken<string>($"{eventPropertiesPath}.Description"));
            Assert.False(templateWriter.Exists($"{eventPropertiesPath}.Input"));
            Assert.False(templateWriter.Exists($"{eventPropertiesPath}.Enabled"));

            var syncedEventProperties = templateWriter.GetToken<Dictionary<string, List<string>>>($"{syncedEventPropertiesPath}");
            Assert.Equal(2, syncedEventProperties[eventResourceName].Count);
            Assert.Contains("Schedule", syncedEventProperties[eventResourceName]);
            Assert.Contains("Description", syncedEventProperties[eventResourceName]);

            // Update to cron with Input
            var updatedAttribute = new ScheduleEventAttribute("cron(0 12 * * ? *)")
            {
                ResourceName = eventResourceName,
                Input = "{\"type\": \"daily\"}"
            };
            lambdaFunctionModel.Attributes = [new AttributeModel<ScheduleEventAttribute> { Data = updatedAttribute }];
            report = GetAnnotationReport([lambdaFunctionModel]);
            cloudFormationWriter.ApplyReport(report);

            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));
            Assert.Equal("cron(0 12 * * ? *)", templateWriter.GetToken<string>($"{eventPropertiesPath}.Schedule"));
            Assert.Equal("{\"type\": \"daily\"}", templateWriter.GetToken<string>($"{eventPropertiesPath}.Input"));
            Assert.False(templateWriter.Exists($"{eventPropertiesPath}.Description"));

            syncedEventProperties = templateWriter.GetToken<Dictionary<string, List<string>>>($"{syncedEventPropertiesPath}");
            Assert.Equal(2, syncedEventProperties[eventResourceName].Count);
            Assert.Contains("Schedule", syncedEventProperties[eventResourceName]);
            Assert.Contains("Input", syncedEventProperties[eventResourceName]);
        }

        [Theory]
        [InlineData(CloudFormationTemplateFormat.Json)]
        [InlineData(CloudFormationTemplateFormat.Yaml)]
        public void VerifyScheduleEvent_MinimalAttributes(CloudFormationTemplateFormat templateFormat)
        {
            // ARRANGE
            var mockFileManager = GetMockFileManager(string.Empty);
            var lambdaFunctionModel = GetLambdaFunctionModel();
            lambdaFunctionModel.PackageType = LambdaPackageType.Zip;

            var att = new ScheduleEventAttribute("rate(1 hour)") { ResourceName = "HourlySchedule" };
            lambdaFunctionModel.Attributes.Add(new AttributeModel<ScheduleEventAttribute> { Data = att });
            var cloudFormationWriter = GetCloudFormationWriter(mockFileManager, _directoryManager, templateFormat, _diagnosticReporter);
            var report = GetAnnotationReport([lambdaFunctionModel]);

            // ACT
            cloudFormationWriter.ApplyReport(report);

            // ASSERT
            ITemplateWriter templateWriter = templateFormat == CloudFormationTemplateFormat.Json ? new JsonWriter() : new YamlWriter();
            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));

            var eventPropertiesPath = $"Resources.{lambdaFunctionModel.ResourceName}.Properties.Events.HourlySchedule.Properties";
            Assert.Equal("rate(1 hour)", templateWriter.GetToken<string>($"{eventPropertiesPath}.Schedule"));
            Assert.False(templateWriter.Exists($"{eventPropertiesPath}.Description"));
            Assert.False(templateWriter.Exists($"{eventPropertiesPath}.Input"));
            Assert.False(templateWriter.Exists($"{eventPropertiesPath}.Enabled"));
        }

        [Theory]
        [InlineData(CloudFormationTemplateFormat.Json)]
        [InlineData(CloudFormationTemplateFormat.Yaml)]
        public void VerifyScheduleEventInput_RelativeFilePath_ReadsFileContents(CloudFormationTemplateFormat templateFormat)
        {
            // ARRANGE - Set up a mock file manager with a JSON file at a relative path
            var mockFileManager = GetMockFileManager(string.Empty);
            var expectedJson = "{\"action\": \"cleanup\", \"target\": \"logs\"}";
            var inputFilePath = Path.Combine(ProjectRootDirectory, "schedule-input.json");
            mockFileManager.WriteAllText(inputFilePath, expectedJson);

            var lambdaFunctionModel = GetLambdaFunctionModel();
            lambdaFunctionModel.PackageType = LambdaPackageType.Zip;

            var att = new ScheduleEventAttribute("rate(1 hour)")
            {
                ResourceName = "HourlyCleanup",
                Input = "schedule-input.json"
            };
            lambdaFunctionModel.Attributes.Add(new AttributeModel<ScheduleEventAttribute> { Data = att });
            var cloudFormationWriter = GetCloudFormationWriter(mockFileManager, _directoryManager, templateFormat, _diagnosticReporter);
            var report = GetAnnotationReport([lambdaFunctionModel]);

            // ACT
            cloudFormationWriter.ApplyReport(report);

            // ASSERT - The file contents should be used instead of the file path
            ITemplateWriter templateWriter = templateFormat == CloudFormationTemplateFormat.Json ? new JsonWriter() : new YamlWriter();
            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));

            var eventPropertiesPath = $"Resources.{lambdaFunctionModel.ResourceName}.Properties.Events.HourlyCleanup.Properties";
            Assert.Equal(expectedJson, templateWriter.GetToken<string>($"{eventPropertiesPath}.Input"));
        }

        [Theory]
        [InlineData(CloudFormationTemplateFormat.Json)]
        [InlineData(CloudFormationTemplateFormat.Yaml)]
        public void VerifyScheduleEventInput_AbsoluteFilePath_ReadsFileContents(CloudFormationTemplateFormat templateFormat)
        {
            // ARRANGE - Set up a mock file manager with a JSON file at an absolute path
            var mockFileManager = GetMockFileManager(string.Empty);
            var expectedJson = "{\"environment\": \"production\"}";
            var absoluteInputPath = Path.Combine("C:", "config", "schedule-input.json");
            mockFileManager.WriteAllText(absoluteInputPath, expectedJson);

            var lambdaFunctionModel = GetLambdaFunctionModel();
            lambdaFunctionModel.PackageType = LambdaPackageType.Zip;

            var att = new ScheduleEventAttribute("rate(5 minutes)")
            {
                ResourceName = "FrequentCheck",
                Input = absoluteInputPath
            };
            lambdaFunctionModel.Attributes.Add(new AttributeModel<ScheduleEventAttribute> { Data = att });
            var cloudFormationWriter = GetCloudFormationWriter(mockFileManager, _directoryManager, templateFormat, _diagnosticReporter);
            var report = GetAnnotationReport([lambdaFunctionModel]);

            // ACT
            cloudFormationWriter.ApplyReport(report);

            // ASSERT - The file contents should be used instead of the file path
            ITemplateWriter templateWriter = templateFormat == CloudFormationTemplateFormat.Json ? new JsonWriter() : new YamlWriter();
            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));

            var eventPropertiesPath = $"Resources.{lambdaFunctionModel.ResourceName}.Properties.Events.FrequentCheck.Properties";
            Assert.Equal(expectedJson, templateWriter.GetToken<string>($"{eventPropertiesPath}.Input"));
        }

        [Theory]
        [InlineData(CloudFormationTemplateFormat.Json)]
        [InlineData(CloudFormationTemplateFormat.Yaml)]
        public void VerifyScheduleEventInput_LiteralJson_UsedAsIs(CloudFormationTemplateFormat templateFormat)
        {
            // ARRANGE - Input is a literal JSON string, not a file path
            var mockFileManager = GetMockFileManager(string.Empty);
            var lambdaFunctionModel = GetLambdaFunctionModel();
            lambdaFunctionModel.PackageType = LambdaPackageType.Zip;

            var literalJson = "{\"key\": \"value\"}";
            var att = new ScheduleEventAttribute("rate(5 minutes)")
            {
                ResourceName = "LiteralInputSchedule",
                Input = literalJson
            };
            lambdaFunctionModel.Attributes.Add(new AttributeModel<ScheduleEventAttribute> { Data = att });
            var cloudFormationWriter = GetCloudFormationWriter(mockFileManager, _directoryManager, templateFormat, _diagnosticReporter);
            var report = GetAnnotationReport([lambdaFunctionModel]);

            // ACT
            cloudFormationWriter.ApplyReport(report);

            // ASSERT - The literal JSON should be used as-is since it doesn't resolve to a file
            ITemplateWriter templateWriter = templateFormat == CloudFormationTemplateFormat.Json ? new JsonWriter() : new YamlWriter();
            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));

            var eventPropertiesPath = $"Resources.{lambdaFunctionModel.ResourceName}.Properties.Events.LiteralInputSchedule.Properties";
            Assert.Equal(literalJson, templateWriter.GetToken<string>($"{eventPropertiesPath}.Input"));
        }

        [Theory]
        [InlineData(CloudFormationTemplateFormat.Json)]
        [InlineData(CloudFormationTemplateFormat.Yaml)]
        public void VerifyScheduleEventInput_NonExistentFilePath_UsedAsIs(CloudFormationTemplateFormat templateFormat)
        {
            // ARRANGE - Input looks like a file path but the file doesn't exist
            var mockFileManager = GetMockFileManager(string.Empty);
            var lambdaFunctionModel = GetLambdaFunctionModel();
            lambdaFunctionModel.PackageType = LambdaPackageType.Zip;

            var nonExistentPath = "does-not-exist.json";
            var att = new ScheduleEventAttribute("rate(5 minutes)")
            {
                ResourceName = "MissingFileSchedule",
                Input = nonExistentPath
            };
            lambdaFunctionModel.Attributes.Add(new AttributeModel<ScheduleEventAttribute> { Data = att });
            var cloudFormationWriter = GetCloudFormationWriter(mockFileManager, _directoryManager, templateFormat, _diagnosticReporter);
            var report = GetAnnotationReport([lambdaFunctionModel]);

            // ACT
            cloudFormationWriter.ApplyReport(report);

            // ASSERT - The path string should be used as-is since the file doesn't exist
            ITemplateWriter templateWriter = templateFormat == CloudFormationTemplateFormat.Json ? new JsonWriter() : new YamlWriter();
            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));

            var eventPropertiesPath = $"Resources.{lambdaFunctionModel.ResourceName}.Properties.Events.MissingFileSchedule.Properties";
            Assert.Equal(nonExistentPath, templateWriter.GetToken<string>($"{eventPropertiesPath}.Input"));
        }
    }
}
