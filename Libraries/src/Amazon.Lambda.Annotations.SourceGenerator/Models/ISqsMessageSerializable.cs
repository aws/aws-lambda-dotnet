using System;
using System.Collections.Generic;
using System.Text;

namespace Amazon.Lambda.Annotations.SourceGenerator.Models
{
    public interface ISqsMessageSerializable : ISqsMessage
    {
        string SourceGeneratorVersion { get; set; }
    }
}
