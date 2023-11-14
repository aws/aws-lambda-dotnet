using System;
using System.Collections.Generic;
using System.Text;

namespace Amazon.Lambda.Annotations.SourceGenerator.Models
{
    public class LambdaSerializerInfo
    {
        public LambdaSerializerInfo(string serializerName, string serializerJsonContextName)
        {
            SerializerName = serializerName;
            SerializerJsonContextName = serializerJsonContextName;
        }

        public string SerializerName { get; }

        public string SerializerJsonContextName { get; }   
    }
}
