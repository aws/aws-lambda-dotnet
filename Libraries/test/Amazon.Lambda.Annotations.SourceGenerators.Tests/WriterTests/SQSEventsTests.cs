using Amazon.Lambda.Annotations.SourceGenerator;
using Amazon.Lambda.Annotations.SourceGenerator.Models;
using Amazon.Lambda.Annotations.SourceGenerator.Models.Attributes;
using Amazon.Lambda.Annotations.SourceGenerator.Writers;
using Amazon.Lambda.Annotations.SQS;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Amazon.Lambda.Annotations.SourceGenerators.Tests.WriterTests
{
    public partial class CloudFormationWriterTests
    {
        const string queueArn1 = "arn:aws:sqs:us-east-2:444455556666:queue1";
        const string queueArn2 = "arn:aws:sqs:us-east-2:444455556666:queue2";

        [Theory]
        [ClassData(typeof(SqsEventsTestData))]
        public void VerifySQSEventAttributes_AreCorrectlyApplied(CloudFormationTemplateFormat templateFormat, IEnumerable<SQSEventAttribute> sqsEventAttributes, string lambdaReturnType)
        {
            // ARRANGE
            var mockFileManager = GetMockFileManager(string.Empty);
            var lambdaFunctionModel = GetLambdaFunctionModel();
            lambdaFunctionModel.PackageType = LambdaPackageType.Zip;
            lambdaFunctionModel.ReturnTypeFullName = lambdaReturnType;
            foreach (var att in sqsEventAttributes)
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

            foreach (var att in sqsEventAttributes)
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

                Assert.Equal(att.IsBatchSizeSet, templateWriter.Exists($"{eventPropertiesPath}.Enabled"));
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

        [Theory]
        [InlineData(CloudFormationTemplateFormat.Json)]
        [InlineData(CloudFormationTemplateFormat.Yaml)]
        public void VerifySQSEventProperties_AreSyncedCorrectly(CloudFormationTemplateFormat templateFormat)
        {
            // ARRANGE
            var mockFileManager = GetMockFileManager(string.Empty);
            var lambdaFunctionModel = GetLambdaFunctionModel();
            lambdaFunctionModel.PackageType = LambdaPackageType.Zip;
            var eventResourceName = "MySQSEvent";
            var eventPropertiesPath = $"Resources.{lambdaFunctionModel.ResourceName}.Properties.Events.{eventResourceName}.Properties";
            var syncedEventPropertiesPath = $"Resources.{lambdaFunctionModel.ResourceName}.Metadata.SyncedEventProperties";

            // Annotate the lambda function with this initial attribute
            var initialAttribute = new SQSEventAttribute(queueArn1)
            {
                ResourceName = eventResourceName,
                MaximumBatchingWindowInSeconds = 15,
                MaximumConcurrency = 30
            };
            lambdaFunctionModel.Attributes = [new AttributeModel<SQSEventAttribute> { Data = initialAttribute }];
            var cloudFormationWriter = GetCloudFormationWriter(mockFileManager, _directoryManager, CloudFormationTemplateFormat.Json, _diagnosticReporter);
            var report = GetAnnotationReport([lambdaFunctionModel]);

            // ACT
            cloudFormationWriter.ApplyReport(report);

            // Assert that initial attributes properties are correctly set
            ITemplateWriter templateWriter = templateFormat == CloudFormationTemplateFormat.Json ? new JsonWriter() : new YamlWriter();
            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));

            // These attributes should exists
            Assert.Equal(15, templateWriter.GetToken<int>($"{eventPropertiesPath}.MaximumBatchingWindowInSeconds"));
            Assert.Equal(30, templateWriter.GetToken<int>($"{eventPropertiesPath}.ScalingConfig.MaximumConcurrency"));
            Assert.Equal(queueArn1, templateWriter.GetToken<string>($"{eventPropertiesPath}.Queue"));

            // These attributes should not exist since they were not set
            Assert.False(templateWriter.Exists($"{eventPropertiesPath}.BatchSize"));
            Assert.False(templateWriter.Exists($"{eventPropertiesPath}.Enabled"));
            Assert.False(templateWriter.Exists($"{eventPropertiesPath}.FilterCriteria"));
            Assert.False(templateWriter.Exists($"{eventPropertiesPath}.FunctionResponseTypes"));

            // Verify the list of synced event properties
            var syncedEventProperties = templateWriter.GetToken<Dictionary<string, List<string>>>($"{syncedEventPropertiesPath}");
            Assert.Equal(3, syncedEventProperties[eventResourceName].Count);
            Assert.Contains("MaximumBatchingWindowInSeconds", syncedEventProperties[eventResourceName]);
            Assert.Contains("ScalingConfig.MaximumConcurrency", syncedEventProperties[eventResourceName]);
            Assert.Contains("Queue", syncedEventProperties[eventResourceName]);

            // Apply updated SQSEvent attribute with new values
            var updatedAttribute = new SQSEventAttribute(queueArn2)
            {
                ResourceName = eventResourceName,
                BatchSize = 10
            };
            lambdaFunctionModel.Attributes = [new AttributeModel<SQSEventAttribute> { Data = updatedAttribute }];
            report = GetAnnotationReport([lambdaFunctionModel]);

            // Apply the new report
            cloudFormationWriter.ApplyReport(report);

            // Assert that updated attributes properties are correctly set
            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));

            // These attributes should exists
            Assert.Equal(10, templateWriter.GetToken<int>($"{eventPropertiesPath}.BatchSize"));
            Assert.Equal(queueArn2, templateWriter.GetToken<string>($"{eventPropertiesPath}.Queue"));

            // These attributes should not exist since they were not set
            Assert.False(templateWriter.Exists($"{eventPropertiesPath}.MaximumBatchingWindowInSeconds"));
            Assert.False(templateWriter.Exists($"{eventPropertiesPath}.Enabled"));
            Assert.False(templateWriter.Exists($"{eventPropertiesPath}.FilterCriteria"));
            Assert.False(templateWriter.Exists($"{eventPropertiesPath}.ScalingConfig"));

            // Verify the list of synced event properties
            syncedEventProperties = templateWriter.GetToken<Dictionary<string, List<string>>>($"{syncedEventPropertiesPath}");
            Assert.Equal(2, syncedEventProperties[eventResourceName].Count);
            Assert.Contains("BatchSize", syncedEventProperties[eventResourceName]);
            Assert.Contains("Queue", syncedEventProperties[eventResourceName]);
        }

        [Theory]
        [InlineData(CloudFormationTemplateFormat.Json)]
        [InlineData(CloudFormationTemplateFormat.Yaml)]
        public void VerifyManuallySetEventProperties_ArePreserved(CloudFormationTemplateFormat templateFormat)
        {
            // ARRANGE
            var mockFileManager = GetMockFileManager(string.Empty);
            var lambdaFunctionModel = GetLambdaFunctionModel();
            lambdaFunctionModel.PackageType = LambdaPackageType.Zip;
            var eventResourceName = "MySQSEvent";
            var eventPropertiesPath = $"Resources.{lambdaFunctionModel.ResourceName}.Properties.Events.{eventResourceName}.Properties";
            var syncedEventPropertiesPath = $"Resources.{lambdaFunctionModel.ResourceName}.Metadata.SyncedEventProperties";

            // Annotate the lambda function with this initial attribute
            var initialAttribute = new SQSEventAttribute(queueArn1)
            {
                ResourceName = eventResourceName,
                BatchSize = 20
            };
            lambdaFunctionModel.Attributes = [new AttributeModel<SQSEventAttribute> { Data = initialAttribute }];
            var cloudFormationWriter = GetCloudFormationWriter(mockFileManager, _directoryManager, CloudFormationTemplateFormat.Json, _diagnosticReporter);
            var report = GetAnnotationReport([lambdaFunctionModel]);

            // ACT
            cloudFormationWriter.ApplyReport(report);

            // Assert that initial attributes properties are correctly set
            ITemplateWriter templateWriter = templateFormat == CloudFormationTemplateFormat.Json ? new JsonWriter() : new YamlWriter();
            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));

            // These attributes should exists
            Assert.Equal(20, templateWriter.GetToken<int>($"{eventPropertiesPath}.BatchSize"));
            Assert.Equal(queueArn1, templateWriter.GetToken<string>($"{eventPropertiesPath}.Queue"));

            // Verify initial attribute properties are synced
            var syncedEventProperties = templateWriter.GetToken<Dictionary<string, List<string>>>($"{syncedEventPropertiesPath}");
            Assert.Equal(2, syncedEventProperties[eventResourceName].Count);
            Assert.Contains("BatchSize", syncedEventProperties[eventResourceName]);
            Assert.Contains("Queue", syncedEventProperties[eventResourceName]);

            // Modify the serverless template by hand and add a new property
            templateWriter.SetToken($"{eventPropertiesPath}.ScalingConfig.MaximumConcurrency", 30);
            mockFileManager.WriteAllText(ServerlessTemplateFilePath, templateWriter.GetContent());

            // Perform another source generation
            cloudFormationWriter.ApplyReport(report);

            // Assert that both the initial properties and the manually added property exists
            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));
            Assert.Equal(20, templateWriter.GetToken<int>($"{eventPropertiesPath}.BatchSize"));
            Assert.Equal(queueArn1, templateWriter.GetToken<string>($"{eventPropertiesPath}.Queue"));
            Assert.Equal(30, templateWriter.GetToken<int>($"{eventPropertiesPath}.ScalingConfig.MaximumConcurrency"));

            // Assert that the synced event properties are still the same and the manually set property is not synced
            syncedEventProperties = templateWriter.GetToken<Dictionary<string, List<string>>>($"{syncedEventPropertiesPath}");
            Assert.Equal(2, syncedEventProperties[eventResourceName].Count);
            Assert.Contains("BatchSize", syncedEventProperties[eventResourceName]);
            Assert.Contains("Queue", syncedEventProperties[eventResourceName]);
            Assert.DoesNotContain("ScalingConfig.MaximumConcurrency", syncedEventProperties[eventResourceName]);
        }

        [Theory]
        [InlineData(CloudFormationTemplateFormat.Json)]
        [InlineData(CloudFormationTemplateFormat.Yaml)]
        public void VerifyQueueCanBeSet_FromCloudFormationParameter(CloudFormationTemplateFormat templateFormat)
        {
            // ARRANGE
            const string jsonContent = @"{
                       'Parameters':{
                          'MyQueue':{
                             'Type':'String',
                             'Default':'arn:aws:sqs:us-east-2:444455556666:queue1'
                          }
                       }
                    }";

            const string yamlContent = @"Parameters:
                                          MyQueue:
                                            Type: String
                                            Default: arn:aws:sqs:us-east-2:444455556666:queue1";

            ITemplateWriter templateWriter = templateFormat == CloudFormationTemplateFormat.Json ? new JsonWriter() : new YamlWriter();
            var content = templateFormat == CloudFormationTemplateFormat.Json ? jsonContent : yamlContent;

            var mockFileManager = GetMockFileManager(content);
            var lambdaFunctionModel = GetLambdaFunctionModel();
            var eventResourceName = "MySQSEvent";
            var sqsEventAttribute = new SQSEventAttribute("@MyQueue") { ResourceName = eventResourceName };
            lambdaFunctionModel.Attributes = [new AttributeModel<SQSEventAttribute> { Data = sqsEventAttribute }];
            var cloudFormationWriter = GetCloudFormationWriter(mockFileManager, _directoryManager, templateFormat, _diagnosticReporter);
            var report = GetAnnotationReport([lambdaFunctionModel]);

            var sqsEventPropertiesPath = $"Resources.{lambdaFunctionModel.ResourceName}.Properties.Events.{eventResourceName}.Properties";
            var syncedEventPropertiesPath = $"Resources.{lambdaFunctionModel.ResourceName}.Metadata.SyncedEventProperties";

            // ACT
            cloudFormationWriter.ApplyReport(report);

            // Verify Queue property must exists as a ref
            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));
            Assert.Equal("MyQueue", templateWriter.GetToken<string>($"{sqsEventPropertiesPath}.Queue.Ref"));
            Assert.False(templateWriter.Exists($"{sqsEventPropertiesPath}.Queue.Fn::GetAtt"));

            // Verify the list of synced event properties
            var syncedEventProperties = templateWriter.GetToken<Dictionary<string, List<string>>>($"{syncedEventPropertiesPath}");
            Assert.Single(syncedEventProperties[eventResourceName]);
            Assert.Equal("Queue.Ref", syncedEventProperties[eventResourceName][0]);

            // Change the Queue property to be an ARN and re-generate the template
            sqsEventAttribute.Queue = queueArn1;
            lambdaFunctionModel.Attributes = [new AttributeModel<SQSEventAttribute> { Data = sqsEventAttribute }];
            report = GetAnnotationReport([lambdaFunctionModel]);
            cloudFormationWriter.ApplyReport(report);

            // Verify Queue property must exists as a ARN
            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));
            Assert.Equal(queueArn1, templateWriter.GetToken<string>($"{sqsEventPropertiesPath}.Queue"));
            Assert.False(templateWriter.Exists($"{sqsEventPropertiesPath}.Queue.Fn::GetAtt"));
            Assert.False(templateWriter.Exists($"{sqsEventPropertiesPath}.Queue.Ref"));

            // Verify the list of synced event properties
            syncedEventProperties = templateWriter.GetToken<Dictionary<string, List<string>>>($"{syncedEventPropertiesPath}");
            Assert.Single(syncedEventProperties[eventResourceName]);
            Assert.Equal("Queue", syncedEventProperties[eventResourceName][0]);
        }

        /// <summary>
        /// This class provides the test data for <see cref="VerifySQSEventAttributes_AreCorrectlyApplied(CloudFormationTemplateFormat, IEnumerable{SQSEventAttribute}, string)"/>
        /// </summary>
        public class SqsEventsTestData : TheoryData<CloudFormationTemplateFormat, IEnumerable<SQSEventAttribute>, string>
        {
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
                    Add(templateFormat, [new(queueArn1) { Filters = "SOME-FILTER1; SOME-FILTER2" },], "void");

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
    }
}
