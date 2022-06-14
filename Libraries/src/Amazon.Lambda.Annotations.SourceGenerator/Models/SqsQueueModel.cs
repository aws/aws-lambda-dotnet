namespace Amazon.Lambda.Annotations.SourceGenerator.Models
{
    public class SqsQueueModel : ISqsQueueSerializable
    {
        public uint EventBatchSize { get; set; }
        public string[] EventFilterCriteria { get; set; }
        public string EventQueueARN { get; set; }
        public bool ContentBasedDeduplication { get; set; }
        public uint EventMaximumBatchingWindowInSeconds { get; set; }
        public string DeduplicationScope { get; set; }
        public uint DelaySeconds { get; set; }
        public bool FifoQueue { get; set; }
        public string FifoThroughputLimit { get; set; }
        public uint KmsDataKeyReusePeriodSeconds { get; set; }
        public string KmsMasterKeyId { get; set; }
        public uint MaximumMessageSize { get; set; }
        public uint MessageRetentionPeriod { get; set; }
        public string QueueName { get; set; }
        public uint ReceiveMessageWaitTimeSeconds { get; set; }
        public string RedriveAllowPolicy { get; set; }
        public string RedrivePolicy { get; set; }
        public string[] Tags { get; set; }
        public uint VisibilityTimeout { get; set; }
        public string QueueLogicalId { get; set; }
    }
}