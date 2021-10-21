namespace Amazon.Lambda.Annotations.SourceGenerator.Models.Attributes
{
    /// <summary>
    /// Represents attribute used on a type.
    /// </summary>
    public class AttributeModel
    {
        /// <summary>
        /// Gets or sets type of the attribute
        /// </summary>
        public TypeModel Type { get; set; }
    }

    /// <summary>
    /// Represents attribute used on a type.
    /// </summary>
    /// <typeparam name="T">
    /// Type of attribute data, for example a FromPath attribute can optionally have Name
    /// which can be modeled using attribute data.
    /// </typeparam>
    public class AttributeModel<T> : AttributeModel where T : IAttributeData
    {
        /// <summary>
        /// Gets or sets data associated with attribute.
        /// </summary>
        public T Data { get; set; }
    }
}