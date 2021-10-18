using System.Collections.Generic;
using Amazon.Lambda.Annotations.SourceGenerator.Serialization;

namespace Amazon.Lambda.Annotations.SourceGenerator.Models
{
    /// <summary>
    /// Represents container class for the Lambda function.
    /// </summary>
    public class LambdaFunctionModel : ILambdaFunctionSerializable
    {
        /// <summary>
        /// Gets or sets original method model.
        /// </summary>
        public LambdaMethodModel LambdaMethod { get; set; }

        /// <summary>
        /// Gets or sets generated method model.
        /// </summary>
        public GeneratedMethodModel GeneratedMethod { get; set; }

        /// <summary>
        /// Gets or sets the type of the Startup.cs class that contains the Configure method.
        /// Returns null if there doesn't exist a Startup class.
        /// </summary>
        public TypeModel StartupType { get; set; }

        /// <summary>
        /// Gets or sets fully qualified name of the serializer used for serialization or deserialization.
        /// </summary>
        public string Serializer { get; set; }

        /// <inheritdoc/>
        public string Handler { get; set; }
    }
}