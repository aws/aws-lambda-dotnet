using System;
using System.Collections.Generic;
using System.Text;

namespace Amazon.Lambda.Annotations
{

    public interface ISqsMessage
    {
        string QueueName { get; set; }
        int BatchSize { get; set; }
        string QueueLogicalId { get; set; }
        int VisibilityTimeout { get; set; }
        /// <summary>
        /// For first-in-first-out (FIFO) queues, specifies whether to enable content-based deduplication. During the deduplication interval, Amazon SQS treats messages that are sent with identical content as duplicates and delivers only one copy of the message. For more information, see the ContentBasedDeduplication attribute for the CreateQueue action in the Amazon SQS API Reference.
        /// Required: No
        /// Type: Boolean
        /// Update requires: No interruption
        /// </summary>
        bool ContentBasedDeduplication { get; set; }

        /// <summary>
        /// For high throughput for FIFO queues, specifies whether message deduplication occurs at the message group or queue level. Valid values are messageGroup and queue.
        /// To enable high throughput for a FIFO queue, set this attribute to messageGroup and set the FifoThroughputLimit attribute to perMessageGroupId. If you set these attributes to anything other than these values, normal throughput is in effect and deduplication occurs as specified.For more information, see High throughput for FIFO queues and Quotas related to messages in the Amazon SQS Developer Guide.
        /// 
        /// Required: No
        /// 
        /// Type: String
        /// Update requires: No interruption
        /// </summary>
        string DeduplicationScope { get; set; }

        /// <summary>
        /// If set to true, creates a FIFO queue. If you don't specify this property, Amazon SQS creates a standard queue. For more information, see FIFO queues in the Amazon SQS Developer Guide.
        /// Required: No
        /// Type: Boolean
        /// Update requires: Replacement
        /// </summary>
        int DelaySeconds { get; set; }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class SqsMessageAttribute : Attribute, ISqsMessage
    {
        public const bool ContentBasedDeduplicationDefault = false;
        public const int VisibilityTimeoutDefault = 30;
        public const int BatchSizeDefault = 10;
        public const int DelaySecondsDefault = 0;

        public string QueueName { get; set; }
        public int BatchSize { get; set; } = BatchSizeDefault;

        public string QueueLogicalId { get; set; }
        public int VisibilityTimeout { get; set; } = VisibilityTimeoutDefault;
        public bool ContentBasedDeduplication { get; set; } = ContentBasedDeduplicationDefault;
        public string DeduplicationScope { get; set; }
        public int DelaySeconds { get; set; } = DelaySecondsDefault;
    }
}
