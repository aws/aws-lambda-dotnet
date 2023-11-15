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
        /// <param name="serializerJsonContextName"></param>
        public LambdaSerializerInfo(string serializerName, string serializerJsonContextName)
        {
            SerializerName = serializerName;
            SerializerJsonContextName = serializerJsonContextName;
        }

        /// <summary>
        /// The full name of the type registered as the ILambdaSerializer.
        /// </summary>
        public string SerializerName { get; }

        /// <summary>
        /// The full name of the type used as the generic parameter of the SourceGeneratorLambdaJsonSerializer.
        /// </summary>
        public string SerializerJsonContextName { get; }   
    }
}
