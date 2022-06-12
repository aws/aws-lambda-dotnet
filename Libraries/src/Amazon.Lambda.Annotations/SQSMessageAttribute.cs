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
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class SqsMessageAttribute : Attribute, ISqsMessage
    {
        public const bool ContentBasedDeduplicationDefault = false;
        public const int VisibilityTimeoutDefault = 30;

        public string QueueName { get; set; }
        public int BatchSize { get; set; }

        public string QueueLogicalId { get; set; }
        public int VisibilityTimeout { get; set; } = VisibilityTimeoutDefault;
        public bool ContentBasedDeduplication { get; set; } = ContentBasedDeduplicationDefault;
    }
}
