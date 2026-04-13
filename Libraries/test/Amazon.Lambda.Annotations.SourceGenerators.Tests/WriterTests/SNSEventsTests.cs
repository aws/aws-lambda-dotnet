using Amazon.Lambda.Annotations.SourceGenerator;
using Amazon.Lambda.Annotations.SourceGenerator.Models;
using Amazon.Lambda.Annotations.SourceGenerator.Models.Attributes;
using Amazon.Lambda.Annotations.SourceGenerator.Writers;
using Amazon.Lambda.Annotations.SNS;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Amazon.Lambda.Annotations.SourceGenerators.Tests.WriterTests
{
    public partial class CloudFormationWriterTests
    {
        const string topicArn1 = "arn:aws:sns:us-east-2:444455556666:topic1";
        const string topicArn2 = "arn:aws:sns:us-east-2:444455556666:topic2";

        [Theory]
        [ClassData(typeof(SnsEventsTestData))]
        public void VerifySNSEventAttributes_AreCorrectlyApplied(CloudFormationTemplateFormat templateFormat, IEnumerable<SNSEventAttribute> snsEventAttributes)
        {
            // ARRANGE
            var mockFileManager = GetMockFileManager(string.Empty);
            var lambdaFunctionModel = GetLambdaFunctionModel();
            lambdaFunctionModel.PackageType = LambdaPackageType.Zip;
            foreach (var att in snsEventAttributes)
            {
                lambdaFunctionModel.Attributes.Add(new AttributeModel<SNSEventAttribute> { Data = att });
            }
            var cloudFormationWriter = GetCloudFormationWriter(mockFileManager, _directoryManager, templateFormat, _diagnosticReporter);
            var report = GetAnnotationReport([lambdaFunctionModel]);

            // ACT
            cloudFormationWriter.ApplyReport(report);

            // ASSERT
            ITemplateWriter templateWriter = templateFormat == CloudFormationTemplateFormat.Json ? new JsonWriter() : new YamlWriter();
            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));

            foreach (var att in snsEventAttributes)
            {
                var eventName = att.ResourceName;
                var eventPath = $"Resources.{lambdaFunctionModel.ResourceName}.Properties.Events.{eventName}";
                var eventPropertiesPath = $"{eventPath}.Properties";

                Assert.True(templateWriter.Exists(eventPath));
                Assert.Equal("SNS", templateWriter.GetToken<string>($"{eventPath}.Type"));

                if (!att.Topic.StartsWith("@"))
                {
                    Assert.Equal(att.Topic, templateWriter.GetToken<string>($"{eventPropertiesPath}.Topic"));
                }
                else
                {
                    Assert.Equal(att.Topic.Substring(1), templateWriter.GetToken<string>($"{eventPropertiesPath}.Topic.Ref"));
                }

                Assert.Equal(att.IsFilterPolicySet, templateWriter.Exists($"{eventPropertiesPath}.FilterPolicy"));
                if (att.IsFilterPolicySet)
                {
                    Assert.Equal(att.FilterPolicy, templateWriter.GetToken<string>($"{eventPropertiesPath}.FilterPolicy"));
                }

                Assert.Equal(att.IsEnabledSet, templateWriter.Exists($"{eventPropertiesPath}.Enabled"));
                if (att.IsEnabledSet)
                {
                    Assert.Equal(att.Enabled, templateWriter.GetToken<bool>($"{eventPropertiesPath}.Enabled"));
                }
            }
        }

        [Theory]
        [InlineData(CloudFormationTemplateFormat.Json)]
        [InlineData(CloudFormationTemplateFormat.Yaml)]
        public void VerifySNSEventProperties_AreSyncedCorrectly(CloudFormationTemplateFormat templateFormat)
        {
            // ARRANGE
            var mockFileManager = GetMockFileManager(string.Empty);
            var lambdaFunctionModel = GetLambdaFunctionModel();
            lambdaFunctionModel.PackageType = LambdaPackageType.Zip;
            var eventResourceName = "MySNSEvent";
            var eventPropertiesPath = $"Resources.{lambdaFunctionModel.ResourceName}.Properties.Events.{eventResourceName}.Properties";
            var syncedEventPropertiesPath = $"Resources.{lambdaFunctionModel.ResourceName}.Metadata.SyncedEventProperties";

            var initialAttribute = new SNSEventAttribute(topicArn1)
            {
                ResourceName = eventResourceName,
                FilterPolicy = "{ \"store\": [\"example_corp\"] }"
            };
            lambdaFunctionModel.Attributes = [new AttributeModel<SNSEventAttribute> { Data = initialAttribute }];
            var cloudFormationWriter = GetCloudFormationWriter(mockFileManager, _directoryManager, templateFormat, _diagnosticReporter);
            var report = GetAnnotationReport([lambdaFunctionModel]);

            // ACT
            cloudFormationWriter.ApplyReport(report);

            // ASSERT
            ITemplateWriter templateWriter = templateFormat == CloudFormationTemplateFormat.Json ? new JsonWriter() : new YamlWriter();
            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));

            Assert.Equal(topicArn1, templateWriter.GetToken<string>($"{eventPropertiesPath}.Topic"));
            Assert.Equal("{ \"store\": [\"example_corp\"] }", templateWriter.GetToken<string>($"{eventPropertiesPath}.FilterPolicy"));
            Assert.False(templateWriter.Exists($"{eventPropertiesPath}.Enabled"));

            var syncedEventProperties = templateWriter.GetToken<Dictionary<string, List<string>>>($"{syncedEventPropertiesPath}");
            Assert.Equal(2, syncedEventProperties[eventResourceName].Count);
            Assert.Contains("Topic", syncedEventProperties[eventResourceName]);
            Assert.Contains("FilterPolicy", syncedEventProperties[eventResourceName]);

            // Update attribute - remove FilterPolicy, add Enabled
            var updatedAttribute = new SNSEventAttribute(topicArn2)
            {
                ResourceName = eventResourceName,
                Enabled = false
            };
            lambdaFunctionModel.Attributes = [new AttributeModel<SNSEventAttribute> { Data = updatedAttribute }];
            report = GetAnnotationReport([lambdaFunctionModel]);

            cloudFormationWriter.ApplyReport(report);

            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));

            Assert.Equal(topicArn2, templateWriter.GetToken<string>($"{eventPropertiesPath}.Topic"));
            Assert.False(templateWriter.Exists($"{eventPropertiesPath}.FilterPolicy"));
            Assert.False(templateWriter.GetToken<bool>($"{eventPropertiesPath}.Enabled"));

            syncedEventProperties = templateWriter.GetToken<Dictionary<string, List<string>>>($"{syncedEventPropertiesPath}");
            Assert.Equal(2, syncedEventProperties[eventResourceName].Count);
            Assert.Contains("Topic", syncedEventProperties[eventResourceName]);
            Assert.Contains("Enabled", syncedEventProperties[eventResourceName]);
        }

        [Theory]
        [InlineData(CloudFormationTemplateFormat.Json)]
        [InlineData(CloudFormationTemplateFormat.Yaml)]
        public void VerifySNSTopicCanBeSet_FromCloudFormationParameter(CloudFormationTemplateFormat templateFormat)
        {
            // ARRANGE
            const string jsonContent = @"{
                       'Parameters':{
                          'MyTopic':{
                             'Type':'String',
                             'Default':'arn:aws:sns:us-east-2:444455556666:topic1'
                          }
                       }
                    }";

            const string yamlContent = @"Parameters:
                                          MyTopic:
                                            Type: String
                                            Default: arn:aws:sns:us-east-2:444455556666:topic1";

            ITemplateWriter templateWriter = templateFormat == CloudFormationTemplateFormat.Json ? new JsonWriter() : new YamlWriter();
            var content = templateFormat == CloudFormationTemplateFormat.Json ? jsonContent : yamlContent;

            var mockFileManager = GetMockFileManager(content);
            var lambdaFunctionModel = GetLambdaFunctionModel();
            var eventResourceName = "MySNSEvent";
            var snsEventAttribute = new SNSEventAttribute("@MyTopic") { ResourceName = eventResourceName };
            lambdaFunctionModel.Attributes = [new AttributeModel<SNSEventAttribute> { Data = snsEventAttribute }];
            var cloudFormationWriter = GetCloudFormationWriter(mockFileManager, _directoryManager, templateFormat, _diagnosticReporter);
            var report = GetAnnotationReport([lambdaFunctionModel]);

            var snsEventPropertiesPath = $"Resources.{lambdaFunctionModel.ResourceName}.Properties.Events.{eventResourceName}.Properties";
            var syncedEventPropertiesPath = $"Resources.{lambdaFunctionModel.ResourceName}.Metadata.SyncedEventProperties";

            // ACT
            cloudFormationWriter.ApplyReport(report);

            // ASSERT - Topic uses Ref (SNS topics use Ref to get the ARN)
            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));
            Assert.Equal("MyTopic", templateWriter.GetToken<string>($"{snsEventPropertiesPath}.Topic.Ref"));
            Assert.False(templateWriter.Exists($"{snsEventPropertiesPath}.Topic.Fn::GetAtt"));

            var syncedEventProperties = templateWriter.GetToken<Dictionary<string, List<string>>>($"{syncedEventPropertiesPath}");
            Assert.Single(syncedEventProperties[eventResourceName]);
            Assert.Equal("Topic.Ref", syncedEventProperties[eventResourceName][0]);
        }

        [Theory]
        [InlineData(CloudFormationTemplateFormat.Json)]
        [InlineData(CloudFormationTemplateFormat.Yaml)]
        public void SwitchBetweenArnAndRef_ForTopic(CloudFormationTemplateFormat templateFormat)
        {
            // Arrange
            ITemplateWriter templateWriter = templateFormat == CloudFormationTemplateFormat.Json ? new JsonWriter() : new YamlWriter();
            var mockFileManager = GetMockFileManager(string.Empty);
            var cloudFormationWriter = GetCloudFormationWriter(mockFileManager, _directoryManager, templateFormat, _diagnosticReporter);

            var lambdaFunctionModel = GetLambdaFunctionModel();
            var eventResourceName = "MySNSEvent";

            var syncedEventPropertiesPath = $"Resources.{lambdaFunctionModel.ResourceName}.Metadata.SyncedEventProperties";
            var snsEventPropertiesPath = $"Resources.{lambdaFunctionModel.ResourceName}.Properties.Events.{eventResourceName}.Properties";

            // Start with Topic ARN
            var snsEventAttribute = new SNSEventAttribute(topicArn1) { ResourceName = eventResourceName };
            lambdaFunctionModel.Attributes = [new AttributeModel<SNSEventAttribute> { Data = snsEventAttribute }];

            // Act
            var report = GetAnnotationReport([lambdaFunctionModel]);
            cloudFormationWriter.ApplyReport(report);

            // Assert - Topic is ARN
            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));
            Assert.Equal(topicArn1, templateWriter.GetToken<string>($"{snsEventPropertiesPath}.Topic"));
            Assert.False(templateWriter.Exists($"{snsEventPropertiesPath}.Topic.Ref"));

            var syncedEventProperties = templateWriter.GetToken<Dictionary<string, List<string>>>($"{syncedEventPropertiesPath}");
            Assert.Single(syncedEventProperties[eventResourceName]);
            Assert.Equal("Topic", syncedEventProperties[eventResourceName][0]);

            // Switch to Topic reference
            snsEventAttribute.Topic = "@MyTopic";
            cloudFormationWriter.ApplyReport(report);

            // Assert - Topic is Ref
            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));
            Assert.Equal("MyTopic", templateWriter.GetToken<string>($"{snsEventPropertiesPath}.Topic.Ref"));

            syncedEventProperties = templateWriter.GetToken<Dictionary<string, List<string>>>($"{syncedEventPropertiesPath}");
            Assert.Single(syncedEventProperties[eventResourceName]);
            Assert.Equal("Topic.Ref", syncedEventProperties[eventResourceName][0]);
        }

        public class SnsEventsTestData : TheoryData<CloudFormationTemplateFormat, IEnumerable<SNSEventAttribute>>
        {
            public SnsEventsTestData()
            {
                foreach (var templateFormat in new List<CloudFormationTemplateFormat> { CloudFormationTemplateFormat.Json, CloudFormationTemplateFormat.Yaml })
                {
                    // Simple attribute
                    Add(templateFormat, [new(topicArn1)]);

                    // Multiple SNSEvent attributes
                    Add(templateFormat, [new(topicArn1), new(topicArn2)]);

                    // Use topic reference
                    Add(templateFormat, [new("@MyTopic")]);

                    // Use both ARN and topic reference
                    Add(templateFormat, [new(topicArn1), new("@MyTopic")]);

                    // Specify filter policy
                    Add(templateFormat, [new(topicArn1) { FilterPolicy = "{ \"store\": [\"example_corp\"] }" }]);

                    // Explicitly specify all properties
                    Add(templateFormat,
                        [new(topicArn1)
                        {
                            FilterPolicy = "{ \"store\": [\"example_corp\"] }",
                            Enabled = false
                        }]);
                }
            }
        }
    }
}
