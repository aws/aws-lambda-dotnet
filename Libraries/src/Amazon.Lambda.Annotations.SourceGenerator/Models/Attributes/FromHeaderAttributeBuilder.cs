using Microsoft.CodeAnalysis;

namespace Amazon.Lambda.Annotations.SourceGenerator.Models.Attributes
{
    /// <summary>
    /// Builder for <see cref="FromHeaderAttribute"/>.
    /// </summary>
    public class FromHeaderAttributeBuilder
    {
        public static FromHeaderAttribute Build(AttributeData att)
        {
            var data = new FromHeaderAttribute();
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