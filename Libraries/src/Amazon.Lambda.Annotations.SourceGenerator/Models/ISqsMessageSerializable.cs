using System;
using System.Collections.Generic;
using System.Text;

namespace Amazon.Lambda.Annotations.SourceGenerator.Models
{
    public interface ISqsMessageSerializable
    {
        string LogicalId { get; set; }
        string QueueName { get; set; }
        string SourceGeneratorVersion { get; set; }
    }
}
