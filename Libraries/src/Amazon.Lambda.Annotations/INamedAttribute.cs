namespace Amazon.Lambda.Annotations
{
    /// <summary>
    /// Interface for a named attribute
    /// </summary>
    public interface INamedAttribute
    {
        /// <summary>
        /// Name of the attribute
        /// </summary>
        string Name { get; set; }
    }
}