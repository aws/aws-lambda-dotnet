using Amazon.Lambda.Annotations.SourceGenerator;
using Amazon.Lambda.Annotations.SourceGenerator.Models;
using Amazon.Lambda.Annotations.SourceGenerator.Models.Attributes;
using Amazon.Lambda.Annotations.SourceGenerator.Writers;
using Amazon.Lambda.Annotations.S3;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Amazon.Lambda.Annotations.SourceGenerators.Tests.WriterTests
{
    public partial class CloudFormationWriterTests
    {
        [Theory]
        [ClassData(typeof(S3EventsTestData))]
        public void VerifyS3EventAttributes_AreCorrectlyApplied(CloudFormationTemplateFormat templateFormat, S3EventAttribute s3EventAttribute)
        {
            // ARRANGE
            var mockFileManager = GetMockFileManager(string.Empty);
            var lambdaFunctionModel = GetLambdaFunctionModel();
            lambdaFunctionModel.PackageType = LambdaPackageType.Zip;
            lambdaFunctionModel.Attributes.Add(new AttributeModel<S3EventAttribute> { Data = s3EventAttribute });
            var cloudFormationWriter = GetCloudFormationWriter(mockFileManager, _directoryManager, templateFormat, _diagnosticReporter);
            var report = GetAnnotationReport([lambdaFunctionModel]);

            // ACT
            cloudFormationWriter.ApplyReport(report);

            // ASSERT
            ITemplateWriter templateWriter = templateFormat == CloudFormationTemplateFormat.Json ? new JsonWriter() : new YamlWriter();
            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));

            var eventName = s3EventAttribute.ResourceName;
            var eventPath = $"Resources.{lambdaFunctionModel.ResourceName}.Properties.Events.{eventName}";
            var eventPropertiesPath = $"{eventPath}.Properties";

            Assert.True(templateWriter.Exists(eventPath));
            Assert.Equal("S3", templateWriter.GetToken<string>($"{eventPath}.Type"));

            // Bucket is always a Ref
            var bucketName = s3EventAttribute.Bucket.StartsWith("@") ? s3EventAttribute.Bucket.Substring(1) : s3EventAttribute.Bucket;
            Assert.Equal(bucketName, templateWriter.GetToken<string>($"{eventPropertiesPath}.Bucket.Ref"));

            // Events - always written (uses default "s3:ObjectCreated:*" when not explicitly set)
            {
                var expectedEvents = s3EventAttribute.Events.Split(';').Select(x => x.Trim()).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                Assert.Equal(expectedEvents, templateWriter.GetToken<List<string>>($"{eventPropertiesPath}.Events"));
            }

            // Filter
            if (s3EventAttribute.IsFilterPrefixSet || s3EventAttribute.IsFilterSuffixSet)
            {
                Assert.True(templateWriter.Exists($"{eventPropertiesPath}.Filter.S3Key.Rules"));
                var rules = templateWriter.GetToken<List<Dictionary<string, string>>>($"{eventPropertiesPath}.Filter.S3Key.Rules");
                if (s3EventAttribute.IsFilterPrefixSet)
                {
                    Assert.Contains(rules, r => r["Name"] == "prefix" && r["Value"] == s3EventAttribute.FilterPrefix);
                }
                if (s3EventAttribute.IsFilterSuffixSet)
                {
                    Assert.Contains(rules, r => r["Name"] == "suffix" && r["Value"] == s3EventAttribute.FilterSuffix);
                }
            }
            else
            {
                Assert.False(templateWriter.Exists($"{eventPropertiesPath}.Filter"));
            }

            // Enabled
            Assert.Equal(s3EventAttribute.IsEnabledSet, templateWriter.Exists($"{eventPropertiesPath}.Enabled"));
            if (s3EventAttribute.IsEnabledSet)
            {
                Assert.Equal(s3EventAttribute.Enabled, templateWriter.GetToken<bool>($"{eventPropertiesPath}.Enabled"));
            }
        }

        [Theory]
        [InlineData(CloudFormationTemplateFormat.Json)]
        [InlineData(CloudFormationTemplateFormat.Yaml)]
        public void VerifyS3EventProperties_AreSyncedCorrectly(CloudFormationTemplateFormat templateFormat)
        {
            // ARRANGE
            var mockFileManager = GetMockFileManager(string.Empty);
            var lambdaFunctionModel = GetLambdaFunctionModel();
            lambdaFunctionModel.PackageType = LambdaPackageType.Zip;
            var eventResourceName = "MyBucket";
            var eventPropertiesPath = $"Resources.{lambdaFunctionModel.ResourceName}.Properties.Events.{eventResourceName}.Properties";
            var syncedEventPropertiesPath = $"Resources.{lambdaFunctionModel.ResourceName}.Metadata.SyncedEventProperties";

            // Initial attribute with filters
            var initialAttribute = new S3EventAttribute("@MyBucket")
            {
                FilterPrefix = "uploads/",
                FilterSuffix = ".jpg"
            };
            lambdaFunctionModel.Attributes = [new AttributeModel<S3EventAttribute> { Data = initialAttribute }];
            var cloudFormationWriter = GetCloudFormationWriter(mockFileManager, _directoryManager, templateFormat, _diagnosticReporter);
            var report = GetAnnotationReport([lambdaFunctionModel]);

            // ACT
            cloudFormationWriter.ApplyReport(report);

            // Assert initial properties
            ITemplateWriter templateWriter = templateFormat == CloudFormationTemplateFormat.Json ? new JsonWriter() : new YamlWriter();
            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));

            Assert.Equal("MyBucket", templateWriter.GetToken<string>($"{eventPropertiesPath}.Bucket.Ref"));
            Assert.True(templateWriter.Exists($"{eventPropertiesPath}.Filter.S3Key.Rules"));
            Assert.True(templateWriter.Exists($"{eventPropertiesPath}.Events"));

            // Update attribute - remove filters, add Enabled=false
            var updatedAttribute = new S3EventAttribute("@MyBucket")
            {
                Enabled = false
            };
            lambdaFunctionModel.Attributes = [new AttributeModel<S3EventAttribute> { Data = updatedAttribute }];
            report = GetAnnotationReport([lambdaFunctionModel]);

            cloudFormationWriter.ApplyReport(report);

            // Assert updated properties
            templateWriter.Parse(mockFileManager.ReadAllText(ServerlessTemplateFilePath));
            Assert.Equal("MyBucket", templateWriter.GetToken<string>($"{eventPropertiesPath}.Bucket.Ref"));
            Assert.False(templateWriter.Exists($"{eventPropertiesPath}.Filter"));
            Assert.False(templateWriter.GetToken<bool>($"{eventPropertiesPath}.Enabled"));
        }

        public class S3EventsTestData : TheoryData<CloudFormationTemplateFormat, S3EventAttribute>
        {
            public S3EventsTestData()
            {
                foreach (var templateFormat in new List<CloudFormationTemplateFormat> { CloudFormationTemplateFormat.Json, CloudFormationTemplateFormat.Yaml })
                {
                    // Simple - default events
                    Add(templateFormat, new S3EventAttribute("@MyBucket"));

                    // With custom events
                    Add(templateFormat, new S3EventAttribute("@MyBucket") { Events = "s3:ObjectCreated:*;s3:ObjectRemoved:*" });

                    // With filters
                    Add(templateFormat, new S3EventAttribute("@MyBucket") { FilterPrefix = "uploads/", FilterSuffix = ".jpg" });

                    // With prefix only
                    Add(templateFormat, new S3EventAttribute("@MyBucket") { FilterPrefix = "logs/" });

                    // With suffix only
                    Add(templateFormat, new S3EventAttribute("@MyBucket") { FilterSuffix = ".png" });

                    // With custom resource name and disabled
                    Add(templateFormat, new S3EventAttribute("@ImageBucket") { ResourceName = "ImageBucketEvent", Enabled = false });

                    // All properties
                    Add(templateFormat, new S3EventAttribute("@MyBucket")
                    {
                        ResourceName = "FullS3Event",
                        Events = "s3:ObjectCreated:Put",
                        FilterPrefix = "data/",
                        FilterSuffix = ".csv",
                        Enabled = true
                    });
                }
            }
        }
    }
}
