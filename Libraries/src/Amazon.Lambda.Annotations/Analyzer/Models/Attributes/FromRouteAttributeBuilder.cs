using Amazon.Lambda.Annotations.APIGateway;
using Microsoft.CodeAnalysis;

namespace Amazon.Lambda.Annotations.SourceGenerator.Models.Attributes
{
    /// <summary>
    /// Builder for <see cref="FromRouteAttribute"/>.
    /// </summary>
    internal class FromRouteAttributeBuilder
    {
        public static FromRouteAttribute Build(AttributeData att)
        {
            var data = new FromRouteAttribute();
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