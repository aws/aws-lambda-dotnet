namespace Amazon.Lambda.Annotations
{
    /// <summary>
    /// An interface for a Named Attribute
    /// </summary>
    public interface INamedAttribute
    {
        /// <summary>
        /// The name of the attribute
        /// </summary>
        string Name { get; set; }
    }
}