namespace Amazon.Lambda.Annotations.SourceGenerator.Models.Attributes
{
    /// <summary>
    /// Represents data associated with <see cref="LambdaFunctionAttribute"/>.
    /// </summary>
    public class LambdaFunctionAttributeData : IAttributeData
    {
        /// <see cref="LambdaFunctionAttribute.Name"/>
        public string Name { get; set; }

        /// <see cref="LambdaFunctionAttribute.Timeout"/>
        public uint? Timeout { get; set; }

        /// <see cref="LambdaFunctionAttribute.MemorySize"/>
        public uint? MemorySize { get; set; }

        /// <see cref="LambdaFunctionAttribute.Role"/>
        public string Role { get; set; }

        /// <see cref="LambdaFunctionAttribute.Policies"/>
        public string Policies { get; set; }
    }
}