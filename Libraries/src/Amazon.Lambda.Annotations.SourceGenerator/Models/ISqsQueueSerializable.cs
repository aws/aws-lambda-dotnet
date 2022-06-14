using System;
using System.Collections.Generic;
using System.Text;

namespace Amazon.Lambda.Annotations.SourceGenerator.Models
{
    public interface ISqsQueueSerializable : ISqsMessage
    {
        string QueueLogicalId { get; set; }
    }
}
