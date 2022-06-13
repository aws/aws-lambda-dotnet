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

        [Theory]
        [InlineData(SqsMessageAttribute.KmsDataKeyReusePeriodSecondsMinimum)]
        [InlineData(SqsMessageAttribute.KmsDataKeyReusePeriodSecondsMaximum)]
        [InlineData(SqsMessageAttribute.KmsDataKeyReusePeriodSecondsMinimum - 1, typeof(ArgumentOutOfRangeException), SqsMessageAttribute.UintPropertyBetweenExceptionMessage)]
        [InlineData(SqsMessageAttribute.KmsDataKeyReusePeriodSecondsMaximum + 1, typeof(ArgumentOutOfRangeException), SqsMessageAttribute.UintPropertyBetweenExceptionMessage)]
        public void KmsDataKeyReusePeriodSecondsValidation(uint value, Type throws = null, string messageFormat = null)
        {
            var target = new SqsMessageAttribute();
            if (throws == null)
            {
                target.KmsDataKeyReusePeriodSeconds = value;
            }
            else
            {
                var error = Assert.Throws(throws, () => target.KmsDataKeyReusePeriodSeconds = value) as ArgumentException;
                Assert.Equal(nameof(SqsMessageAttribute.KmsDataKeyReusePeriodSeconds), error.ParamName);
                Assert.Equal( string.Format(messageFormat, nameof(SqsMessageAttribute.KmsDataKeyReusePeriodSeconds), SqsMessageAttribute.KmsDataKeyReusePeriodSecondsMinimum, SqsMessageAttribute.KmsDataKeyReusePeriodSecondsMaximum) + $" (Parameter '{nameof(SqsMessageAttribute.KmsDataKeyReusePeriodSeconds)}')", 
                    error.Message);

            }
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

        [Theory]
        [InlineData(SqsMessageAttribute.EventBatchSizeMinimum)]
        [InlineData(SqsMessageAttribute.EventBatchSizeMaximum)]
        [InlineData(SqsMessageAttribute.EventBatchSizeMinimum - 1, typeof(ArgumentOutOfRangeException), SqsMessageAttribute.UintPropertyBetweenExceptionMessage)]
        [InlineData(SqsMessageAttribute.EventBatchSizeMaximum + 1, typeof(ArgumentOutOfRangeException), SqsMessageAttribute.UintPropertyBetweenExceptionMessage)]
        public void EventBatchSizeValidation(uint value, Type throws = null, string messageFormat = null)
        {
            var target = new SqsMessageAttribute();
            if (throws == null)
            {
                target.EventBatchSize = value;
            }
            else
            {
                var error = Assert.Throws(throws, () => target.EventBatchSize = value) as ArgumentException;
                Assert.Equal(nameof(SqsMessageAttribute.EventBatchSize), error.ParamName);
                Assert.Equal(string.Format(messageFormat, nameof(SqsMessageAttribute.EventBatchSize), SqsMessageAttribute.EventBatchSizeMinimum, SqsMessageAttribute.EventBatchSizeMaximum) + $" (Parameter '{nameof(SqsMessageAttribute.EventBatchSize)}')", error.Message);

            }
        }

        [Theory]
        [InlineData(SqsMessageAttribute.VisibilityTimeoutMinimum)]
        [InlineData(SqsMessageAttribute.VisibilityTimeoutMaximum)]
        [InlineData(SqsMessageAttribute.VisibilityTimeoutMaximum+1, typeof(ArgumentOutOfRangeException), SqsMessageAttribute.UintPropertyBetweenExceptionMessage)]
        public void VisibilityTimeoutValidation(uint value, Type throws = null, string message = null)
        {
            var target = new SqsMessageAttribute();
            if (throws == null)
            {
                target.VisibilityTimeout = value;
            }
            else
            {
                var error = Assert.Throws(throws, () => target.VisibilityTimeout = value) as ArgumentException;
                Assert.Equal(nameof(SqsMessageAttribute.VisibilityTimeout), error.ParamName);
                Assert.Equal(
                    string.Format(message, nameof(SqsMessageAttribute.VisibilityTimeout), SqsMessageAttribute.VisibilityTimeoutMinimum, SqsMessageAttribute.VisibilityTimeoutMaximum) + $" (Parameter '{nameof(SqsMessageAttribute.VisibilityTimeout)}')", 
                    error.Message);

            }


        }

        [Theory]
        [InlineData(SqsMessageAttribute.ReceiveMessageWaitTimeSecondsMinimum)]
        [InlineData(SqsMessageAttribute.ReceiveMessageWaitTimeSecondsMaximum + 1, typeof(ArgumentOutOfRangeException), SqsMessageAttribute.ReceiveMessageWaitTimeSecondsArgumentOutOfRangeExceptionMessage + $" (Parameter '{nameof(SqsMessageAttribute.ReceiveMessageWaitTimeSeconds)}')")]
        public void ReceiveMessageWaitTimeSecondsValidation(uint value, Type throws = null, string message = null)
        {
            var target = new SqsMessageAttribute();
            if (throws == null)
            {
                target.ReceiveMessageWaitTimeSeconds = value;
            }
            else
            {
                var error = Assert.Throws(throws, () => target.ReceiveMessageWaitTimeSeconds = value) as ArgumentException;
                Assert.Equal(nameof(SqsMessageAttribute.ReceiveMessageWaitTimeSeconds), error.ParamName);
                Assert.Equal(message, error.Message);

            }
        }

        [Theory]
        [InlineData("{ 'deadLetterTargetArn': 'arn:somewhere', 'maxReceiveCount': 5 }")]
        public void RedriveAllowPolicyValidation(string value, Type exceptionType = null, string exceptionMessage = null)
        {
            var target = new SqsMessageAttribute();
            if (exceptionType == null)
            {
                target.RedrivePolicy = value;
            }
            else
            {
                var error = Assert.Throws(exceptionType, () => target.RedrivePolicy = value) as ArgumentException;
                Assert.Equal(nameof(SqsMessageAttribute.RedrivePolicy), error.ParamName);
                Assert.Equal(exceptionMessage, error.Message);
            }

        }
    }
}
