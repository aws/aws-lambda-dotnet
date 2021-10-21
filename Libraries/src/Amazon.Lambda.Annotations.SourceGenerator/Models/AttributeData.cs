namespace Amazon.Lambda.Annotations.SourceGenerator.Models
{
    /// <summary>
    /// Interface for attribute data to allow strong typing.
    /// </summary>
    public interface IAttributeData
    {
    }

    /// <summary>
    /// Represents data associated with <see cref="FromPathAttribute"/>.
    /// </summary>
    public class FromPathAttributeData : IAttributeData
    {
        public string Name { get; set; }
    }
}