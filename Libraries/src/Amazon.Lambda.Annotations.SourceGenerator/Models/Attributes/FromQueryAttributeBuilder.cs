using Microsoft.CodeAnalysis;

namespace Amazon.Lambda.Annotations.SourceGenerator.Models.Attributes
{
    /// <summary>
    /// Builder for <see cref="FromQueryAttribute"/>.
    /// </summary>
    public class FromQueryAttributeBuilder
    {
        public static FromQueryAttribute Build(AttributeData att)
        {
            var data = new FromQueryAttribute();
            foreach (var pair in att.NamedArguments)
            {
                if (pair.Key == nameof(data.Name) && pair.Value.Value is string value)
                {
                    data.Name = value;
                }
            }

            return data;
        }
    }
}