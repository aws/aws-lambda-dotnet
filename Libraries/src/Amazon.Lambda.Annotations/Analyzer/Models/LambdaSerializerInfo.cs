using System;
using System.Collections.Generic;
using System.Text;

namespace Amazon.Lambda.Annotations.SourceGenerator.Models
{
    /// <summary>
    /// Information for the registered ILambdaSerializer in the Lambda project.
    /// </summary>
    public class LambdaSerializerInfo
    {
        /// <summary>
        /// Construct an instance of LambdaSerializerInfo
        /// </summary>
        /// <param name="serializerName"></param>
        public LambdaSerializerInfo(string serializerName)
        {
            SerializerName = serializerName;
        }

        /// <summary>
        /// The full name of the type registered as the ILambdaSerializer.
        /// </summary>
        public string SerializerName { get; }
    }
}
