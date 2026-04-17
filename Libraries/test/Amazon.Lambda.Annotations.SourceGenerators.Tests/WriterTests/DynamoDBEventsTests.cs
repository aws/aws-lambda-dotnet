// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.Annotations.SourceGenerator;
using Amazon.Lambda.Annotations.SourceGenerator.Models;
using Amazon.Lambda.Annotations.SourceGenerator.Models.Attributes;
using Amazon.Lambda.Annotations.SourceGenerator.Writers;
using Amazon.Lambda.Annotations.DynamoDB;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Amazon.Lambda.Annotations.SourceGenerators.Tests.WriterTests
{
    public partial class CloudFormationWriterTests
    {
        const string streamArn1 = "arn:aws:dynamodb:us-east-2:444455556666:table/MyTable/stream/2024-01-01T00:00:00";
        const string streamArn2 = "arn:aws:dynamodb:us-east-2:444455556666:table/MyTable2/stream/2024-01-01T00:00:00";

        [Theory]
        [ClassData(typeof(DynamoDBEventsTestData))]
        public void VerifyDynamoDBEventAttributes_AreCorrectlyApplied(CloudFormationTemplateFormat templateFormat, IEnumerable<DynamoDBEventAttribute> dynamoDBEventAttributes)
        {
            // ARRANGE
            var mockFileManager = GetMockFileManager(string.Empty);
            var lambdaFunctionModel = GetLambdaFunctionModel();
            lambdaFunctionModel.PackageType = LambdaPackageType.Zip;
            lambdaFunctionModel.ReturnTypeFullName = "void";
            foreach (var att in dynamoDBEventAttributes)
            {
                lambdaFunctionModel.Attributes.Add(new AttributeModel<DynamoDBEventAttribute> { Data = att });
            }
            var cloudFormationWriter = GetCloudFormationWriter(mockFileManager, _directoryManager, templateFormat, _diagnosticReporter);
            var report = GetAnnotationReport([lambdaFunctionModel]);

            // ACT
            cloudFormationWriter.ApplyReport(report);

            // ASSERT
            ITemplateWriter templateWriter = templateFormat == CloudFormationTemplateFormat.Json ? new JsonWriter() : new YamlWriter();
            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));

            foreach (var att in dynamoDBEventAttributes)
            {
                var eventName = att.ResourceName;
                var eventPath = $"Resources.{lambdaFunctionModel.ResourceName}.Properties.Events.{eventName}";
                var eventPropertiesPath = $"{eventPath}.Properties";

                Assert.True(templateWriter.Exists(eventPath));
                Assert.Equal("DynamoDB", templateWriter.GetToken<string>($"{eventPath}.Type"));

                if (!att.Stream.StartsWith("@"))
                {
                    Assert.Equal(att.Stream, templateWriter.GetToken<string>($"{eventPropertiesPath}.Stream"));
                }
                else
                {
                    Assert.Equal([att.Stream.Substring(1), "StreamArn"], templateWriter.GetToken<List<string>>($"{eventPropertiesPath}.Stream.Fn::GetAtt"));
                }

                Assert.Equal(att.StartingPosition, templateWriter.GetToken<string>($"{eventPropertiesPath}.StartingPosition"));

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

                Assert.Equal(att.IsMaximumBatchingWindowInSecondsSet, templateWriter.Exists($"{eventPropertiesPath}.MaximumBatchingWindowInSeconds"));
                if (att.IsMaximumBatchingWindowInSecondsSet)
                {
                    Assert.Equal(att.MaximumBatchingWindowInSeconds, templateWriter.GetToken<uint>($"{eventPropertiesPath}.MaximumBatchingWindowInSeconds"));
                }
            }
        }

        [Theory]
        [InlineData(CloudFormationTemplateFormat.Json)]
        [InlineData(CloudFormationTemplateFormat.Yaml)]
        public void VerifyDynamoDBEventProperties_AreSyncedCorrectly(CloudFormationTemplateFormat templateFormat)
        {
            // ARRANGE
            var mockFileManager = GetMockFileManager(string.Empty);
            var lambdaFunctionModel = GetLambdaFunctionModel();
            lambdaFunctionModel.PackageType = LambdaPackageType.Zip;
            var eventResourceName = "MyDynamoDBEvent";
            var eventPropertiesPath = $"Resources.{lambdaFunctionModel.ResourceName}.Properties.Events.{eventResourceName}.Properties";
            var syncedEventPropertiesPath = $"Resources.{lambdaFunctionModel.ResourceName}.Metadata.SyncedEventProperties";

            var initialAttribute = new DynamoDBEventAttribute(streamArn1)
            {
                ResourceName = eventResourceName,
                MaximumBatchingWindowInSeconds = 15
            };
            lambdaFunctionModel.Attributes = [new AttributeModel<DynamoDBEventAttribute> { Data = initialAttribute }];
            var cloudFormationWriter = GetCloudFormationWriter(mockFileManager, _directoryManager, templateFormat, _diagnosticReporter);
            var report = GetAnnotationReport([lambdaFunctionModel]);

            // ACT
            cloudFormationWriter.ApplyReport(report);

            // Assert initial properties
            ITemplateWriter templateWriter = templateFormat == CloudFormationTemplateFormat.Json ? new JsonWriter() : new YamlWriter();
            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));

            Assert.Equal(15, templateWriter.GetToken<int>($"{eventPropertiesPath}.MaximumBatchingWindowInSeconds"));
            Assert.Equal(streamArn1, templateWriter.GetToken<string>($"{eventPropertiesPath}.Stream"));
            Assert.Equal("LATEST", templateWriter.GetToken<string>($"{eventPropertiesPath}.StartingPosition"));
            Assert.False(templateWriter.Exists($"{eventPropertiesPath}.BatchSize"));
            Assert.False(templateWriter.Exists($"{eventPropertiesPath}.Enabled"));
            Assert.False(templateWriter.Exists($"{eventPropertiesPath}.FilterCriteria"));

            var syncedEventProperties = templateWriter.GetToken<Dictionary<string, List<string>>>($"{syncedEventPropertiesPath}");
            Assert.Equal(3, syncedEventProperties[eventResourceName].Count);
            Assert.Contains("Stream", syncedEventProperties[eventResourceName]);
            Assert.Contains("StartingPosition", syncedEventProperties[eventResourceName]);
            Assert.Contains("MaximumBatchingWindowInSeconds", syncedEventProperties[eventResourceName]);

            // Update attribute
            var updatedAttribute = new DynamoDBEventAttribute(streamArn2)
            {
                ResourceName = eventResourceName,
                BatchSize = 10
            };
            lambdaFunctionModel.Attributes = [new AttributeModel<DynamoDBEventAttribute> { Data = updatedAttribute }];
            report = GetAnnotationReport([lambdaFunctionModel]);
            cloudFormationWriter.ApplyReport(report);

            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));
            Assert.Equal(10, templateWriter.GetToken<int>($"{eventPropertiesPath}.BatchSize"));
            Assert.Equal(streamArn2, templateWriter.GetToken<string>($"{eventPropertiesPath}.Stream"));
            Assert.Equal("LATEST", templateWriter.GetToken<string>($"{eventPropertiesPath}.StartingPosition"));
            Assert.False(templateWriter.Exists($"{eventPropertiesPath}.MaximumBatchingWindowInSeconds"));
            Assert.False(templateWriter.Exists($"{eventPropertiesPath}.Enabled"));

            syncedEventProperties = templateWriter.GetToken<Dictionary<string, List<string>>>($"{syncedEventPropertiesPath}");
            Assert.Equal(3, syncedEventProperties[eventResourceName].Count);
            Assert.Contains("BatchSize", syncedEventProperties[eventResourceName]);
            Assert.Contains("Stream", syncedEventProperties[eventResourceName]);
            Assert.Contains("StartingPosition", syncedEventProperties[eventResourceName]);
        }

        [Theory]
        [InlineData(CloudFormationTemplateFormat.Json)]
        [InlineData(CloudFormationTemplateFormat.Yaml)]
        public void SwitchBetweenArnAndRef_ForDynamoDBStream(CloudFormationTemplateFormat templateFormat)
        {
            // Arrange
            ITemplateWriter templateWriter = templateFormat == CloudFormationTemplateFormat.Json ? new JsonWriter() : new YamlWriter();
            var mockFileManager = GetMockFileManager(string.Empty);
            var cloudFormationWriter = GetCloudFormationWriter(mockFileManager, _directoryManager, templateFormat, _diagnosticReporter);

            var lambdaFunctionModel = GetLambdaFunctionModel();
            var eventResourceName = "MyDynamoDBEvent";

            var syncedEventPropertiesPath = $"Resources.{lambdaFunctionModel.ResourceName}.Metadata.SyncedEventProperties";
            var eventPropertiesPath = $"Resources.{lambdaFunctionModel.ResourceName}.Properties.Events.{eventResourceName}.Properties";

            // Start with Stream ARN
            var dynamoDBEventAttribute = new DynamoDBEventAttribute(streamArn1) { ResourceName = eventResourceName };
            lambdaFunctionModel.Attributes = [new AttributeModel<DynamoDBEventAttribute> { Data = dynamoDBEventAttribute }];

            // Act
            var report = GetAnnotationReport([lambdaFunctionModel]);
            cloudFormationWriter.ApplyReport(report);

            // Assert - Stream as ARN
            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));
            var syncedEventProperties = templateWriter.GetToken<Dictionary<string, List<string>>>($"{syncedEventPropertiesPath}");

            Assert.Equal(streamArn1, templateWriter.GetToken<string>($"{eventPropertiesPath}.Stream"));
            Assert.False(templateWriter.Exists($"{eventPropertiesPath}.Stream.Fn::GetAtt"));
            Assert.Contains("Stream", syncedEventProperties[eventResourceName]);

            // Switch to Stream reference
            dynamoDBEventAttribute.Stream = "@MyTable";
            cloudFormationWriter.ApplyReport(report);

            // Assert - Stream as Ref
            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));
            syncedEventProperties = templateWriter.GetToken<Dictionary<string, List<string>>>($"{syncedEventPropertiesPath}");

            Assert.Equal(["MyTable", "StreamArn"], templateWriter.GetToken<List<string>>($"{eventPropertiesPath}.Stream.Fn::GetAtt"));
            Assert.Contains("Stream.Fn::GetAtt", syncedEventProperties[eventResourceName]);
        }

        [Theory]
        [InlineData(CloudFormationTemplateFormat.Json)]
        [InlineData(CloudFormationTemplateFormat.Yaml)]
        public void VerifyStreamCanBeSet_FromCloudFormationParameter(CloudFormationTemplateFormat templateFormat)
        {
            // ARRANGE
            const string jsonContent = @"{
                       'Parameters':{
                          'MyTable':{
                             'Type':'String',
                             'Default':'arn:aws:dynamodb:us-east-2:444455556666:table/MyTable/stream/2024-01-01T00:00:00'
                          }
                       }
                    }";

            const string yamlContent = @"Parameters:
                                          MyTable:
                                            Type: String
                                            Default: arn:aws:dynamodb:us-east-2:444455556666:table/MyTable/stream/2024-01-01T00:00:00";

            ITemplateWriter templateWriter = templateFormat == CloudFormationTemplateFormat.Json ? new JsonWriter() : new YamlWriter();
            var content = templateFormat == CloudFormationTemplateFormat.Json ? jsonContent : yamlContent;

            var mockFileManager = GetMockFileManager(content);
            var lambdaFunctionModel = GetLambdaFunctionModel();
            var eventResourceName = "MyDynamoDBEvent";
            var dynamoDBEventAttribute = new DynamoDBEventAttribute("@MyTable") { ResourceName = eventResourceName };
            lambdaFunctionModel.Attributes = [new AttributeModel<DynamoDBEventAttribute> { Data = dynamoDBEventAttribute }];
            var cloudFormationWriter = GetCloudFormationWriter(mockFileManager, _directoryManager, templateFormat, _diagnosticReporter);
            var report = GetAnnotationReport([lambdaFunctionModel]);

            var eventPropertiesPath = $"Resources.{lambdaFunctionModel.ResourceName}.Properties.Events.{eventResourceName}.Properties";
            var syncedEventPropertiesPath = $"Resources.{lambdaFunctionModel.ResourceName}.Metadata.SyncedEventProperties";

            // ACT
            cloudFormationWriter.ApplyReport(report);

            // Verify Stream property exists as a Ref (when @name matches a CF Parameter, writer uses Ref)
            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));
            Assert.Equal("MyTable", templateWriter.GetToken<string>($"{eventPropertiesPath}.Stream.Ref"));
            Assert.False(templateWriter.Exists($"{eventPropertiesPath}.Stream.Fn::GetAtt"));

            // Verify the list of synced event properties
            var syncedEventProperties = templateWriter.GetToken<Dictionary<string, List<string>>>($"{syncedEventPropertiesPath}");
            Assert.Contains("Stream.Ref", syncedEventProperties[eventResourceName]);

            // Change the Stream property to be an ARN and re-generate the template
            dynamoDBEventAttribute.Stream = streamArn1;
            lambdaFunctionModel.Attributes = [new AttributeModel<DynamoDBEventAttribute> { Data = dynamoDBEventAttribute }];
            report = GetAnnotationReport([lambdaFunctionModel]);
            cloudFormationWriter.ApplyReport(report);

            // Verify Stream property exists as an ARN
            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));
            Assert.Equal(streamArn1, templateWriter.GetToken<string>($"{eventPropertiesPath}.Stream"));
            Assert.False(templateWriter.Exists($"{eventPropertiesPath}.Stream.Fn::GetAtt"));
            Assert.False(templateWriter.Exists($"{eventPropertiesPath}.Stream.Ref"));

            // Verify the list of synced event properties
            syncedEventProperties = templateWriter.GetToken<Dictionary<string, List<string>>>($"{syncedEventPropertiesPath}");
            Assert.Contains("Stream", syncedEventProperties[eventResourceName]);
        }

        [Theory]
        [InlineData(CloudFormationTemplateFormat.Json)]
        [InlineData(CloudFormationTemplateFormat.Yaml)]
        public void VerifyManuallySetDynamoDBEventProperties_ArePreserved(CloudFormationTemplateFormat templateFormat)
        {
            // ARRANGE
            var mockFileManager = GetMockFileManager(string.Empty);
            var lambdaFunctionModel = GetLambdaFunctionModel();
            lambdaFunctionModel.PackageType = LambdaPackageType.Zip;
            var eventResourceName = "MyDynamoDBEvent";
            var eventPropertiesPath = $"Resources.{lambdaFunctionModel.ResourceName}.Properties.Events.{eventResourceName}.Properties";
            var syncedEventPropertiesPath = $"Resources.{lambdaFunctionModel.ResourceName}.Metadata.SyncedEventProperties";

            var initialAttribute = new DynamoDBEventAttribute(streamArn1)
            {
                ResourceName = eventResourceName,
                BatchSize = 20
            };
            lambdaFunctionModel.Attributes = [new AttributeModel<DynamoDBEventAttribute> { Data = initialAttribute }];
            var cloudFormationWriter = GetCloudFormationWriter(mockFileManager, _directoryManager, templateFormat, _diagnosticReporter);
            var report = GetAnnotationReport([lambdaFunctionModel]);

            // ACT
            cloudFormationWriter.ApplyReport(report);

            // Assert that initial attributes properties are correctly set
            ITemplateWriter templateWriter = templateFormat == CloudFormationTemplateFormat.Json ? new JsonWriter() : new YamlWriter();
            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));

            Assert.Equal(20, templateWriter.GetToken<int>($"{eventPropertiesPath}.BatchSize"));
            Assert.Equal(streamArn1, templateWriter.GetToken<string>($"{eventPropertiesPath}.Stream"));

            // Verify initial attribute properties are synced
            var syncedEventProperties = templateWriter.GetToken<Dictionary<string, List<string>>>($"{syncedEventPropertiesPath}");
            Assert.Contains("BatchSize", syncedEventProperties[eventResourceName]);
            Assert.Contains("Stream", syncedEventProperties[eventResourceName]);

            // Modify the serverless template by hand and add a new property
            templateWriter.SetToken($"{eventPropertiesPath}.MaximumBatchingWindowInSeconds", 30);
            mockFileManager.WriteAllText(ServerlessTemplateFilePath, templateWriter.GetContent());

            // Perform another source generation
            cloudFormationWriter.ApplyReport(report);

            // Assert that both the initial properties and the manually added property exists
            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));
            Assert.Equal(20, templateWriter.GetToken<int>($"{eventPropertiesPath}.BatchSize"));
            Assert.Equal(streamArn1, templateWriter.GetToken<string>($"{eventPropertiesPath}.Stream"));
            Assert.Equal(30, templateWriter.GetToken<int>($"{eventPropertiesPath}.MaximumBatchingWindowInSeconds"));

            // Assert that the synced event properties are still the same and the manually set property is not synced
            syncedEventProperties = templateWriter.GetToken<Dictionary<string, List<string>>>($"{syncedEventPropertiesPath}");
            Assert.Contains("BatchSize", syncedEventProperties[eventResourceName]);
            Assert.Contains("Stream", syncedEventProperties[eventResourceName]);
            Assert.DoesNotContain("MaximumBatchingWindowInSeconds", syncedEventProperties[eventResourceName]);
        }

        public class DynamoDBEventsTestData : TheoryData<CloudFormationTemplateFormat, IEnumerable<DynamoDBEventAttribute>>
        {
            public DynamoDBEventsTestData()
            {
                foreach (var templateFormat in new List<CloudFormationTemplateFormat> { CloudFormationTemplateFormat.Json, CloudFormationTemplateFormat.Yaml })
                {
                    // Simple attribute
                    Add(templateFormat, [new(streamArn1)]);

                    // Multiple DynamoDBEvent attributes
                    Add(templateFormat, [new(streamArn1), new(streamArn2)]);

                    // Use table reference
                    Add(templateFormat, [new("@MyTable")]);

                    // Specify filters
                    Add(templateFormat, [new(streamArn1) { Filters = "SOME-FILTER1; SOME-FILTER2" }]);

                    // Explicitly specify all properties
                    Add(templateFormat,
                        [new(streamArn1)
                        {
                            BatchSize = 10,
                            Filters = "SOME-FILTER1; SOME-FILTER2",
                            MaximumBatchingWindowInSeconds = 15,
                            Enabled = false,
                            StartingPosition = "TRIM_HORIZON"
                        }]);
                }
            }
        }
    }
}
