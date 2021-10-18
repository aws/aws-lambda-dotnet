namespace Amazon.Lambda.Annotations.SourceGenerator.Serialization
{
    /// <summary>
    /// Defines contract between Source Generators and serverless template writers
    /// </summary>
    public interface ILambdaFunctionSerializable
    {
        /// <summary>
        /// The function within code that is called to begin execution.
        /// </summary>
        string Handler { get; }
    }
}