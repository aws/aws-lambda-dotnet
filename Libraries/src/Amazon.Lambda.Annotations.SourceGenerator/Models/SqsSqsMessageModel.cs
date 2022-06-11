using System;
using System.Collections.Generic;
using System.Linq;
using Amazon.Lambda.Annotations.SourceGenerator.Models.Attributes;

namespace Amazon.Lambda.Annotations.SourceGenerator.Models
{
    /// <summary>
    /// Represents container class for the Lambda function.
    /// </summary>
    public class SqsSqsMessageModel : ISqsMessageSerializable
    {
        public string LogicalId { get; set; }
        public string QueueName { get; set; }
        public string SourceGeneratorVersion { get; set; }
    }
}