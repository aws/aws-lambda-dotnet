namespace Amazon.Lambda.Annotations.SourceGenerator.Models
{
    /// <summary>
    /// Represents a type.
    /// </summary>
    public class TypeModel
    {
        /// <summary>
        /// Gets or sets the name of the type.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the full qualified name of the type.
        /// In some cases such as value types (ex. int), FullName will be the alias i.e. int in spite of System.Int32.
        /// </summary>
        public string FullName { get; set; }

        /// <summary>
        /// True if this type is a value type.
        /// </summary>
        public bool IsValueType { get; set; }

        /// <summary>
        /// True if this type is a <see cref="string"/> type.
        /// </summary>
        /// <returns></returns>
        public bool IsString()
        {
            return FullName == "string";
        }
    }
}