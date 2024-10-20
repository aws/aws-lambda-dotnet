using YamlDotNet.RepresentationModel;

namespace Amazon.Lambda.Annotations.SourceGenerator.Writers
{
    /// <summary>
    /// These enums dictate the deserialized <see cref="YamlNode"/> type during the invocation of YamlWriter.SetToken(..)
    /// These token types do not play any roles when using a jsonWriter.
    /// </summary>
    public enum TokenType
    {
        /// <summary>
        /// This token type is deserialized to a <see cref="YamlSequenceNode"/>.
        /// </summary>
        List,

        /// <summary>
        /// This token type is deserialized to a <see cref="YamlMappingNode"/>.
        /// </summary>
        KeyVal,

        /// <summary>
        /// This token type is deserialized to a <see cref="YamlMappingNode"/>.
        /// </summary>
        Object,

        /// <summary>
        /// This token type is deserialized to a <see cref="YamlScalarNode"/>.
        /// </summary>
        Other
    }
}