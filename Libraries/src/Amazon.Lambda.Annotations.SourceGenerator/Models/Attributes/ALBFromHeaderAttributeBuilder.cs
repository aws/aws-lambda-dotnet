using Amazon.Lambda.Annotations.ALB;
using Microsoft.CodeAnalysis;

namespace Amazon.Lambda.Annotations.SourceGenerator.Models.Attributes
{
    /// <summary>
    /// Builder for <see cref="ALB.FromHeaderAttribute"/>.
    /// </summary>
    public class ALBFromHeaderAttributeBuilder
    {
        public static ALB.FromHeaderAttribute Build(AttributeData att)
        {
            var data = new ALB.FromHeaderAttribute();
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
