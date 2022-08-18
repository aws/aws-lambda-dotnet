using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;

namespace Amazon.Lambda.Annotations.SourceGenerator.Models
{
    public static class SqsQueueModelBuilder
    {
        public static ISqsQueueSerializable Build(ILambdaFunctionSerializable lambdaFunction, SqsMessageAttribute sqsMessageAttribute)
        {
            ISqsQueueSerializable sqsQueueModel = new SqsQueueModel()
            {
                DelaySeconds = sqsMessageAttribute.DelaySeconds,
                DeduplicationScope = sqsMessageAttribute.DeduplicationScope,
                KmsDataKeyReusePeriodSeconds = sqsMessageAttribute.KmsDataKeyReusePeriodSeconds,
                FifoThroughputLimit = sqsMessageAttribute.FifoThroughputLimit,
                FifoQueue = sqsMessageAttribute.FifoQueue,
                VisibilityTimeout = sqsMessageAttribute.VisibilityTimeout,
                Tags = sqsMessageAttribute.Tags,
                ContentBasedDeduplication = sqsMessageAttribute.ContentBasedDeduplication,
                EventMaximumBatchingWindowInSeconds = sqsMessageAttribute.EventMaximumBatchingWindowInSeconds,
                EventFilterCriteria = sqsMessageAttribute.EventFilterCriteria,
                EventBatchSize = sqsMessageAttribute.EventBatchSize,
                ReceiveMessageWaitTimeSeconds = sqsMessageAttribute.ReceiveMessageWaitTimeSeconds,
                MessageRetentionPeriod = sqsMessageAttribute.MessageRetentionPeriod,
                MaximumMessageSize = sqsMessageAttribute.MaximumMessageSize,
                RedriveAllowPolicy = sqsMessageAttribute.RedriveAllowPolicy,
                RedrivePolicy = sqsMessageAttribute.RedrivePolicy,
                KmsMasterKeyId = sqsMessageAttribute.KmsMasterKeyId,
                EventQueueARN = sqsMessageAttribute.EventQueueARN,
                // Only set the QueueLogicalId if the EventQueueArn is not set
                // because the QueueLogicalId becomes irrelevent if
                // the function is using an existing queue
                QueueLogicalId = string.IsNullOrEmpty(sqsMessageAttribute.EventQueueARN) ? lambdaFunction.Name + "Queue" : null
            };
            if (!string.IsNullOrEmpty(sqsMessageAttribute.QueueName))
            {
                if (sqsMessageAttribute.QueueName.TrimStart(' ','\t').StartsWith("{")
                    && sqsMessageAttribute.QueueName.TrimEnd(' ', '\t').EndsWith("}"))
                {
                    sqsQueueModel.QueueName = JObject.Parse(sqsMessageAttribute.QueueName);
                }
                else
                {
                    sqsQueueModel.QueueName = new JValue(sqsMessageAttribute.QueueName);

                }
            }
            else if (sqsMessageAttribute.FifoQueue)
            {
                sqsQueueModel.QueueName = new JValue(lambdaFunction.Name + "Queue.fifo");
            }

            return sqsQueueModel;
        }
    }
}
