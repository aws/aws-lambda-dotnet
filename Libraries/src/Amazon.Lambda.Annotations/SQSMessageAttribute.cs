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
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class SqsMessageAttribute : Attribute, ISqsMessage
    {
        public SqsMessageAttribute()
        {
            
        }
        public string QueueName { get; set; }
        public int BatchSize { get; set; }

        public string QueueLogicalId { get; set; }
        public int VisibilityTimeout { get; set; } = SqsMessageAttribute.VisibilityTimeoutDefault;
        public const int VisibilityTimeoutDefault = 30;
    }
}
