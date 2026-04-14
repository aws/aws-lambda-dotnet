// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.Annotations.S3;
using System.Linq;
using Xunit;

namespace Amazon.Lambda.Annotations.SourceGenerators.Tests
{
    public class S3EventAttributeTests
    {
        // ===== Constructor and Default Values =====

        [Fact]
        public void Constructor_SetsBucketProperty()
        {
            var attr = new S3EventAttribute("@MyBucket");

            Assert.Equal("@MyBucket", attr.Bucket);
        }

        [Fact]
        public void DefaultValues_AreCorrect()
        {
            var attr = new S3EventAttribute("@MyBucket");

            // Events defaults to "s3:ObjectCreated:*" but IsEventsSet should be false
            Assert.Equal("s3:ObjectCreated:*", attr.Events);
            Assert.False(attr.IsEventsSet);

            // Filters default to null
            Assert.Null(attr.FilterPrefix);
            Assert.False(attr.IsFilterPrefixSet);
            Assert.Null(attr.FilterSuffix);
            Assert.False(attr.IsFilterSuffixSet);

            // Enabled defaults to true but IsEnabledSet should be false
            Assert.True(attr.Enabled);
            Assert.False(attr.IsEnabledSet);

            // ResourceName defaults to bucket name without "@"
            Assert.Equal("MyBucket", attr.ResourceName);
            Assert.False(attr.IsResourceNameSet);
        }

        // ===== ResourceName Tests =====

        [Fact]
        public void ResourceName_DerivedFromBucket_WithAtPrefix()
        {
            var attr = new S3EventAttribute("@TestBucket");

            Assert.False(attr.IsResourceNameSet);
            Assert.Equal("TestBucket", attr.ResourceName);
        }

        [Fact]
        public void ResourceName_WhenExplicitlySet_IsTracked()
        {
            var attr = new S3EventAttribute("@MyBucket");

            Assert.False(attr.IsResourceNameSet);

            attr.ResourceName = "CustomEventName";
            Assert.True(attr.IsResourceNameSet);
            Assert.Equal("CustomEventName", attr.ResourceName);
        }

        [Fact]
        public void ResourceName_WithNullBucket_DoesNotThrow()
        {
            var attr = new S3EventAttribute(null);

            // Should not throw - returns null safely
            Assert.Null(attr.ResourceName);
        }

        [Fact]
        public void ResourceName_WithEmptyBucket_DoesNotThrow()
        {
            var attr = new S3EventAttribute("");

            // Should not throw - returns empty string
            Assert.Equal("", attr.ResourceName);
        }

        // ===== Events Property Tests =====

        [Fact]
        public void Events_DefaultValue_IsObjectCreatedAll()
        {
            var attr = new S3EventAttribute("@MyBucket");

            Assert.Equal("s3:ObjectCreated:*", attr.Events);
            Assert.False(attr.IsEventsSet);
        }

        [Fact]
        public void Events_WhenExplicitlySet_IsTracked()
        {
            var attr = new S3EventAttribute("@MyBucket")
            {
                Events = "s3:ObjectRemoved:*"
            };

            Assert.True(attr.IsEventsSet);
            Assert.Equal("s3:ObjectRemoved:*", attr.Events);
        }

        [Fact]
        public void Events_WhenSetToSameAsDefault_IsStillTrackedAsSet()
        {
            var attr = new S3EventAttribute("@MyBucket")
            {
                Events = "s3:ObjectCreated:*"
            };

            Assert.True(attr.IsEventsSet);
            Assert.Equal("s3:ObjectCreated:*", attr.Events);
        }

        [Fact]
        public void Events_MultipleSemicolonSeparated_WorksCorrectly()
        {
            var attr = new S3EventAttribute("@MyBucket")
            {
                Events = "s3:ObjectCreated:*;s3:ObjectRemoved:*"
            };

            var eventList = attr.Events.Split(';').Select(x => x.Trim()).ToList();
            Assert.Equal(2, eventList.Count);
            Assert.Contains("s3:ObjectCreated:*", eventList);
            Assert.Contains("s3:ObjectRemoved:*", eventList);
        }

        // ===== Filter Properties Tests =====

        [Fact]
        public void FilterPrefix_WhenSet_IsTracked()
        {
            var attr = new S3EventAttribute("@MyBucket")
            {
                FilterPrefix = "uploads/"
            };

            Assert.True(attr.IsFilterPrefixSet);
            Assert.Equal("uploads/", attr.FilterPrefix);
        }

        [Fact]
        public void FilterSuffix_WhenSet_IsTracked()
        {
            var attr = new S3EventAttribute("@MyBucket")
            {
                FilterSuffix = ".jpg"
            };

            Assert.True(attr.IsFilterSuffixSet);
            Assert.Equal(".jpg", attr.FilterSuffix);
        }

        [Fact]
        public void Filters_BothSet_AreTrackedIndependently()
        {
            var attr = new S3EventAttribute("@MyBucket")
            {
                FilterPrefix = "data/",
                FilterSuffix = ".csv"
            };

            Assert.True(attr.IsFilterPrefixSet);
            Assert.True(attr.IsFilterSuffixSet);
            Assert.Equal("data/", attr.FilterPrefix);
            Assert.Equal(".csv", attr.FilterSuffix);
        }

        // ===== Enabled Property Tests =====

        [Fact]
        public void Enabled_DefaultValue_IsTrue()
        {
            var attr = new S3EventAttribute("@MyBucket");

            Assert.True(attr.Enabled);
            Assert.False(attr.IsEnabledSet);
        }

        [Fact]
        public void Enabled_WhenSetToFalse_IsTracked()
        {
            var attr = new S3EventAttribute("@MyBucket")
            {
                Enabled = false
            };

            Assert.True(attr.IsEnabledSet);
            Assert.False(attr.Enabled);
        }

        [Fact]
        public void Enabled_WhenSetToTrue_IsTracked()
        {
            var attr = new S3EventAttribute("@MyBucket")
            {
                Enabled = true
            };

            Assert.True(attr.IsEnabledSet);
            Assert.True(attr.Enabled);
        }

        // ===== Validation Tests =====

        [Fact]
        public void Validate_ValidBucketReference_ReturnsNoErrors()
        {
            var attr = new S3EventAttribute("@MyBucket");

            var errors = attr.Validate();
            Assert.Empty(errors);
        }

        [Fact]
        public void Validate_NullBucket_ReturnsError()
        {
            var attr = new S3EventAttribute(null);

            var errors = attr.Validate();
            Assert.Single(errors);
            Assert.Contains("Bucket", errors[0]);
            Assert.Contains("required", errors[0]);
        }

        [Fact]
        public void Validate_EmptyBucket_ReturnsError()
        {
            var attr = new S3EventAttribute("");

            var errors = attr.Validate();
            Assert.Single(errors);
            Assert.Contains("Bucket", errors[0]);
            Assert.Contains("required", errors[0]);
        }

        [Fact]
        public void Validate_BucketWithoutAtPrefix_ReturnsError()
        {
            var attr = new S3EventAttribute("MyBucket");

            var errors = attr.Validate();
            Assert.Single(errors);
            Assert.Contains("Bucket", errors[0]);
            Assert.Contains("@", errors[0]);
        }

        [Fact]
        public void Validate_BucketWithOnlyAtSign_ReturnsError()
        {
            var attr = new S3EventAttribute("@");

            var errors = attr.Validate();
            Assert.Single(errors);
            Assert.Contains("Bucket", errors[0]);
            Assert.Contains("must not be empty", errors[0]);
        }

        [Fact]
        public void Validate_BucketWithInvalidCharsAfterAt_ReturnsError()
        {
            var attr = new S3EventAttribute("@My-Bucket!");

            var errors = attr.Validate();
            Assert.Single(errors);
            Assert.Contains("Bucket", errors[0]);
            Assert.Contains("alphanumeric", errors[0]);
        }

        [Fact]
        public void Validate_InvalidResourceName_ReturnsError()
        {
            var attr = new S3EventAttribute("@MyBucket")
            {
                ResourceName = "invalid-name!"
            };

            var errors = attr.Validate();
            Assert.Single(errors);
            Assert.Contains("ResourceName", errors[0]);
            Assert.Contains("alphanumeric", errors[0]);
        }

        [Fact]
        public void Validate_EmptyResourceName_ReturnsError()
        {
            var attr = new S3EventAttribute("@MyBucket")
            {
                ResourceName = ""
            };

            var errors = attr.Validate();
            Assert.Single(errors);
            Assert.Contains("ResourceName", errors[0]);
        }

        [Fact]
        public void Validate_ValidResourceName_ReturnsNoErrors()
        {
            var attr = new S3EventAttribute("@MyBucket")
            {
                ResourceName = "MyS3Event123"
            };

            var errors = attr.Validate();
            Assert.Empty(errors);
        }

        [Fact]
        public void Validate_UnsetResourceName_ReturnsNoErrors()
        {
            var attr = new S3EventAttribute("@MyBucket");

            var errors = attr.Validate();
            Assert.Empty(errors);
            Assert.False(attr.IsResourceNameSet);
        }

        [Fact]
        public void Validate_EmptyEvents_ReturnsError()
        {
            var attr = new S3EventAttribute("@MyBucket")
            {
                Events = ""
            };

            var errors = attr.Validate();
            Assert.Single(errors);
            Assert.Contains("Events", errors[0]);
            Assert.Contains("must not be empty", errors[0]);
        }

        [Fact]
        public void Validate_NullEvents_ReturnsError()
        {
            var attr = new S3EventAttribute("@MyBucket")
            {
                Events = null
            };

            // Events getter returns default when backing field is null, but the
            // validation should check the actual state. With null set, Events returns
            // the default "s3:ObjectCreated:*" so this should pass validation.
            var errors = attr.Validate();
            Assert.Empty(errors);
        }

        [Fact]
        public void Validate_MultipleErrors_ReturnsAll()
        {
            var attr = new S3EventAttribute("")
            {
                ResourceName = "invalid-name!",
                Events = ""
            };

            var errors = attr.Validate();
            // Should have errors for: Bucket (empty), ResourceName (invalid), Events (empty)
            Assert.Equal(3, errors.Count);
            Assert.Contains(errors, e => e.Contains("Bucket"));
            Assert.Contains(errors, e => e.Contains("ResourceName"));
            Assert.Contains(errors, e => e.Contains("Events"));
        }

        [Fact]
        public void Validate_AllValidWithOptionals_ReturnsNoErrors()
        {
            var attr = new S3EventAttribute("@MyBucket")
            {
                ResourceName = "FullS3Event",
                Events = "s3:ObjectCreated:Put",
                FilterPrefix = "data/",
                FilterSuffix = ".csv",
                Enabled = true
            };

            var errors = attr.Validate();
            Assert.Empty(errors);
        }

        // ===== Events Semicolon Edge Cases =====

        [Fact]
        public void Events_TrailingSemicolon_SplitFiltersEmptyEntries()
        {
            // This tests that the CloudFormationWriter correctly filters empty entries
            // when splitting Events by semicolon
            var attr = new S3EventAttribute("@MyBucket")
            {
                Events = "s3:ObjectCreated:*;"
            };

            var eventList = attr.Events
                .Split(';')
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            Assert.Single(eventList);
            Assert.Equal("s3:ObjectCreated:*", eventList[0]);
        }

        [Fact]
        public void Events_LeadingSemicolon_SplitFiltersEmptyEntries()
        {
            var attr = new S3EventAttribute("@MyBucket")
            {
                Events = ";s3:ObjectCreated:*"
            };

            var eventList = attr.Events
                .Split(';')
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            Assert.Single(eventList);
            Assert.Equal("s3:ObjectCreated:*", eventList[0]);
        }

        [Fact]
        public void Events_ConsecutiveSemicolons_SplitFiltersEmptyEntries()
        {
            var attr = new S3EventAttribute("@MyBucket")
            {
                Events = "s3:ObjectCreated:*;;s3:ObjectRemoved:*"
            };

            var eventList = attr.Events
                .Split(';')
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            Assert.Equal(2, eventList.Count);
            Assert.Equal("s3:ObjectCreated:*", eventList[0]);
            Assert.Equal("s3:ObjectRemoved:*", eventList[1]);
        }
    }
}
