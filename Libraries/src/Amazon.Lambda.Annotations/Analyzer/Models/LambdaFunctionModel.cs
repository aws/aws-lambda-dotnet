using System.Collections.Generic;
using System.Linq;
using Amazon.Lambda.Annotations.SourceGenerator.Models.Attributes;

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
        /// The original method name.
        /// </summary>
        public string MethodName => LambdaMethod.Name;

        /// <summary>
        /// Gets or sets fully qualified name of the serializer used for serialization or deserialization.
        /// </summary>
        public LambdaSerializerInfo SerializerInfo { get; set; } =
            new LambdaSerializerInfo("Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer");
        
        /// <summary>
        /// Gets or sets if the output is an executable.
        /// </summary>
        public bool IsExecutable { get; set; }
        
        /// <summary>
        /// Gets or sets the Lambda runtime to use.
        /// </summary>
        public string Runtime { get; set; }

        /// <inheritdoc />
        public string Handler => IsExecutable ? LambdaMethod.ContainingAssembly : $"{LambdaMethod.ContainingAssembly}::{GeneratedMethod.ContainingType.FullName}::{LambdaMethod.Name}";

        /// <inheritdoc />
        public string ResourceName => LambdaMethod.LambdaFunctionAttribute.Data.ResourceName ??
                              string.Join(string.Empty, GeneratedMethod.ContainingType.FullName.Where(char.IsLetterOrDigit));

        /// <inheritdoc />
        public uint? Timeout => LambdaMethod.LambdaFunctionAttribute.Data.Timeout;

        /// <inheritdoc />
        public uint? MemorySize => LambdaMethod.LambdaFunctionAttribute.Data.MemorySize;

        /// <inheritdoc />
        public string Role => LambdaMethod.LambdaFunctionAttribute.Data.Role;

        /// <inheritdoc />
        public string Policies => LambdaMethod.LambdaFunctionAttribute.Data.Policies;

        /// <inheritdoc />
        /// The default value is set to Zip
        public LambdaPackageType PackageType  => LambdaMethod.LambdaFunctionAttribute.Data.PackageType;

        /// <inheritdoc />
        public IList<AttributeModel> Attributes => LambdaMethod.Attributes ?? new List<AttributeModel>();

        /// <inheritdoc />
        public string ReturnTypeFullName  => LambdaMethod.ReturnType.FullName;

        /// <inheritdoc />
        public string SourceGeneratorVersion { get; set; }

        /// <summary>
        /// Indicates if the model is valid.
        /// </summary>
        public bool IsValid {  get; set; }
    }
}