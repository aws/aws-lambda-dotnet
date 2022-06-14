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
        [Theory]
        [InlineData("")]
        [InlineData(null)]
        [InlineData(SqsMessageAttribute.DeduplicationScopeMessageGroup)]
        [InlineData(SqsMessageAttribute.DeduplicationScopeMessageQueue)]
        [InlineData("invalidValue", typeof(ArgumentOutOfRangeException), SqsMessageAttribute.DeduplicationScopeArgumentOutOfRangeExceptionMessage)]
        public void DeduplicationScopeValidation(string value, Type expectedException = null, string messageFormat = null)
        {
            var target = new SqsMessageAttribute();
            if (expectedException == null)
            {
                target.DeduplicationScope = value;
                // no exception, all good
            }
            else
            {
                var error = Assert.Throws(expectedException, () => target.DeduplicationScope = value) as ArgumentException;
                Assert.Equal(nameof(SqsMessageAttribute.DeduplicationScope), error.ParamName);
                Assert.Equal(string.Format(SqsMessageAttribute.DeduplicationScopeArgumentOutOfRangeExceptionMessage,
                    nameof(SqsMessageAttribute.DeduplicationScope), 
                    string.Join(',', SqsMessageAttribute.ValidDeduplicationScopes))
                             + $" (Parameter '{nameof(SqsMessageAttribute.DeduplicationScope)}')", error.Message);

            }
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

        [Theory]
        [InlineData(SqsMessageAttribute.MaximumMessageSizeMinimum)]
        [InlineData(SqsMessageAttribute.MaximumMessageSizeMaximum)]
        [InlineData(SqsMessageAttribute.MaximumMessageSizeMinimum - 1, typeof(ArgumentOutOfRangeException), SqsMessageAttribute.UintPropertyBetweenExceptionMessage)]
        [InlineData(SqsMessageAttribute.MaximumMessageSizeMaximum + 1, typeof(ArgumentOutOfRangeException), SqsMessageAttribute.UintPropertyBetweenExceptionMessage)]
        public void MaximumMessageSizeValidation(uint value, Type expectedException = null, string expectedErrorFormat = null)
        {
            var target = new SqsMessageAttribute();
            if (expectedException == null)
            {
                target.MaximumMessageSize = value;
            }
            else
            {
                var error = Assert.Throws(expectedException, () => target.MaximumMessageSize = value) as ArgumentException;
                Assert.Equal(nameof(SqsMessageAttribute.MaximumMessageSize), error.ParamName);
                Assert.Equal(string.Format(expectedErrorFormat, nameof(SqsMessageAttribute.MaximumMessageSize), SqsMessageAttribute.MaximumMessageSizeMinimum, SqsMessageAttribute.MaximumMessageSizeMaximum) + $" (Parameter '{nameof(SqsMessageAttribute.MaximumMessageSize)}')", error.Message);

            }
        }

        [Theory]
        [InlineData(SqsMessageAttribute.MessageRetentionPeriodMinimum)]
        [InlineData(SqsMessageAttribute.MessageRetentionPeriodMaximum)]
        [InlineData(SqsMessageAttribute.MessageRetentionPeriodMinimum - 1, typeof(ArgumentOutOfRangeException), SqsMessageAttribute.UintPropertyBetweenExceptionMessage)]
        [InlineData(SqsMessageAttribute.MessageRetentionPeriodMaximum + 1, typeof(ArgumentOutOfRangeException), SqsMessageAttribute.UintPropertyBetweenExceptionMessage)]
        public void MessageRetentionPeriodValidation(uint value, Type expectedException = null, string expectedMessageFormat = null)
        {
            var target = new SqsMessageAttribute();
            if (expectedException == null)
            {
                target.MessageRetentionPeriod = value;
            }
            else
            {
                var error = Assert.Throws(expectedException, () => target.MessageRetentionPeriod = SqsMessageAttribute.MessageRetentionPeriodMinimum - 1) as ArgumentException;
                Assert.Equal(nameof(SqsMessageAttribute.MessageRetentionPeriod), error.ParamName);
                Assert.Equal(string.Format(expectedMessageFormat, nameof(SqsMessageAttribute.MessageRetentionPeriod), SqsMessageAttribute.MessageRetentionPeriodMinimum, SqsMessageAttribute.MessageRetentionPeriodMaximum)  + $" (Parameter '{nameof(SqsMessageAttribute.MessageRetentionPeriod)}')", error.Message);
            }
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
        [InlineData(SqsMessageAttribute.ReceiveMessageWaitTimeSecondsMaximum + 1, typeof(ArgumentOutOfRangeException), SqsMessageAttribute.UintPropertyBetweenExceptionMessage)]
        public void ReceiveMessageWaitTimeSecondsValidation(uint value, Type throws = null, string messageFormat = null)
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
                Assert.Equal(string.Format(messageFormat, nameof(SqsMessageAttribute.ReceiveMessageWaitTimeSeconds), SqsMessageAttribute.ReceiveMessageWaitTimeSecondsMinimum, SqsMessageAttribute.ReceiveMessageWaitTimeSecondsMaximum) + $" (Parameter '{nameof(SqsMessageAttribute.ReceiveMessageWaitTimeSeconds)}')", error.Message);

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

        [Theory]
        [InlineData(SqsMessageAttribute.MaximumBatchingWindowInSecondsDefault)]
        [InlineData(SqsMessageAttribute.MaximumBatchingWindowInSecondsMinimum)]
        [InlineData(SqsMessageAttribute.MaximumBatchingWindowInSecondsMaximum)]
        [InlineData(SqsMessageAttribute.MaximumBatchingWindowInSecondsMaximum + 1, typeof(ArgumentOutOfRangeException), SqsMessageAttribute.UintPropertyBetweenExceptionMessage)]
        public void MaximumBatchingWindowInSecondsValidation(uint value, Type throws = null, string messageFormat = null)
        {
            var target = new SqsMessageAttribute();
            if (throws == null)
            {
                target.EventMaximumBatchingWindowInSeconds = value;
            }
            else
            {
                var error = Assert.Throws(throws, () => target.EventMaximumBatchingWindowInSeconds = value) as ArgumentException;
                Assert.Equal(nameof(SqsMessageAttribute.EventMaximumBatchingWindowInSeconds), error.ParamName);
                Assert.Equal(string.Format(messageFormat, nameof(SqsMessageAttribute.EventMaximumBatchingWindowInSeconds), SqsMessageAttribute.MaximumBatchingWindowInSecondsMinimum, SqsMessageAttribute.MaximumBatchingWindowInSecondsMaximum) + $" (Parameter '{nameof(SqsMessageAttribute.EventMaximumBatchingWindowInSeconds)}')", error.Message);

            }
        }


    }
}
