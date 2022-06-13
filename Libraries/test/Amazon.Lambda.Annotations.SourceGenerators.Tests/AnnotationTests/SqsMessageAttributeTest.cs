using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace Amazon.Lambda.Annotations.SourceGenerators.Tests.AnnotationTests
{
    // I am putting this here because there is no Amazon.Lambda.Annotations.Tests project
    // I did not want to add a project in a pull request without asking first
    public class SqsMessageAttributeTest
    {
        [Fact]
        public void QueueNameQueueLogicalIdMutuallyExclusive()
        {
            var target = new SqsMessageAttribute
            {
                QueueLogicalId = "MyLogicalId"
            };
            Assert.Throws<InvalidOperationException>(() => target.EventQueueARN = "MyQueueName");

            // other way around
            target = new SqsMessageAttribute
            {
                EventQueueARN = "arn:aws:sqs:us-east-1:968993296699:app-deploy-blue-LAVETRYB3JKX-SomeQueueName"
            };
            Assert.Throws<InvalidOperationException>(() => target.QueueLogicalId = "MyLogicalId");
        }

        [Fact]
        public void DeduplicationScopeValidation()
        {
            var target = new SqsMessageAttribute();
            target.DeduplicationScope = "messageGroup";
            target.DeduplicationScope = "queue";
            target.DeduplicationScope = string.Empty;
            target.DeduplicationScope = null;
            Assert.Throws<ArgumentOutOfRangeException>(() => target.DeduplicationScope = "notValid");
        }

        [Fact]
        public void DelaySecondsValidation()
        {
            var target = new SqsMessageAttribute();
            target.DelaySeconds = SqsMessageAttribute.DelaySecondsMinimum;
            target.DelaySeconds = SqsMessageAttribute.DelaySecondsMaximum;
            Assert.Throws<ArgumentOutOfRangeException>(() => target.DelaySeconds = -1);
            Assert.Throws<ArgumentOutOfRangeException>(() => target.DelaySeconds = SqsMessageAttribute.DelaySecondsMaximum + 1);
        }

        [Fact]
        public void FifoThroughputLimit()
        {
            var target = new SqsMessageAttribute();
            target.FifoThroughputLimit = "perQueue";
            target.FifoThroughputLimit = "perMessageGroupId";
            Assert.Throws<ArgumentOutOfRangeException>(() => target.FifoThroughputLimit = "notValid");
        }

        [Fact]
        public void KmsDataKeyReusePeriodSecondsValidation()
        {
            var target = new SqsMessageAttribute();
            target.KmsDataKeyReusePeriodSeconds = SqsMessageAttribute.KmsDataKeyReusePeriodSecondsMinimum;
            target.KmsDataKeyReusePeriodSeconds = SqsMessageAttribute.KmsDataKeyReusePeriodSecondsMaximum;
            var error = Assert.Throws<ArgumentOutOfRangeException>(() => target.KmsDataKeyReusePeriodSeconds = SqsMessageAttribute.KmsDataKeyReusePeriodSecondsMinimum - 1);
            Assert.Equal(nameof(SqsMessageAttribute.KmsDataKeyReusePeriodSeconds), error.ParamName);
            Assert.Equal(SqsMessageAttribute.KmsDataKeyReusePeriodSecondsArgumentOutOfRangeExceptionMessage + $" (Parameter '{nameof(SqsMessageAttribute.KmsDataKeyReusePeriodSeconds)}')", error.Message);
            error = Assert.Throws<ArgumentOutOfRangeException>(() => target.KmsDataKeyReusePeriodSeconds = SqsMessageAttribute.KmsDataKeyReusePeriodSecondsMaximum + 1);
            Assert.Equal(nameof(SqsMessageAttribute.KmsDataKeyReusePeriodSeconds), error.ParamName);
            Assert.Equal(SqsMessageAttribute.KmsDataKeyReusePeriodSecondsArgumentOutOfRangeExceptionMessage + $" (Parameter '{nameof(SqsMessageAttribute.KmsDataKeyReusePeriodSeconds)}')", error.Message);
        }

        [Fact]
        public void MaximumMessageSizeValidation()
        {
            var target = new SqsMessageAttribute();
            target.MaximumMessageSize = SqsMessageAttribute.MaximumMessageSizeMinimum;
            target.MaximumMessageSize = SqsMessageAttribute.MaximumMessageSizeMaximum;
            var error = Assert.Throws<ArgumentOutOfRangeException>(() => target.MaximumMessageSize = SqsMessageAttribute.MaximumMessageSizeMinimum - 1);
            Assert.Equal(nameof(SqsMessageAttribute.MaximumMessageSize), error.ParamName);
            Assert.Equal(SqsMessageAttribute.MaximumMessageSizeArgumentOutOfRangeExceptionMessage + $" (Parameter '{nameof(SqsMessageAttribute.MaximumMessageSize)}')", error.Message);
        }

        [Fact]
        public void MessageRetentionPeriodValidation()
        {
            var target = new SqsMessageAttribute();
            target.MessageRetentionPeriod = SqsMessageAttribute.MessageRetentionPeriodMinimum;
            target.MessageRetentionPeriod = SqsMessageAttribute.MessageRetentionPeriodMaximum;
            var error = Assert.Throws<ArgumentOutOfRangeException>(() => target.MessageRetentionPeriod = SqsMessageAttribute.MessageRetentionPeriodMinimum - 1);
            Assert.Equal(nameof(SqsMessageAttribute.MessageRetentionPeriod), error.ParamName);
            Assert.Equal(SqsMessageAttribute.MessageRetentionPeriodArgumentOutOfRangeExceptionMessage + $" (Parameter '{nameof(SqsMessageAttribute.MessageRetentionPeriod)}')", error.Message);
            error = Assert.Throws<ArgumentOutOfRangeException>(() => target.MessageRetentionPeriod = SqsMessageAttribute.MessageRetentionPeriodMaximum + 1);
            Assert.Equal(nameof(SqsMessageAttribute.MessageRetentionPeriod), error.ParamName);
            Assert.Equal(SqsMessageAttribute.MessageRetentionPeriodArgumentOutOfRangeExceptionMessage + $" (Parameter '{nameof(SqsMessageAttribute.MessageRetentionPeriod)}')", error.Message);
        }

        [Fact]
        public void QueueNameValidation()
        {
            var target = new SqsMessageAttribute();
            target.QueueName = "MyQueueName";
            target.QueueName = "MyQueueName.fifo";
            // no exception, no problem
        }

        [Fact]
        public void EventBatchSizeValidation()
        {
            var target = new SqsMessageAttribute();
            target.EventBatchSize = SqsMessageAttribute.EventBatchSizeMinimum;
            target.EventBatchSize = SqsMessageAttribute.EventBatchSizeMaximum;

            var error = Assert.Throws<ArgumentOutOfRangeException>(() => target.EventBatchSize = SqsMessageAttribute.EventBatchSizeMinimum - 1);
            Assert.Equal(nameof(SqsMessageAttribute.EventBatchSize), error.ParamName);
            Assert.Equal(SqsMessageAttribute.EventBatchSizeArgumentOutOfRangeExceptionMessage + $" (Parameter '{nameof(SqsMessageAttribute.EventBatchSize)}')", error.Message);

            error = Assert.Throws<ArgumentOutOfRangeException>(() => target.EventBatchSize = SqsMessageAttribute.EventBatchSizeMaximum + 1);
            Assert.Equal(nameof(SqsMessageAttribute.EventBatchSize), error.ParamName);
            Assert.Equal(SqsMessageAttribute.EventBatchSizeArgumentOutOfRangeExceptionMessage + $" (Parameter '{nameof(SqsMessageAttribute.EventBatchSize)}')", error.Message);
        }

        [Fact]
        public void VisibilityTimeoutValidation()
        {
            var target = new SqsMessageAttribute();
            target.VisibilityTimeout = SqsMessageAttribute.VisibilityTimeoutMinimum;
            target.VisibilityTimeout = SqsMessageAttribute.VisibilityTimeoutMaximum;

            var error = Assert.Throws<ArgumentOutOfRangeException>(() => target.VisibilityTimeout = SqsMessageAttribute.VisibilityTimeoutMinimum - 1);
            Assert.Equal(nameof(SqsMessageAttribute.VisibilityTimeout), error.ParamName);
            Assert.Equal(SqsMessageAttribute.VisibilityTimeoutArgumentOutOfRangeExceptionMessage + $" (Parameter '{nameof(SqsMessageAttribute.VisibilityTimeout)}')", error.Message);

            error = Assert.Throws<ArgumentOutOfRangeException>(() => target.VisibilityTimeout = SqsMessageAttribute.VisibilityTimeoutMaximum + 1);
            Assert.Equal(nameof(SqsMessageAttribute.VisibilityTimeout), error.ParamName);
            Assert.Equal(SqsMessageAttribute.VisibilityTimeoutArgumentOutOfRangeExceptionMessage + $" (Parameter '{nameof(SqsMessageAttribute.VisibilityTimeout)}')", error.Message);

        }
    }
}
