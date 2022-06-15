using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;

namespace Amazon.Lambda.Annotations.SourceGenerator.Models
{
    public interface ISqsQueueSerializable
    {
        string QueueLogicalId { get; set; }
        JToken QueueName { get; set; }
        uint EventBatchSize { get; set; }
        string[] EventFilterCriteria { get; set; }
        string EventQueueARN { get; set; }
        bool ContentBasedDeduplication { get; set; }
        uint EventMaximumBatchingWindowInSeconds { get; set; }
        string DeduplicationScope { get; set; }
        uint DelaySeconds { get; set; }
        bool FifoQueue { get; set; }
        string FifoThroughputLimit { get; set; }
        uint KmsDataKeyReusePeriodSeconds { get; set; }
        string KmsMasterKeyId { get; set; }
        uint MaximumMessageSize { get; set; }
        uint MessageRetentionPeriod { get; set; }
        uint ReceiveMessageWaitTimeSeconds { get; set; }
        string RedriveAllowPolicy { get; set; }
        string RedrivePolicy { get; set; }
        string[] Tags { get; set; }
        uint VisibilityTimeout { get; set; }

    }
}
