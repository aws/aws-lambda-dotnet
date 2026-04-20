// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.Annotations.DynamoDB;
using Xunit;

namespace Amazon.Lambda.Annotations.SourceGenerators.Tests
{
    public class DynamoDBEventAttributeTests
    {
        // ===== Constructor and Default Values =====

        [Fact]
        public void Constructor_SetsStreamProperty()
        {
            var attr = new DynamoDBEventAttribute("@MyTable");

            Assert.Equal("@MyTable", attr.Stream);
        }

        [Fact]
        public void DefaultValues_AreCorrect()
        {
            var attr = new DynamoDBEventAttribute("@MyTable");

            Assert.Equal(StartingPosition.LATEST, attr.StartingPosition);
            Assert.False(attr.IsBatchSizeSet);
            Assert.False(attr.IsEnabledSet);
            Assert.False(attr.IsMaximumBatchingWindowInSecondsSet);
            Assert.Null(attr.Filters);
            Assert.False(attr.IsFiltersSet);
            Assert.False(attr.IsResourceNameSet);
            Assert.Equal("MyTable", attr.ResourceName);
        }

        // ===== ResourceName Tests =====

        [Fact]
        public void ResourceName_DerivedFromStream_WithAtPrefix()
        {
            var attr = new DynamoDBEventAttribute("@TestTable");

            Assert.False(attr.IsResourceNameSet);
            Assert.Equal("TestTable", attr.ResourceName);
        }

        [Fact]
        public void ResourceName_DerivedFromStreamArn()
        {
            var attr = new DynamoDBEventAttribute("arn:aws:dynamodb:us-east-1:123456789012:table/MyTable/stream/2024-01-01T00:00:00.000");

            Assert.False(attr.IsResourceNameSet);
            Assert.Equal("MyTable", attr.ResourceName);
        }

        [Fact]
        public void ResourceName_WhenExplicitlySet_IsTracked()
        {
            var attr = new DynamoDBEventAttribute("@MyTable");

            Assert.False(attr.IsResourceNameSet);

            attr.ResourceName = "CustomEventName";
            Assert.True(attr.IsResourceNameSet);
            Assert.Equal("CustomEventName", attr.ResourceName);
        }

        // ===== BatchSize Tests =====

        [Fact]
        public void BatchSize_DefaultNotSet()
        {
            var attr = new DynamoDBEventAttribute("@MyTable");

            Assert.False(attr.IsBatchSizeSet);
        }

        [Fact]
        public void BatchSize_WhenSet_IsTracked()
        {
            var attr = new DynamoDBEventAttribute("@MyTable")
            {
                BatchSize = 50
            };

            Assert.True(attr.IsBatchSizeSet);
            Assert.Equal((uint)50, attr.BatchSize);
        }

        // ===== StartingPosition Tests =====

        [Fact]
        public void StartingPosition_DefaultValue_IsLatest()
        {
            var attr = new DynamoDBEventAttribute("@MyTable");

            Assert.Equal(StartingPosition.LATEST, attr.StartingPosition);
        }

        [Fact]
        public void StartingPosition_CanBeSetToTrimHorizon()
        {
            var attr = new DynamoDBEventAttribute("@MyTable")
            {
                StartingPosition = StartingPosition.TRIM_HORIZON
            };

            Assert.Equal(StartingPosition.TRIM_HORIZON, attr.StartingPosition);
        }

        // ===== MaximumBatchingWindowInSeconds Tests =====

        [Fact]
        public void MaximumBatchingWindowInSeconds_DefaultNotSet()
        {
            var attr = new DynamoDBEventAttribute("@MyTable");

            Assert.False(attr.IsMaximumBatchingWindowInSecondsSet);
        }

        [Fact]
        public void MaximumBatchingWindowInSeconds_WhenSet_IsTracked()
        {
            var attr = new DynamoDBEventAttribute("@MyTable")
            {
                MaximumBatchingWindowInSeconds = 60
            };

            Assert.True(attr.IsMaximumBatchingWindowInSecondsSet);
            Assert.Equal((uint)60, attr.MaximumBatchingWindowInSeconds);
        }

        // ===== Enabled Property Tests =====

        [Fact]
        public void Enabled_DefaultNotSet()
        {
            var attr = new DynamoDBEventAttribute("@MyTable");

            Assert.False(attr.IsEnabledSet);
        }

        [Fact]
        public void Enabled_WhenSetToFalse_IsTracked()
        {
            var attr = new DynamoDBEventAttribute("@MyTable")
            {
                Enabled = false
            };

            Assert.True(attr.IsEnabledSet);
            Assert.False(attr.Enabled);
        }

        [Fact]
        public void Enabled_WhenSetToTrue_IsTracked()
        {
            var attr = new DynamoDBEventAttribute("@MyTable")
            {
                Enabled = true
            };

            Assert.True(attr.IsEnabledSet);
            Assert.True(attr.Enabled);
        }

        // ===== Filters Tests =====

        [Fact]
        public void Filters_DefaultIsNull()
        {
            var attr = new DynamoDBEventAttribute("@MyTable");

            Assert.Null(attr.Filters);
            Assert.False(attr.IsFiltersSet);
        }

        [Fact]
        public void Filters_WhenSet_IsTracked()
        {
            var attr = new DynamoDBEventAttribute("@MyTable")
            {
                Filters = "{\"eventName\": [\"INSERT\"]}"
            };

            Assert.True(attr.IsFiltersSet);
            Assert.Equal("{\"eventName\": [\"INSERT\"]}", attr.Filters);
        }

        // ===== Validation Tests =====

        [Fact]
        public void Validate_ValidResourceReference_ReturnsNoErrors()
        {
            var attr = new DynamoDBEventAttribute("@MyTable");

            var errors = attr.Validate();
            Assert.Empty(errors);
        }

        [Fact]
        public void Validate_ValidStreamArn_ReturnsNoErrors()
        {
            var attr = new DynamoDBEventAttribute("arn:aws:dynamodb:us-east-1:123456789012:table/MyTable/stream/2024-01-01T00:00:00.000");

            var errors = attr.Validate();
            Assert.Empty(errors);
        }

        [Fact]
        public void Validate_InvalidStreamArn_ReturnsError()
        {
            var attr = new DynamoDBEventAttribute("not-a-valid-arn");

            var errors = attr.Validate();
            Assert.Single(errors);
            Assert.Contains("Stream", errors[0]);
        }

        [Fact]
        public void Validate_InvalidResourceName_ReturnsError()
        {
            var attr = new DynamoDBEventAttribute("@MyTable")
            {
                ResourceName = "invalid-name!"
            };

            var errors = attr.Validate();
            Assert.Single(errors);
            Assert.Contains("ResourceName", errors[0]);
            Assert.Contains("alphanumeric", errors[0]);
        }

        [Fact]
        public void Validate_BatchSizeTooLarge_ReturnsError()
        {
            var attr = new DynamoDBEventAttribute("@MyTable")
            {
                BatchSize = 10001
            };

            var errors = attr.Validate();
            Assert.Single(errors);
            Assert.Contains("BatchSize", errors[0]);
        }

        [Fact]
        public void Validate_MaxBatchingWindowTooLarge_ReturnsError()
        {
            var attr = new DynamoDBEventAttribute("@MyTable")
            {
                MaximumBatchingWindowInSeconds = 301
            };

            var errors = attr.Validate();
            Assert.Single(errors);
            Assert.Contains("MaximumBatchingWindowInSeconds", errors[0]);
        }


        [Fact]
        public void Validate_AtSignOnly_ReturnsError()
        {
            var attr = new DynamoDBEventAttribute("@");

            var errors = attr.Validate();
            Assert.Single(errors);
            Assert.Contains("Stream", errors[0]);
            Assert.Contains("'@' prefix must be followed by a non-empty resource or parameter name", errors[0]);
        }

        [Fact]
        public void Validate_AtSignWithWhitespace_ReturnsError()
        {
            var attr = new DynamoDBEventAttribute("@   ");

            var errors = attr.Validate();
            Assert.Single(errors);
            Assert.Contains("Stream", errors[0]);
            Assert.Contains("'@' prefix must be followed by a non-empty resource or parameter name", errors[0]);
        }

        [Fact]
        public void Validate_MultipleErrors_ReturnsAll()
        {
            var attr = new DynamoDBEventAttribute("not-valid")
            {
                ResourceName = "invalid!",
                BatchSize = 10001
            };

            var errors = attr.Validate();
            Assert.Equal(3, errors.Count);
        }

        [Fact]
        public void Validate_AllValidWithOptionals_ReturnsNoErrors()
        {
            var attr = new DynamoDBEventAttribute("@MyTable")
            {
                ResourceName = "MyDynamoDBEvent",
                BatchSize = 100,
                StartingPosition = StartingPosition.TRIM_HORIZON,
                MaximumBatchingWindowInSeconds = 60,
                Filters = "{\"eventName\": [\"INSERT\"]}",
                Enabled = true
            };

            var errors = attr.Validate();
            Assert.Empty(errors);
        }
    }
}
