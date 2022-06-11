using System;
using System.Collections.Generic;
using System.Text;

namespace Amazon.Lambda.Annotations
{

    public interface ISqsMessage
    {
        string LogicalId { get; set; }
        string QueueName { get; set; }
    }

    [AttributeUsage(AttributeTargets.Parameter)]
    public class SqsMessageAttribute : Attribute, ISqsMessage
    {
        public string LogicalId { get; set; }
        public string QueueName { get; set; }

        public SqsMessageAttribute()
        {
            
        }

        public SqsMessageAttribute(string logicalId = null, string queueName = null)
        {
            LogicalId = logicalId;
            QueueName = queueName;
        }
    }
}
