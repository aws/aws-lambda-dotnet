// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.Annotations.SQS;
using Xunit;

namespace Amazon.Lambda.Annotations.SourceGenerators.Tests
{
    public class SQSEventAttributeTests
    {
        // ===== Constructor and Default Values =====

        [Fact]
        public void Constructor_SetsQueueProperty()
        {
            var attr = new SQSEventAttribute("@MyQueue");

            Assert.Equal("@MyQueue", attr.Queue);
        }

        [Fact]
        public void DefaultValues_AreCorrect()
        {
            var attr = new SQSEventAttribute("@MyQueue");

            Assert.False(attr.IsBatchSizeSet);
            Assert.False(attr.IsEnabledSet);
            Assert.False(attr.IsMaximumBatchingWindowInSecondsSet);
            Assert.False(attr.IsMaximumConcurrencySet);
            Assert.Null(attr.Filters);
            Assert.False(attr.IsFiltersSet);
            Assert.False(attr.IsResourceNameSet);
            Assert.Equal("MyQueue", attr.ResourceName);
        }

        // ===== ResourceName Tests =====

        [Fact]
        public void ResourceName_DerivedFromQueue_WithAtPrefix()
        {
            var attr = new SQSEventAttribute("@TestQueue");

            Assert.False(attr.IsResourceNameSet);
            Assert.Equal("TestQueue", attr.ResourceName);
        }

        [Fact]
        public void ResourceName_DerivedFromQueueArn()
        {
            var attr = new SQSEventAttribute("arn:aws:sqs:us-east-1:123456789012:MyQueue");

            Assert.False(attr.IsResourceNameSet);
            Assert.Equal("MyQueue", attr.ResourceName);
        }

        [Fact]
        public void ResourceName_WhenExplicitlySet_IsTracked()
        {
            var attr = new SQSEventAttribute("@MyQueue");

            Assert.False(attr.IsResourceNameSet);

            attr.ResourceName = "CustomEventName";
            Assert.True(attr.IsResourceNameSet);
            Assert.Equal("CustomEventName", attr.ResourceName);
        }

        // ===== BatchSize Tests =====

        [Fact]
        public void BatchSize_DefaultNotSet()
        {
            var attr = new SQSEventAttribute("@MyQueue");

            Assert.False(attr.IsBatchSizeSet);
        }

        [Fact]
        public void BatchSize_WhenSet_IsTracked()
        {
            var attr = new SQSEventAttribute("@MyQueue")
            {
                BatchSize = 50
            };

            Assert.True(attr.IsBatchSizeSet);
            Assert.Equal((uint)50, attr.BatchSize);
        }

        // ===== MaximumBatchingWindowInSeconds Tests =====

        [Fact]
        public void MaximumBatchingWindowInSeconds_DefaultNotSet()
        {
            var attr = new SQSEventAttribute("@MyQueue");

            Assert.False(attr.IsMaximumBatchingWindowInSecondsSet);
        }

        [Fact]
        public void MaximumBatchingWindowInSeconds_WhenSet_IsTracked()
        {
            var attr = new SQSEventAttribute("@MyQueue")
            {
                MaximumBatchingWindowInSeconds = 60
            };

            Assert.True(attr.IsMaximumBatchingWindowInSecondsSet);
            Assert.Equal((uint)60, attr.MaximumBatchingWindowInSeconds);
        }

        // ===== MaximumConcurrency Tests =====

        [Fact]
        public void MaximumConcurrency_DefaultNotSet()
        {
            var attr = new SQSEventAttribute("@MyQueue");

            Assert.False(attr.IsMaximumConcurrencySet);
        }

        [Fact]
        public void MaximumConcurrency_WhenSet_IsTracked()
        {
            var attr = new SQSEventAttribute("@MyQueue")
            {
                MaximumConcurrency = 10
            };

            Assert.True(attr.IsMaximumConcurrencySet);
            Assert.Equal((uint)10, attr.MaximumConcurrency);
        }

        // ===== Enabled Property Tests =====

        [Fact]
        public void Enabled_DefaultNotSet()
        {
            var attr = new SQSEventAttribute("@MyQueue");

            Assert.False(attr.IsEnabledSet);
        }

        [Fact]
        public void Enabled_WhenSetToFalse_IsTracked()
        {
            var attr = new SQSEventAttribute("@MyQueue")
            {
                Enabled = false
            };

            Assert.True(attr.IsEnabledSet);
            Assert.False(attr.Enabled);
        }

        [Fact]
        public void Enabled_WhenSetToTrue_IsTracked()
        {
            var attr = new SQSEventAttribute("@MyQueue")
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
            var attr = new SQSEventAttribute("@MyQueue");

            Assert.Null(attr.Filters);
            Assert.False(attr.IsFiltersSet);
        }

        [Fact]
        public void Filters_WhenSet_IsTracked()
        {
            var attr = new SQSEventAttribute("@MyQueue")
            {
                Filters = "{ \"body\" : { \"RequestCode\" : [ \"BBBB\" ] } }"
            };

            Assert.True(attr.IsFiltersSet);
            Assert.Equal("{ \"body\" : { \"RequestCode\" : [ \"BBBB\" ] } }", attr.Filters);
        }

        // ===== Validation Tests =====

        [Fact]
        public void Validate_ValidResourceReference_ReturnsNoErrors()
        {
            var attr = new SQSEventAttribute("@MyQueue");

            var errors = attr.Validate();
            Assert.Empty(errors);
        }

        [Fact]
        public void Validate_ValidQueueArn_ReturnsNoErrors()
        {
            var attr = new SQSEventAttribute("arn:aws:sqs:us-east-1:123456789012:MyQueue");

            var errors = attr.Validate();
            Assert.Empty(errors);
        }

        [Fact]
        public void Validate_InvalidQueueArn_ReturnsError()
        {
            var attr = new SQSEventAttribute("not-a-valid-arn");

            var errors = attr.Validate();
            Assert.Single(errors);
            Assert.Contains("Queue", errors[0]);
            Assert.Contains("ARN", errors[0]);
        }

        [Fact]
        public void Validate_InvalidResourceName_ReturnsError()
        {
            var attr = new SQSEventAttribute("@MyQueue")
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
            var attr = new SQSEventAttribute("@MyQueue")
            {
                BatchSize = 10001
            };

            var errors = attr.Validate();
            Assert.Contains(errors, e => e.Contains("BatchSize"));
        }

        [Fact]
        public void Validate_BatchSizeZero_ReturnsError()
        {
            var attr = new SQSEventAttribute("@MyQueue")
            {
                BatchSize = 0
            };

            var errors = attr.Validate();
            Assert.Contains(errors, e => e.Contains("BatchSize"));
        }

        [Fact]
        public void Validate_MaxBatchingWindowTooLarge_ReturnsError()
        {
            var attr = new SQSEventAttribute("@MyQueue")
            {
                MaximumBatchingWindowInSeconds = 301
            };

            var errors = attr.Validate();
            Assert.Single(errors);
            Assert.Contains("MaximumBatchingWindowInSeconds", errors[0]);
        }

        [Fact]
        public void Validate_MaximumConcurrencyTooLow_ReturnsError()
        {
            var attr = new SQSEventAttribute("@MyQueue")
            {
                MaximumConcurrency = 1
            };

            var errors = attr.Validate();
            Assert.Single(errors);
            Assert.Contains("MaximumConcurrency", errors[0]);
        }

        [Fact]
        public void Validate_MaximumConcurrencyTooHigh_ReturnsError()
        {
            var attr = new SQSEventAttribute("@MyQueue")
            {
                MaximumConcurrency = 1001
            };

            var errors = attr.Validate();
            Assert.Single(errors);
            Assert.Contains("MaximumConcurrency", errors[0]);
        }

        [Fact]
        public void Validate_BatchSizeGreaterThan10_RequiresMaximumBatchingWindow()
        {
            var attr = new SQSEventAttribute("@MyQueue")
            {
                BatchSize = 100
            };

            var errors = attr.Validate();
            Assert.Contains(errors, e => e.Contains("MaximumBatchingWindowInSeconds"));
        }

        [Fact]
        public void Validate_FifoQueue_MaximumBatchingWindowNotAllowed()
        {
            var attr = new SQSEventAttribute("arn:aws:sqs:us-east-2:444455556666:test-queue.fifo")
            {
                MaximumBatchingWindowInSeconds = 5
            };

            var errors = attr.Validate();
            Assert.Contains(errors, e => e.Contains("FIFO"));
        }

        [Fact]
        public void Validate_FifoQueue_BatchSizeGreaterThan10NotAllowed()
        {
            var attr = new SQSEventAttribute("arn:aws:sqs:us-east-2:444455556666:test-queue.fifo")
            {
                BatchSize = 100,
                MaximumBatchingWindowInSeconds = 5
            };

            var errors = attr.Validate();
            Assert.Contains(errors, e => e.Contains("FIFO") && e.Contains("BatchSize"));
        }

        [Fact]
        public void Validate_EmptyResourceName_ReturnsError()
        {
            var attr = new SQSEventAttribute("@MyQueue")
            {
                ResourceName = ""
            };

            var errors = attr.Validate();
            Assert.Single(errors);
            Assert.Contains("ResourceName", errors[0]);
        }

        [Fact]
        public void Validate_AllValidWithOptionals_ReturnsNoErrors()
        {
            var attr = new SQSEventAttribute("@MyQueue")
            {
                ResourceName = "MySQSEvent",
                BatchSize = 5,
                MaximumBatchingWindowInSeconds = 60,
                MaximumConcurrency = 30,
                Filters = "{ \"body\" : { \"RequestCode\" : [ \"BBBB\" ] } }",
                Enabled = true
            };

            var errors = attr.Validate();
            Assert.Empty(errors);
        }
    }
}
